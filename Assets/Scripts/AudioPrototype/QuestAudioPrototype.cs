using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    public sealed class QuestAudioPrototype : MonoBehaviour
    {
        private const int TargetSampleRate = 48000;
        private const int MicrophoneClipSeconds = 2;
        private const int SpectrumSize = 512;
        private const float MinimumMonitorVolume = 0f;
        private const float MaximumMonitorVolume = 1.4f;
        private const float HotInputLevel = 0.95f;

        [Header("Signal Chain")]
        [SerializeField] private AudioSource monitorSource;
        [SerializeField] private AudioReverbFilter reverbFilter;
        [SerializeField] private AudioEchoFilter echoFilter;
        [SerializeField] private AudioHighPassFilter highPassFilter;
        [SerializeField] private AudioLowPassFilter lowPassFilter;

        [Header("UI")]
        [SerializeField] private Text statusText;
        [SerializeField] private Text metricsText;
        [SerializeField] private Text presetText;
        [SerializeField] private Slider monitorVolumeSlider;
        [SerializeField] private Slider inputLevelSlider;
        [SerializeField] private Slider outputLevelSlider;
        [SerializeField] private Button startButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button previousPresetButton;
        [SerializeField] private Button nextPresetButton;
        [SerializeField] private Toggle monitorToggle;
        [SerializeField] private Toggle safetyToggle;

        [Header("Runtime Defaults")]
        [SerializeField, Range(0f, MaximumMonitorVolume)] private float monitorVolume = 0.9f;
        [SerializeField] private bool safetyLimiterEnabled = true;
        [SerializeField] private bool requestLowLatencyAudio = true;
        [SerializeField] private PrototypePreset initialPreset = PrototypePreset.KtvRoom;

        private readonly float[] microphoneSamples = new float[SpectrumSize];
        private readonly float[] outputSamples = new float[SpectrumSize];
        private readonly StringBuilder metricsBuilder = new StringBuilder(512);

        private PrototypePreset currentPreset;
        private AudioClip microphoneClip;
        private string activeDevice;
        private float smoothedInputLevel;
        private float smoothedOutputLevel;
        private int lastReadPosition;
        private bool isMonitoring;
        private bool isWaitingForPermission;
        private bool isSafetyReducingGain;
        private double estimatedMicrophoneLagMs;
        private string audioConfigurationNote = "Audio configuration not requested yet.";

        private bool vocalProcessorEnabled;
        private float processorInputDrive = 1f;
        private float processorGateThreshold = 0f;
        private float processorCompressorThreshold = 1f;
        private float processorCompressorRatio = 1f;
        private float processorMakeupGain = 1f;
        private float processorLimiterCeiling = 0.92f;
        private float processorEnvelope;
        private float processorGain = 1f;

        private enum PrototypePreset
        {
            DryReference,
            KtvRoom,
            StrongKtv,
            SafeSmallRoom,
        }

        private void Reset()
        {
            monitorSource = GetComponent<AudioSource>();
            reverbFilter = GetComponent<AudioReverbFilter>();
            echoFilter = GetComponent<AudioEchoFilter>();
            highPassFilter = GetComponent<AudioHighPassFilter>();
            lowPassFilter = GetComponent<AudioLowPassFilter>();
        }

        private void Awake()
        {
            EnsureSignalChain();
            EnsureAudioListener();
            ConfigureLowLatencyAudio();
            WireUi();
            currentPreset = initialPreset;
            ApplyPreset(currentPreset);
            ApplyMonitorVolume(monitorVolume);
            ApplySafetyState(safetyLimiterEnabled);
            RefreshUi();
        }

        private IEnumerator Start()
        {
            SetStatus($"{audioConfigurationNote} Start with Quest volume low, then raise gradually.");
            yield return RequestMicrophonePermissionIfNeeded();
            if (HasMicrophonePermission())
            {
                StartMonitoring();
            }
            RefreshUi();
        }

        private void Update()
        {
            UpdateLevels();
            UpdateEstimatedLag();
            ApplySafetyLimiter();
            RefreshMetrics();
        }

        private void OnDestroy()
        {
            StopMonitoring();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!vocalProcessorEnabled)
            {
                return;
            }

            for (var index = 0; index < data.Length; index += channels)
            {
                var sidechain = 0f;
                for (var channel = 0; channel < channels; channel += 1)
                {
                    var driven = data[index + channel] * processorInputDrive;
                    data[index + channel] = driven;
                    var absolute = Math.Abs(driven);
                    if (absolute > sidechain)
                    {
                        sidechain = absolute;
                    }
                }

                processorEnvelope = Math.Max(sidechain, processorEnvelope * 0.996f);
                var gateGain = processorEnvelope < processorGateThreshold ? 0.22f : 1f;
                var compressedGain = CalculateCompressorGain(processorEnvelope);
                processorGain = processorGain * 0.982f + compressedGain * 0.018f;
                var finalGain = gateGain * processorGain * processorMakeupGain;

                for (var channel = 0; channel < channels; channel += 1)
                {
                    data[index + channel] = SoftLimit(data[index + channel] * finalGain, processorLimiterCeiling);
                }
            }
        }

        public void StartMonitoring()
        {
            if (isMonitoring)
            {
                return;
            }

            if (!HasMicrophonePermission())
            {
                StartCoroutine(RequestPermissionThenStart());
                return;
            }

            if (Microphone.devices.Length == 0)
            {
                SetStatus("No microphone device is visible to Unity.");
                return;
            }

            activeDevice = Microphone.devices[0];
            microphoneClip = Microphone.Start(activeDevice, true, MicrophoneClipSeconds, TargetSampleRate);
            if (microphoneClip == null)
            {
                SetStatus("Microphone.Start returned no clip.");
                return;
            }

            StartCoroutine(StartSourceWhenMicrophoneIsPrimed());
        }

        public void StopMonitoring()
        {
            if (monitorSource != null)
            {
                monitorSource.Stop();
                monitorSource.clip = null;
            }

            if (!string.IsNullOrEmpty(activeDevice) && Microphone.IsRecording(activeDevice))
            {
                Microphone.End(activeDevice);
            }

            microphoneClip = null;
            activeDevice = null;
            lastReadPosition = 0;
            estimatedMicrophoneLagMs = 0;
            smoothedInputLevel = 0;
            smoothedOutputLevel = 0;
            isMonitoring = false;
            isSafetyReducingGain = false;
            SetStatus("Stopped.");
            RefreshUi();
        }

        public void SelectPreviousPreset()
        {
            var next = (int)currentPreset - 1;
            if (next < 0)
            {
                next = Enum.GetValues(typeof(PrototypePreset)).Length - 1;
            }

            ApplyPreset((PrototypePreset)next);
        }

        public void SelectNextPreset()
        {
            var count = Enum.GetValues(typeof(PrototypePreset)).Length;
            ApplyPreset((PrototypePreset)(((int)currentPreset + 1) % count));
        }

        private void EnsureSignalChain()
        {
            if (monitorSource == null)
            {
                monitorSource = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            }

            reverbFilter = reverbFilter == null
                ? gameObject.GetComponent<AudioReverbFilter>() ?? gameObject.AddComponent<AudioReverbFilter>()
                : reverbFilter;
            echoFilter = echoFilter == null
                ? gameObject.GetComponent<AudioEchoFilter>() ?? gameObject.AddComponent<AudioEchoFilter>()
                : echoFilter;
            highPassFilter = highPassFilter == null
                ? gameObject.GetComponent<AudioHighPassFilter>() ?? gameObject.AddComponent<AudioHighPassFilter>()
                : highPassFilter;
            lowPassFilter = lowPassFilter == null
                ? gameObject.GetComponent<AudioLowPassFilter>() ?? gameObject.AddComponent<AudioLowPassFilter>()
                : lowPassFilter;

            monitorSource.playOnAwake = false;
            monitorSource.loop = true;
            monitorSource.spatialBlend = 0f;
            monitorSource.priority = 0;
            monitorSource.bypassListenerEffects = false;
            monitorSource.bypassEffects = false;
            monitorSource.bypassReverbZones = true;
        }

        private void EnsureAudioListener()
        {
            if (FindObjectOfType<AudioListener>() != null)
            {
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.gameObject.AddComponent<AudioListener>();
                return;
            }

            var listenerObject = new GameObject("Fallback Audio Listener");
            listenerObject.AddComponent<AudioListener>();
        }

        private void ConfigureLowLatencyAudio()
        {
            if (!requestLowLatencyAudio)
            {
                audioConfigurationNote = "Using Unity default audio configuration.";
                return;
            }

            var configuration = AudioSettings.GetConfiguration();
            var previousSampleRate = configuration.sampleRate;
            var previousBufferSize = configuration.dspBufferSize;
            var nextBufferSize = previousBufferSize <= 0 ? 256 : Math.Min(previousBufferSize, 256);

            configuration.sampleRate = TargetSampleRate;
            configuration.dspBufferSize = nextBufferSize;

            var changed = previousSampleRate != configuration.sampleRate ||
                          previousBufferSize != configuration.dspBufferSize;
            var applied = !changed || AudioSettings.Reset(configuration);
            audioConfigurationNote = applied
                ? $"Audio config: {configuration.sampleRate} Hz / {configuration.dspBufferSize} samples."
                : $"Audio config request failed; using {previousSampleRate} Hz / {previousBufferSize} samples.";
        }

        private void WireUi()
        {
            startButton?.onClick.AddListener(StartMonitoring);
            stopButton?.onClick.AddListener(StopMonitoring);
            previousPresetButton?.onClick.AddListener(SelectPreviousPreset);
            nextPresetButton?.onClick.AddListener(SelectNextPreset);

            if (monitorVolumeSlider != null)
            {
                monitorVolumeSlider.minValue = MinimumMonitorVolume;
                monitorVolumeSlider.maxValue = MaximumMonitorVolume;
                monitorVolumeSlider.SetValueWithoutNotify(monitorVolume);
                monitorVolumeSlider.onValueChanged.AddListener(ApplyMonitorVolume);
            }

            if (monitorToggle != null)
            {
                monitorToggle.SetIsOnWithoutNotify(true);
                monitorToggle.onValueChanged.AddListener(SetMonitorOutputEnabled);
            }

            if (safetyToggle != null)
            {
                safetyToggle.SetIsOnWithoutNotify(safetyLimiterEnabled);
                safetyToggle.onValueChanged.AddListener(ApplySafetyState);
            }
        }

        private IEnumerator RequestPermissionThenStart()
        {
            yield return RequestMicrophonePermissionIfNeeded();
            if (HasMicrophonePermission())
            {
                StartMonitoring();
            }
        }

        private IEnumerator RequestMicrophonePermissionIfNeeded()
        {
            if (HasMicrophonePermission() || isWaitingForPermission)
            {
                yield break;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            isWaitingForPermission = true;
            Permission.RequestUserPermission(Permission.Microphone);
            SetStatus("Waiting for Android microphone permission...");
            var timeoutAt = Time.realtimeSinceStartup + 12f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            isWaitingForPermission = false;
            SetStatus(HasMicrophonePermission()
                ? "Microphone permission granted."
                : "Microphone permission was not granted.");
#else
            yield break;
#endif
        }

        private bool HasMicrophonePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
            return true;
#endif
        }

        private IEnumerator StartSourceWhenMicrophoneIsPrimed()
        {
            SetStatus($"Starting mic: {activeDevice}");
            var startDeadline = Time.realtimeSinceStartup + 2f;
            while (Microphone.GetPosition(activeDevice) <= 0 && Time.realtimeSinceStartup < startDeadline)
            {
                yield return null;
            }

            if (Microphone.GetPosition(activeDevice) <= 0)
            {
                StopMonitoring();
                SetStatus("Microphone did not produce samples.");
                yield break;
            }

            monitorSource.clip = microphoneClip;
            monitorSource.loop = true;
            monitorSource.timeSamples = 0;
            monitorSource.Play();
            isMonitoring = true;
            lastReadPosition = Microphone.GetPosition(activeDevice);
            SetStatus("Monitoring. Keep headset volume low while tuning.");
            RefreshUi();
        }

        private void ApplyPreset(PrototypePreset preset)
        {
            currentPreset = preset;

            switch (preset)
            {
                case PrototypePreset.DryReference:
                    ConfigureFilters(false, 80f, 18000f, AudioReverbPreset.Off, 0f, 1f);
                    ApplyMonitorVolume(0.1f);
                    break;
                case PrototypePreset.KtvRoom:
                    ConfigureFilters(true, 80f, 16000f, AudioReverbPreset.Hallway, 64f, 0.82f);
                    ApplyMonitorVolume(0.9f);
                    break;
                case PrototypePreset.StrongKtv:
                    ConfigureFilters(true, 75f, 15000f, AudioReverbPreset.Arena, 92f, 1.1f);
                    ApplyMonitorVolume(1.05f);
                    break;
                case PrototypePreset.SafeSmallRoom:
                    ConfigureFilters(true, 100f, 13500f, AudioReverbPreset.Room, 42f, 0.52f);
                    ApplyMonitorVolume(0.65f);
                    break;
            }

            RefreshUi();
        }

        private void ConfigureFilters(
            bool effectsEnabled,
            float highPassCutoff,
            float lowPassCutoff,
            AudioReverbPreset reverbPreset,
            float echoDelayMs,
            float echoWetMix)
        {
            ConfigureVocalProcessor(effectsEnabled);

            highPassFilter.enabled = effectsEnabled;
            highPassFilter.cutoffFrequency = highPassCutoff;
            highPassFilter.highpassResonanceQ = 1.05f;

            lowPassFilter.enabled = effectsEnabled;
            lowPassFilter.cutoffFrequency = lowPassCutoff;
            lowPassFilter.lowpassResonanceQ = 1f;

            reverbFilter.enabled = effectsEnabled && reverbPreset != AudioReverbPreset.Off;
            reverbFilter.reverbPreset = reverbPreset;
            reverbFilter.dryLevel = 0f;
            reverbFilter.room = currentPreset == PrototypePreset.StrongKtv ? 1200 : 650;
            reverbFilter.roomHF = currentPreset == PrototypePreset.StrongKtv ? -120 : -260;
            reverbFilter.decayTime = currentPreset == PrototypePreset.StrongKtv ? 3.2f : 2.35f;
            reverbFilter.decayHFRatio = 0.86f;
            reverbFilter.reflectionsLevel = currentPreset == PrototypePreset.StrongKtv ? 350f : 120f;
            reverbFilter.reflectionsDelay = 0.02f;
            reverbFilter.reverbLevel = currentPreset == PrototypePreset.StrongKtv ? 1150f : 760f;
            reverbFilter.reverbDelay = 0.045f;
            reverbFilter.diffusion = 96f;
            reverbFilter.density = 92f;
            reverbFilter.hfReference = 7000f;

            echoFilter.enabled = effectsEnabled && echoDelayMs > 0f;
            echoFilter.delay = echoDelayMs;
            echoFilter.decayRatio = currentPreset == PrototypePreset.StrongKtv ? 0.32f : 0.22f;
            echoFilter.wetMix = echoWetMix;
            echoFilter.dryMix = 1f;
        }

        private void ConfigureVocalProcessor(bool effectsEnabled)
        {
            vocalProcessorEnabled = effectsEnabled;

            switch (currentPreset)
            {
                case PrototypePreset.DryReference:
                    processorInputDrive = 1f;
                    processorGateThreshold = 0f;
                    processorCompressorThreshold = DbToLinear(-2f);
                    processorCompressorRatio = 1f;
                    processorMakeupGain = 1f;
                    processorLimiterCeiling = 0.95f;
                    break;
                case PrototypePreset.KtvRoom:
                    processorInputDrive = 5.8f;
                    processorGateThreshold = DbToLinear(-60f);
                    processorCompressorThreshold = DbToLinear(-28f);
                    processorCompressorRatio = 4.2f;
                    processorMakeupGain = 3.2f;
                    processorLimiterCeiling = 0.94f;
                    break;
                case PrototypePreset.StrongKtv:
                    processorInputDrive = 8.5f;
                    processorGateThreshold = DbToLinear(-62f);
                    processorCompressorThreshold = DbToLinear(-34f);
                    processorCompressorRatio = 6.5f;
                    processorMakeupGain = 4.4f;
                    processorLimiterCeiling = 0.96f;
                    break;
                case PrototypePreset.SafeSmallRoom:
                    processorInputDrive = 3.8f;
                    processorGateThreshold = DbToLinear(-54f);
                    processorCompressorThreshold = DbToLinear(-24f);
                    processorCompressorRatio = 3f;
                    processorMakeupGain = 2.2f;
                    processorLimiterCeiling = 0.88f;
                    break;
            }
        }

        private void ApplyMonitorVolume(float value)
        {
            monitorVolume = Mathf.Clamp(value, MinimumMonitorVolume, MaximumMonitorVolume);
            if (monitorSource != null)
            {
                monitorSource.volume = monitorVolume;
            }

            monitorVolumeSlider?.SetValueWithoutNotify(monitorVolume);
        }

        private void SetMonitorOutputEnabled(bool enabled)
        {
            if (monitorSource == null)
            {
                return;
            }

            monitorSource.mute = !enabled;
            SetStatus(enabled ? "Monitor output enabled." : "Monitor output muted.");
        }

        private void ApplySafetyState(bool enabled)
        {
            safetyLimiterEnabled = enabled;
            if (!enabled)
            {
                isSafetyReducingGain = false;
            }
        }

        private void UpdateLevels()
        {
            if (microphoneClip != null && !string.IsNullOrEmpty(activeDevice))
            {
                var position = Microphone.GetPosition(activeDevice);
                if (position >= SpectrumSize)
                {
                    microphoneClip.GetData(microphoneSamples, position - SpectrumSize);
                    smoothedInputLevel = Mathf.Lerp(smoothedInputLevel, CalculateRms(microphoneSamples) * 10f, 0.22f);
                }
            }
            else
            {
                smoothedInputLevel = Mathf.Lerp(smoothedInputLevel, 0f, 0.12f);
            }

            if (monitorSource != null && monitorSource.isPlaying)
            {
                monitorSource.GetOutputData(outputSamples, 0);
                smoothedOutputLevel = Mathf.Lerp(smoothedOutputLevel, CalculateRms(outputSamples) * 16f, 0.22f);
            }
            else
            {
                smoothedOutputLevel = Mathf.Lerp(smoothedOutputLevel, 0f, 0.12f);
            }

            inputLevelSlider?.SetValueWithoutNotify(Mathf.Clamp01(smoothedInputLevel));
            outputLevelSlider?.SetValueWithoutNotify(Mathf.Clamp01(smoothedOutputLevel));
        }

        private void UpdateEstimatedLag()
        {
            if (!isMonitoring || string.IsNullOrEmpty(activeDevice) || microphoneClip == null)
            {
                estimatedMicrophoneLagMs = 0;
                return;
            }

            var writePosition = Microphone.GetPosition(activeDevice);
            var readPosition = monitorSource.timeSamples;
            var totalSamples = microphoneClip.samples;
            var lagSamples = writePosition - readPosition;
            if (lagSamples < 0)
            {
                lagSamples += totalSamples;
            }

            estimatedMicrophoneLagMs = lagSamples * 1000.0 / microphoneClip.frequency;
            lastReadPosition = readPosition;
        }

        private void ApplySafetyLimiter()
        {
            if (!safetyLimiterEnabled || monitorSource == null)
            {
                return;
            }

            var hot = smoothedInputLevel > HotInputLevel || smoothedOutputLevel > HotInputLevel;
            if (hot)
            {
                isSafetyReducingGain = true;
                ApplyMonitorVolume(Mathf.Max(0.04f, monitorSource.volume * 0.92f));
                return;
            }

            if (isSafetyReducingGain && smoothedInputLevel < 0.45f && smoothedOutputLevel < 0.45f)
            {
                isSafetyReducingGain = false;
            }
        }

        private void RefreshUi()
        {
            if (presetText != null)
            {
                presetText.text = $"Preset: {FormatPresetName(currentPreset)}";
            }

            if (startButton != null)
            {
                startButton.interactable = !isMonitoring && !isWaitingForPermission;
            }

            if (stopButton != null)
            {
                stopButton.interactable = isMonitoring;
            }

            if (safetyToggle != null)
            {
                safetyToggle.SetIsOnWithoutNotify(safetyLimiterEnabled);
            }
        }

        private void RefreshMetrics()
        {
            if (metricsText == null)
            {
                return;
            }

            AudioSettings.GetDSPBufferSize(out var dspBufferLength, out var dspBufferCount);
            var dspLatencyMs = dspBufferLength * dspBufferCount * 1000.0 / AudioSettings.outputSampleRate;

            metricsBuilder.Clear();
            metricsBuilder.Append("Input ");
            metricsBuilder.Append(Mathf.RoundToInt(Mathf.Clamp01(smoothedInputLevel) * 100f));
            metricsBuilder.Append("%  Output ");
            metricsBuilder.Append(Mathf.RoundToInt(Mathf.Clamp01(smoothedOutputLevel) * 100f));
            metricsBuilder.Append("%\nMic lag estimate ");
            metricsBuilder.Append(Math.Round(estimatedMicrophoneLagMs, 1));
            metricsBuilder.Append(" ms  DSP buffer ");
            metricsBuilder.Append(Math.Round(dspLatencyMs, 1));
            metricsBuilder.Append(" ms\nSample rate ");
            metricsBuilder.Append(AudioSettings.outputSampleRate);
            metricsBuilder.Append(" Hz  Device ");
            metricsBuilder.Append(string.IsNullOrEmpty(activeDevice) ? "none" : activeDevice);
            metricsBuilder.Append("\nProcessor ");
            metricsBuilder.Append(vocalProcessorEnabled ? "gate/comp/limiter" : "bypassed");

            if (isSafetyReducingGain)
            {
                metricsBuilder.Append("\nSafety gain reduction active");
            }

            metricsText.text = metricsBuilder.ToString();
        }

        private static float CalculateRms(float[] samples)
        {
            double sum = 0;
            for (var i = 0; i < samples.Length; i += 1)
            {
                sum += samples[i] * samples[i];
            }

            return Mathf.Sqrt((float)(sum / samples.Length));
        }

        private float CalculateCompressorGain(float envelope)
        {
            if (processorCompressorRatio <= 1f || envelope <= processorCompressorThreshold)
            {
                return 1f;
            }

            var compressed = processorCompressorThreshold +
                             (envelope - processorCompressorThreshold) / processorCompressorRatio;
            return compressed / Math.Max(envelope, 0.000001f);
        }

        private static float SoftLimit(float value, float ceiling)
        {
            var sign = Math.Sign(value);
            var absolute = Math.Abs(value);
            if (absolute <= ceiling)
            {
                return value;
            }

            var headroom = Math.Max(0.001f, 0.999f - ceiling);
            var limited = ceiling + headroom * (float)Math.Tanh((absolute - ceiling) / headroom);
            return sign * Math.Min(0.999f, limited);
        }

        private static float DbToLinear(float decibels)
        {
            return (float)Math.Pow(10.0, decibels / 20.0);
        }

        private static string FormatPresetName(PrototypePreset preset)
        {
            return preset switch
            {
                PrototypePreset.DryReference => "Dry Reference",
                PrototypePreset.KtvRoom => "KTV Room",
                PrototypePreset.StrongKtv => "Strong KTV",
                PrototypePreset.SafeSmallRoom => "Safe Small Room",
                _ => preset.ToString(),
            };
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }

            Debug.Log($"[TsukiVox Audio Prototype] {message}");
        }
    }
}
