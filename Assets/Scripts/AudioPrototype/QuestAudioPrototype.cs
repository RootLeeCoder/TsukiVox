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
        private const float MaximumMonitorVolume = 1f;
        private const float DefaultMonitorVolume = 0.7f;
        private const float VoiceVolumeCurveExponent = 2.9050633f;
        private const float HotInputLevel = 0.95f;
        private const float UnityInputMeterGain = 36f;
        private const float UnityOutputMeterGain = 24f;
        private const float NativeMeterGain = 22f;
        private const float MeterDisplayCurve = 0.62f;
        private const float DefaultDistanceFullGainClearance = 0.12f;
        private const float DefaultDistanceCutoffClearance = 0.30f;
        private const bool DefaultDistanceMonitoringEnabled = true;
        private const float AudioPreferencesSaveDelay = 0.5f;
        private const string PresetPrefsKey = "TsukiVox.Audio.Preset.v1";
        private const string MonitorVolumePrefsKey = "TsukiVox.Audio.MonitorVolume.v1";
        private const string VoiceVolumePrefsKey = "TsukiVox.Audio.VoiceVolume.v2";
        private const string AmbiencePrefsKey = "TsukiVox.Audio.Ambience.v1";
        private const string EchoPrefsKey = "TsukiVox.Audio.Echo.v1";
        private const string DynamicsPrefsKey = "TsukiVox.Audio.Dynamics.v1";
        private const string DistanceMonitoringPrefsKey = "TsukiVox.Audio.DistanceMonitoring.v1";

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
        [SerializeField] private Toggle nativeToggle;
        [SerializeField] private Toggle safetyToggle;

        [Header("Runtime Defaults")]
        [SerializeField, Range(0f, MaximumMonitorVolume)] private float monitorVolume = DefaultMonitorVolume;
        [SerializeField] private bool safetyLimiterEnabled = true;
        [SerializeField] private bool preferNativeOboeBackend = false;
        [SerializeField] private bool requestLowLatencyAudio = true;
        [SerializeField] private PrototypePreset initialPreset = PrototypePreset.KtvRoom;

        [Header("User Voice Settings")]
        [SerializeField, Range(0f, 1f)] private float ambienceAmount = 0.55f;
        [SerializeField, Range(0f, 1f)] private float echoAmount = 0.3f;
        [SerializeField, Range(0f, 1f)] private float dynamicsAmount = 0.65f;

        [Header("Distance Monitoring")]
        [SerializeField] private bool distanceMonitoringEnabled = true;
        [SerializeField, Range(0.02f, 0.15f)] private float distanceFullGainClearance = DefaultDistanceFullGainClearance;
        [SerializeField, Range(0.15f, 0.6f)] private float distanceCutoffClearance = DefaultDistanceCutoffClearance;
        [SerializeField, Range(1f, 30f)] private float distanceAttackSmoothing = 12f;
        [SerializeField, Range(1f, 30f)] private float distanceReleaseSmoothing = 7f;

        private readonly float[] microphoneSamples = new float[SpectrumSize];
        private readonly float[] outputSamples = new float[SpectrumSize];
        private readonly StringBuilder metricsBuilder = new StringBuilder(512);

        private PrototypePreset currentPreset;
        private AudioClip microphoneClip;
        private string activeDevice;
        private float smoothedInputLevel;
        private float smoothedOutputLevel;
        private float safetyInputLevel;
        private float safetyOutputLevel;
        private int lastReadPosition;
        private bool isMonitoring;
        private bool isWaitingForPermission;
        private bool isSafetyReducingGain;
        private bool monitorOutputEnabled = true;
        private NativeAudioBackend activeBackend = NativeAudioBackend.UnityMicrophone;
        private TsukiVoxNativeStats nativeStats;
        private string backendNote = "Backend not started.";
        private double estimatedMicrophoneLagMs;
        private string audioConfigurationNote = "Audio configuration not requested yet.";
        private QuestHandheldPropsPrototype handheldPropsPrototype;
        private float microphoneSurfaceClearance = float.PositiveInfinity;
        private float distanceMonitorGain;
        private float safetyMonitorGain = 1f;
        private float appliedMonitorGain = -1f;
        private bool isMicrophoneDistanceTracked;
        private bool audioPreferencesDirty;
        private float audioPreferencesSaveAt;

        private bool vocalProcessorEnabled;
        private float processorInputDrive = 1f;
        private float processorGateThreshold = 0f;
        private float processorCompressorThreshold = 1f;
        private float processorCompressorRatio = 1f;
        private float processorMakeupGain = 1f;
        private float processorLimiterCeiling = 0.92f;
        private float processorMonitorPreGain = 1f;
        private float processorEnvelope;
        private float processorGain = 1f;

        private enum PrototypePreset
        {
            DryReference,
            KtvRoom,
            StrongKtv,
            SafeSmallRoom,
        }

        private readonly struct PresetSettings
        {
            public PresetSettings(float ambience, float echo, float dynamics)
            {
                Ambience = ambience;
                Echo = echo;
                Dynamics = dynamics;
            }

            public float Ambience { get; }
            public float Echo { get; }
            public float Dynamics { get; }
        }

        public bool IsMonitoring => isMonitoring;

        public bool IsWaitingForPermission => isWaitingForPermission;

        public bool IsSafetyReducingGain => isSafetyReducingGain;

        public string ActiveBackendName => activeBackend == NativeAudioBackend.NativeOboeDryMonitor ? "Native Oboe Dry" : "Unity Microphone";

        public string CurrentPresetName => FormatPresetName(currentPreset);

        public int CurrentPresetIndex => (int)currentPreset;

        public int PresetCount => Enum.GetValues(typeof(PrototypePreset)).Length;

        public float MonitorVolume => monitorVolume;

        public float MonitorVolumeMaximum => MaximumMonitorVolume;

        public float MonitorPreGain => CalculateVoicePreGain(monitorVolume);

        public float MonitorPreGainDecibels => LinearToDb(MonitorPreGain);

        public float AmbienceAmount => ambienceAmount;

        public float EchoAmount => echoAmount;

        public float DynamicsAmount => dynamicsAmount;

        public bool IsDistanceMonitoringEnabled => distanceMonitoringEnabled;

        public bool IsMicrophoneDistanceTracked => isMicrophoneDistanceTracked;

        public float MicrophoneSurfaceClearance => microphoneSurfaceClearance;

        public float DistanceMonitorGain => distanceMonitorGain;

        public float EffectiveMonitorVolume => MonitorPreGain * distanceMonitorGain * safetyMonitorGain;

        public bool HasCustomEffectSettings
        {
            get
            {
                var defaults = GetPresetSettings(currentPreset);
                return !Mathf.Approximately(ambienceAmount, defaults.Ambience) ||
                       !Mathf.Approximately(echoAmount, defaults.Echo) ||
                       !Mathf.Approximately(dynamicsAmount, defaults.Dynamics);
            }
        }

        public float InputLevel => Mathf.Clamp01(smoothedInputLevel);

        public float OutputLevel => Mathf.Clamp01(smoothedOutputLevel);

        public bool IsMonitorOutputEnabled => monitorOutputEnabled;

        public bool IsSafetyLimiterEnabled => safetyLimiterEnabled;

        public bool PrefersNativeOboeBackend => preferNativeOboeBackend;

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
            currentPreset = initialPreset;
            LoadAudioPreferences();
            ApplyCurrentEffectSettings();
            distanceMonitorGain = distanceMonitoringEnabled ? 0f : 1f;
            ApplyEffectiveMonitorGain();
            WireUi();
            EnsureQuestUiInteraction();
            QuestXrBootstrap.EnsureSceneBootstrap();
            QuestPlaylistPrototype.EnsureScenePrototype();
            QuestVideoScreenPrototype.EnsureScenePrototype();
            QuestAppShellPrototype.EnsureSceneShell();
            QuestKtvRoomPrototype.EnsureSceneRoom();
            QuestHandheldPropsPrototype.EnsureSceneProps();
            ApplySafetyState(safetyLimiterEnabled);
            ApplyNativePreference(preferNativeOboeBackend);
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
            UpdateDistanceMonitoring();
            UpdateLevels();
            UpdateEstimatedLag();
            ApplySafetyLimiter();
            ApplyEffectiveMonitorGain();
            SaveAudioPreferencesIfDue();
            RefreshMetrics();
        }

        private void OnDestroy()
        {
            StopMonitoring();
            SavePendingAudioPreferences();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SavePendingAudioPreferences();
            }
        }

        private void OnApplicationQuit()
        {
            SavePendingAudioPreferences();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!vocalProcessorEnabled)
            {
                for (var sampleIndex = 0; sampleIndex < data.Length; sampleIndex += 1)
                {
                    data[sampleIndex] = SoftLimit(
                        data[sampleIndex] * processorMonitorPreGain,
                        processorLimiterCeiling);
                }

                return;
            }

            for (var index = 0; index < data.Length; index += channels)
            {
                var sidechain = 0f;
                for (var channel = 0; channel < channels; channel += 1)
                {
                    var driven = data[index + channel] * processorInputDrive * processorMonitorPreGain;
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

            if (TryStartNativeMonitoring())
            {
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
                monitorSource.mute = false;
            }

            NativeOboeDryMonitor.Stop();
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
            safetyInputLevel = 0;
            safetyOutputLevel = 0;
            isMonitoring = false;
            isSafetyReducingGain = false;
            activeBackend = NativeAudioBackend.UnityMicrophone;
            nativeStats = default;
            backendNote = "Backend stopped.";
            SetStatus("Stopped.");
            RefreshUi();
        }

        public void ToggleMonitoring()
        {
            if (isMonitoring)
            {
                StopMonitoring();
            }
            else
            {
                StartMonitoring();
            }
        }

        public void SelectPreset(int presetIndex)
        {
            var count = Enum.GetValues(typeof(PrototypePreset)).Length;
            ApplyPreset((PrototypePreset)Mathf.Clamp(presetIndex, 0, count - 1));
        }

        public void SetMonitorVolume(float value)
        {
            ApplyMonitorVolume(value);
        }

        public void SetAmbienceAmount(float value)
        {
            ambienceAmount = Mathf.Clamp01(value);
            ApplyCurrentEffectSettings();
            QueueAudioPreferencesSave();
        }

        public void SetEchoAmount(float value)
        {
            echoAmount = Mathf.Clamp01(value);
            ApplyCurrentEffectSettings();
            QueueAudioPreferencesSave();
        }

        public void SetDynamicsAmount(float value)
        {
            dynamicsAmount = Mathf.Clamp01(value);
            ApplyCurrentEffectSettings();
            QueueAudioPreferencesSave();
        }

        public void SetDistanceMonitoringEnabled(bool enabled)
        {
            distanceMonitoringEnabled = enabled;
            if (!enabled)
            {
                isMicrophoneDistanceTracked = false;
                microphoneSurfaceClearance = float.PositiveInfinity;
            }

            QueueAudioPreferencesSave();
        }

        public void SetMonitorOutput(bool enabled)
        {
            SetMonitorOutputEnabled(enabled);
        }

        public void SetPreferNativeBackend(bool enabled)
        {
            ApplyNativePreference(enabled);
        }

        public void SetSafetyLimiterEnabled(bool enabled)
        {
            ApplySafetyState(enabled);
        }

        public void RestoreDefaultSettings()
        {
            currentPreset = initialPreset;
            var defaults = GetPresetSettings(currentPreset);
            monitorVolume = DefaultMonitorVolume;
            ambienceAmount = defaults.Ambience;
            echoAmount = defaults.Echo;
            dynamicsAmount = defaults.Dynamics;
            distanceMonitoringEnabled = DefaultDistanceMonitoringEnabled;
            isMicrophoneDistanceTracked = false;
            microphoneSurfaceClearance = float.PositiveInfinity;
            distanceMonitorGain = 0f;
            safetyMonitorGain = 1f;

            ApplyCurrentEffectSettings();
            SetMonitorOutputEnabled(true);
            ApplySafetyState(true);
            if (preferNativeOboeBackend)
            {
                ApplyNativePreference(false);
            }
            else
            {
                preferNativeOboeBackend = false;
                nativeToggle?.SetIsOnWithoutNotify(false);
            }

            ApplyEffectiveMonitorGain();
            monitorVolumeSlider?.SetValueWithoutNotify(monitorVolume);
            QueueAudioPreferencesSave();
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
            if (FindAnyObjectByType<AudioListener>() != null)
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

        private void EnsureQuestUiInteraction()
        {
            QuestUiPointer.EnsureScenePointer();
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
                monitorToggle.SetIsOnWithoutNotify(monitorOutputEnabled);
                monitorToggle.onValueChanged.AddListener(SetMonitorOutputEnabled);
            }

            if (nativeToggle != null)
            {
                nativeToggle.SetIsOnWithoutNotify(preferNativeOboeBackend);
                nativeToggle.onValueChanged.AddListener(ApplyNativePreference);
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
            monitorSource.mute = !monitorOutputEnabled;
            monitorSource.timeSamples = 0;
            monitorSource.Play();
            isMonitoring = true;
            activeBackend = NativeAudioBackend.UnityMicrophone;
            backendNote = "Unity Microphone backend.";
            lastReadPosition = Microphone.GetPosition(activeDevice);
            SetStatus(vocalProcessorEnabled
                ? "Unity monitoring with KTV reverb/echo active. Keep headset volume low while tuning."
                : "Unity dry monitoring. Keep headset volume low while tuning.");
            RefreshUi();
        }

        private bool TryStartNativeMonitoring()
        {
            if (!preferNativeOboeBackend || !NativeOboeDryMonitor.IsAvailable)
            {
                return false;
            }

            if (NativeOboeDryMonitor.TryStart(EffectiveMonitorVolume, !monitorOutputEnabled, out var error))
            {
                if (monitorSource != null)
                {
                    monitorSource.Stop();
                    monitorSource.clip = null;
                }

                isMonitoring = true;
                activeBackend = NativeAudioBackend.NativeOboeDryMonitor;
                backendNote = "Native Oboe Dry backend. Unity reverb and echo are bypassed.";
                estimatedMicrophoneLagMs = 0;
                SetStatus("Native Oboe dry monitoring. Use Unity backend for audible KTV reverb.");
                RefreshUi();
                return true;
            }

            backendNote = $"Native Oboe failed: {error}";
            SetStatus($"{backendNote} Falling back to Unity microphone.");
            return false;
        }

        private void ApplyPreset(PrototypePreset preset)
        {
            currentPreset = preset;
            var settings = GetPresetSettings(preset);
            ambienceAmount = settings.Ambience;
            echoAmount = settings.Echo;
            dynamicsAmount = settings.Dynamics;
            ApplyCurrentEffectSettings();
            ApplyEffectiveMonitorGain();
            monitorVolumeSlider?.SetValueWithoutNotify(monitorVolume);
            QueueAudioPreferencesSave();
            RefreshUi();
        }

        private void ApplyCurrentEffectSettings()
        {
            ConfigureVocalProcessor();

            var filtersEnabled = ambienceAmount > 0.001f || echoAmount > 0.001f || dynamicsAmount > 0.001f;
            highPassFilter.enabled = filtersEnabled;
            highPassFilter.cutoffFrequency = Mathf.Lerp(70f, 110f, dynamicsAmount);
            highPassFilter.highpassResonanceQ = 1.05f;

            lowPassFilter.enabled = filtersEnabled;
            lowPassFilter.cutoffFrequency = Mathf.Lerp(18000f, 14500f, ambienceAmount);
            lowPassFilter.lowpassResonanceQ = 1f;

            reverbFilter.enabled = ambienceAmount > 0.001f;
            reverbFilter.reverbPreset = ambienceAmount > 0.001f
                ? AudioReverbPreset.User
                : AudioReverbPreset.Off;
            reverbFilter.dryLevel = 0f;
            reverbFilter.room = Mathf.Lerp(-1800f, -280f, ambienceAmount);
            reverbFilter.roomHF = Mathf.Lerp(-2200f, -340f, ambienceAmount);
            reverbFilter.decayTime = Mathf.Lerp(0.75f, 3.6f, ambienceAmount);
            reverbFilter.decayHFRatio = Mathf.Lerp(0.62f, 0.9f, ambienceAmount);
            reverbFilter.reflectionsLevel = Mathf.Lerp(-1600f, 280f, ambienceAmount);
            reverbFilter.reflectionsDelay = Mathf.Lerp(0.008f, 0.035f, ambienceAmount);
            reverbFilter.reverbLevel = Mathf.Lerp(-1400f, 980f, ambienceAmount);
            reverbFilter.reverbDelay = Mathf.Lerp(0.012f, 0.055f, ambienceAmount);
            reverbFilter.diffusion = Mathf.Lerp(72f, 98f, ambienceAmount);
            reverbFilter.density = Mathf.Lerp(68f, 96f, ambienceAmount);
            reverbFilter.hfReference = 7000f;

            echoFilter.enabled = echoAmount > 0.001f;
            echoFilter.delay = Mathf.Lerp(48f, 125f, echoAmount);
            echoFilter.decayRatio = Mathf.Lerp(0.12f, 0.38f, echoAmount);
            echoFilter.wetMix = Mathf.Lerp(0.04f, 0.58f, echoAmount);
            echoFilter.dryMix = 1f;
        }

        private void ConfigureVocalProcessor()
        {
            vocalProcessorEnabled = dynamicsAmount > 0.001f;
            processorMonitorPreGain = CalculateVoicePreGain(monitorVolume);
            processorInputDrive = Mathf.Lerp(1f, 7.2f, dynamicsAmount);
            processorGateThreshold = DbToLinear(Mathf.Lerp(-68f, -54f, dynamicsAmount));
            processorCompressorThreshold = DbToLinear(Mathf.Lerp(-10f, -31f, dynamicsAmount));
            processorCompressorRatio = Mathf.Lerp(1.1f, 6.2f, dynamicsAmount);
            processorMakeupGain = Mathf.Lerp(1f, 3.8f, dynamicsAmount);
            processorLimiterCeiling = Mathf.Lerp(0.96f, 0.9f, dynamicsAmount);
        }

        private void ApplyMonitorVolume(float value)
        {
            monitorVolume = Mathf.Clamp01(value);
            processorMonitorPreGain = CalculateVoicePreGain(monitorVolume);
            ApplyEffectiveMonitorGain();
            monitorVolumeSlider?.SetValueWithoutNotify(monitorVolume);
            QueueAudioPreferencesSave();
        }

        private void SetMonitorOutputEnabled(bool enabled)
        {
            monitorOutputEnabled = enabled;
            if (monitorSource == null)
            {
                NativeOboeDryMonitor.SetMuted(!monitorOutputEnabled);
                SetStatus(enabled ? "Monitor output enabled." : "Monitor output muted.");
                return;
            }

            monitorSource.mute = !enabled;
            if (activeBackend == NativeAudioBackend.NativeOboeDryMonitor)
            {
                NativeOboeDryMonitor.SetMuted(!enabled);
            }

            SetStatus(enabled ? "Monitor output enabled." : "Monitor output muted.");
        }

        private void ApplyNativePreference(bool enabled)
        {
            preferNativeOboeBackend = enabled;
            nativeToggle?.SetIsOnWithoutNotify(preferNativeOboeBackend);

            if (!isMonitoring)
            {
                SetStatus(enabled
                    ? "Native Oboe selected: dry, lowest-latency AB path without Unity effects."
                    : "Unity microphone selected: KTV preset effects are active.");
                return;
            }

            StopMonitoring();
            StartMonitoring();
        }

        private void ApplySafetyState(bool enabled)
        {
            safetyLimiterEnabled = enabled;
            if (!enabled)
            {
                safetyMonitorGain = 1f;
                isSafetyReducingGain = false;
                ApplyEffectiveMonitorGain();
            }
        }

        private void UpdateDistanceMonitoring()
        {
            var targetGain = 1f;
            isMicrophoneDistanceTracked = false;
            microphoneSurfaceClearance = float.PositiveInfinity;

            if (distanceMonitoringEnabled)
            {
                if (handheldPropsPrototype == null)
                {
                    handheldPropsPrototype = FindAnyObjectByType<QuestHandheldPropsPrototype>();
                }

                if (handheldPropsPrototype != null && handheldPropsPrototype.IsMicrophoneTracked)
                {
                    var clearance = handheldPropsPrototype.MicrophoneFaceSurfaceClearance;
                    if (!float.IsNaN(clearance) && !float.IsInfinity(clearance))
                    {
                        microphoneSurfaceClearance = clearance;
                        isMicrophoneDistanceTracked = true;
                        targetGain = CalculateDistanceMonitorGain(
                            clearance,
                            distanceFullGainClearance,
                            distanceCutoffClearance);
                    }
                    else
                    {
                        targetGain = 0f;
                    }
                }
                else
                {
                    targetGain = 0f;
                }
            }

            var smoothingRate = targetGain > distanceMonitorGain
                ? distanceAttackSmoothing
                : distanceReleaseSmoothing;
            var smoothing = 1f - Mathf.Exp(-smoothingRate * Time.unscaledDeltaTime);
            distanceMonitorGain = Mathf.Lerp(distanceMonitorGain, targetGain, smoothing);
            if (distanceMonitorGain < 0.001f)
            {
                distanceMonitorGain = 0f;
            }
            else if (distanceMonitorGain > 0.999f)
            {
                distanceMonitorGain = 1f;
            }
        }

        public static float CalculateDistanceMonitorGain(
            float surfaceClearance,
            float fullGainClearance = DefaultDistanceFullGainClearance,
            float cutoffClearance = DefaultDistanceCutoffClearance)
        {
            if (float.IsNaN(surfaceClearance) || float.IsInfinity(surfaceClearance))
            {
                return 0f;
            }

            var near = Mathf.Max(0f, fullGainClearance);
            var far = Mathf.Max(near + 0.001f, cutoffClearance);
            var linearGain = Mathf.InverseLerp(far, near, Mathf.Max(0f, surfaceClearance));
            return linearGain * linearGain * (3f - 2f * linearGain);
        }

        private void ApplyEffectiveMonitorGain()
        {
            var effectiveGain = Mathf.Clamp(
                EffectiveMonitorVolume,
                MinimumMonitorVolume,
                CalculateVoicePreGain(MaximumMonitorVolume));
            if (Mathf.Abs(effectiveGain - appliedMonitorGain) < 0.001f)
            {
                return;
            }

            appliedMonitorGain = effectiveGain;
            if (monitorSource != null)
            {
                monitorSource.volume = Mathf.Clamp01(distanceMonitorGain * safetyMonitorGain);
            }

            if (activeBackend == NativeAudioBackend.NativeOboeDryMonitor)
            {
                NativeOboeDryMonitor.SetGain(effectiveGain);
            }
        }

        private void LoadAudioPreferences()
        {
            var presetCount = Enum.GetValues(typeof(PrototypePreset)).Length;
            var savedPreset = PlayerPrefs.GetInt(PresetPrefsKey, (int)initialPreset);
            currentPreset = (PrototypePreset)Mathf.Clamp(savedPreset, 0, presetCount - 1);

            var defaults = GetPresetSettings(currentPreset);
            if (PlayerPrefs.HasKey(VoiceVolumePrefsKey))
            {
                monitorVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VoiceVolumePrefsKey, DefaultMonitorVolume));
            }
            else if (PlayerPrefs.HasKey(MonitorVolumePrefsKey))
            {
                monitorVolume = MigrateLegacyMonitorVolume(PlayerPrefs.GetFloat(MonitorVolumePrefsKey));
            }
            else
            {
                monitorVolume = DefaultMonitorVolume;
            }
            ambienceAmount = Mathf.Clamp01(PlayerPrefs.GetFloat(AmbiencePrefsKey, defaults.Ambience));
            echoAmount = Mathf.Clamp01(PlayerPrefs.GetFloat(EchoPrefsKey, defaults.Echo));
            dynamicsAmount = Mathf.Clamp01(PlayerPrefs.GetFloat(DynamicsPrefsKey, defaults.Dynamics));
            distanceMonitoringEnabled = PlayerPrefs.GetInt(
                DistanceMonitoringPrefsKey,
                distanceMonitoringEnabled ? 1 : 0) != 0;
        }

        private void QueueAudioPreferencesSave()
        {
            PlayerPrefs.SetInt(PresetPrefsKey, (int)currentPreset);
            PlayerPrefs.SetFloat(VoiceVolumePrefsKey, monitorVolume);
            PlayerPrefs.SetFloat(AmbiencePrefsKey, ambienceAmount);
            PlayerPrefs.SetFloat(EchoPrefsKey, echoAmount);
            PlayerPrefs.SetFloat(DynamicsPrefsKey, dynamicsAmount);
            PlayerPrefs.SetInt(DistanceMonitoringPrefsKey, distanceMonitoringEnabled ? 1 : 0);
            audioPreferencesDirty = true;
            audioPreferencesSaveAt = Time.unscaledTime + AudioPreferencesSaveDelay;
        }

        private void SaveAudioPreferencesIfDue()
        {
            if (audioPreferencesDirty && Time.unscaledTime >= audioPreferencesSaveAt)
            {
                SavePendingAudioPreferences();
            }
        }

        private void SavePendingAudioPreferences()
        {
            if (!audioPreferencesDirty)
            {
                return;
            }

            PlayerPrefs.Save();
            audioPreferencesDirty = false;
        }

        private void UpdateLevels()
        {
            if (microphoneClip != null && !string.IsNullOrEmpty(activeDevice))
            {
                var position = Microphone.GetPosition(activeDevice);
                if (position >= SpectrumSize)
                {
                    microphoneClip.GetData(microphoneSamples, position - SpectrumSize);
                    var inputLevel = CalculateRms(microphoneSamples);
                    safetyInputLevel = Mathf.Lerp(safetyInputLevel, inputLevel, 0.22f);
                    smoothedInputLevel = Mathf.Lerp(
                        smoothedInputLevel,
                        ToMeterDisplayLevel(inputLevel, UnityInputMeterGain),
                        0.22f);
                }
            }
            else
            {
                var inputLevel = GetNativeInputLevel();
                safetyInputLevel = Mathf.Lerp(safetyInputLevel, inputLevel, 0.22f);
                smoothedInputLevel = Mathf.Lerp(
                    smoothedInputLevel,
                    ToMeterDisplayLevel(inputLevel, NativeMeterGain),
                    0.22f);
            }

            if (activeBackend == NativeAudioBackend.NativeOboeDryMonitor)
            {
                var outputLevel = GetNativeOutputLevel();
                safetyOutputLevel = Mathf.Lerp(safetyOutputLevel, outputLevel, 0.22f);
                smoothedOutputLevel = Mathf.Lerp(
                    smoothedOutputLevel,
                    ToMeterDisplayLevel(outputLevel, NativeMeterGain),
                    0.22f);
            }
            else if (monitorSource != null && monitorSource.isPlaying)
            {
                monitorSource.GetOutputData(outputSamples, 0);
                var outputLevel = CalculateRms(outputSamples);
                safetyOutputLevel = Mathf.Lerp(safetyOutputLevel, outputLevel, 0.22f);
                smoothedOutputLevel = Mathf.Lerp(
                    smoothedOutputLevel,
                    ToMeterDisplayLevel(outputLevel, UnityOutputMeterGain),
                    0.22f);
            }
            else
            {
                safetyOutputLevel = Mathf.Lerp(safetyOutputLevel, 0f, 0.12f);
                smoothedOutputLevel = Mathf.Lerp(smoothedOutputLevel, 0f, 0.12f);
            }

            inputLevelSlider?.SetValueWithoutNotify(Mathf.Clamp01(smoothedInputLevel));
            outputLevelSlider?.SetValueWithoutNotify(Mathf.Clamp01(smoothedOutputLevel));
        }

        private void UpdateEstimatedLag()
        {
            if (activeBackend == NativeAudioBackend.NativeOboeDryMonitor)
            {
                estimatedMicrophoneLagMs = 0;
                NativeOboeDryMonitor.TryGetStats(out nativeStats);
                return;
            }

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
            if (!safetyLimiterEnabled)
            {
                safetyMonitorGain = 1f;
                isSafetyReducingGain = false;
                return;
            }

            var hot = safetyInputLevel > HotInputLevel || safetyOutputLevel > HotInputLevel;
            if (hot)
            {
                safetyMonitorGain = Mathf.MoveTowards(
                    safetyMonitorGain,
                    0.12f,
                    Time.unscaledDeltaTime * 2.8f);
            }
            else if (safetyInputLevel < 0.45f && safetyOutputLevel < 0.45f)
            {
                safetyMonitorGain = Mathf.MoveTowards(
                    safetyMonitorGain,
                    1f,
                    Time.unscaledDeltaTime * 0.3f);
            }

            isSafetyReducingGain = safetyMonitorGain < 0.999f;
        }

        private void RefreshUi()
        {
            if (presetText != null)
            {
                presetText.text = $"Preset: {FormatPresetName(currentPreset)}";
            }

            monitorVolumeSlider?.SetValueWithoutNotify(monitorVolume);

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

            if (nativeToggle != null)
            {
                nativeToggle.SetIsOnWithoutNotify(preferNativeOboeBackend);
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
            metricsBuilder.Append(activeBackend == NativeAudioBackend.NativeOboeDryMonitor
                ? "native dry soft limiter"
                : (vocalProcessorEnabled ? "gate/comp/limiter + reverb/echo" : "bypassed"));
            metricsBuilder.Append("\nDistance monitor ");
            metricsBuilder.Append(distanceMonitoringEnabled ? "enabled" : "disabled");
            metricsBuilder.Append("  tracked ");
            metricsBuilder.Append(isMicrophoneDistanceTracked);
            metricsBuilder.Append("  gain ");
            metricsBuilder.Append(distanceMonitorGain.ToString("0.00"));
            if (isMicrophoneDistanceTracked)
            {
                metricsBuilder.Append("  clearance ");
                metricsBuilder.Append((microphoneSurfaceClearance * 100f).ToString("0.0"));
                metricsBuilder.Append(" cm");
            }
            metricsBuilder.Append("\nBackend ");
            metricsBuilder.Append(activeBackend == NativeAudioBackend.NativeOboeDryMonitor ? "Native Oboe Dry" : "Unity Microphone");

            if (activeBackend == NativeAudioBackend.NativeOboeDryMonitor)
            {
                AppendNativeMetrics();
            }
            else if (!string.IsNullOrEmpty(backendNote))
            {
                metricsBuilder.Append("\n");
                metricsBuilder.Append(backendNote);
            }

            if (isSafetyReducingGain)
            {
                metricsBuilder.Append("\nSafety gain reduction active");
            }

            metricsText.text = metricsBuilder.ToString();
        }

        private void AppendNativeMetrics()
        {
            NativeOboeDryMonitor.TryGetStats(out nativeStats);
            metricsBuilder.Append("  ");
            metricsBuilder.Append(nativeStats.IsExclusive ? "Exclusive" : "Shared");
            metricsBuilder.Append("\nBurst ");
            metricsBuilder.Append(nativeStats.framesPerBurst);
            metricsBuilder.Append("  Buffers in/out ");
            metricsBuilder.Append(nativeStats.inputBufferFrames);
            metricsBuilder.Append("/");
            metricsBuilder.Append(nativeStats.outputBufferFrames);
            metricsBuilder.Append("  XRuns ");
            metricsBuilder.Append(nativeStats.inputXRunCount);
            metricsBuilder.Append("/");
            metricsBuilder.Append(nativeStats.outputXRunCount);
            metricsBuilder.Append("\nCallbacks ");
            metricsBuilder.Append(nativeStats.callbackCount);
        }

        private float GetNativeInputLevel()
        {
            return activeBackend == NativeAudioBackend.NativeOboeDryMonitor &&
                   NativeOboeDryMonitor.TryGetStats(out nativeStats)
                ? Mathf.Clamp01(nativeStats.inputLevel)
                : 0f;
        }

        private float GetNativeOutputLevel()
        {
            return activeBackend == NativeAudioBackend.NativeOboeDryMonitor &&
                   NativeOboeDryMonitor.TryGetStats(out nativeStats)
                ? Mathf.Clamp01(nativeStats.outputLevel)
                : 0f;
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

        private static float ToMeterDisplayLevel(float linearLevel, float displayGain)
        {
            var scaled = Mathf.Clamp01(Mathf.Max(0f, linearLevel) * displayGain);
            return Mathf.Pow(scaled, MeterDisplayCurve);
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

        private static float LinearToDb(float linear)
        {
            return linear <= 0.000001f ? -80f : 20f * Mathf.Log10(linear);
        }

        public static float CalculateVoicePreGain(float normalizedVolume)
        {
            if (normalizedVolume <= 0f)
            {
                return 0f;
            }

            // 70% preserves unity gain; 100% reaches +9 dB without relying on AudioSource.volume > 1.
            return Mathf.Pow(Mathf.Clamp01(normalizedVolume) / DefaultMonitorVolume, VoiceVolumeCurveExponent);
        }

        private static float MigrateLegacyMonitorVolume(float legacyGain)
        {
            if (legacyGain <= 0f)
            {
                return 0f;
            }

            var normalized = DefaultMonitorVolume *
                             Mathf.Pow(Mathf.Clamp(legacyGain, 0f, 1.4f), 1f / VoiceVolumeCurveExponent);
            return Mathf.Clamp01(normalized);
        }

        private static PresetSettings GetPresetSettings(PrototypePreset preset)
        {
            return preset switch
            {
                PrototypePreset.DryReference => new PresetSettings(0f, 0f, 0f),
                PrototypePreset.KtvRoom => new PresetSettings(0.55f, 0.3f, 0.65f),
                PrototypePreset.StrongKtv => new PresetSettings(0.86f, 0.62f, 0.9f),
                PrototypePreset.SafeSmallRoom => new PresetSettings(0.34f, 0.12f, 0.48f),
                _ => new PresetSettings(0.55f, 0.3f, 0.65f),
            };
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
