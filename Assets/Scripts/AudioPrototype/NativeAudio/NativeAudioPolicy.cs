using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TsukiVox.AudioPrototype
{
    public static class NativeAudioAbi
    {
        public const int ApiVersion = 5;
        public const int ParameterSize = 96;
        public const int StatsSize = 288;

        public static bool IsCompatible(int version, int parameters, int stats)
        {
            return version == ApiVersion && parameters == ParameterSize && stats == StatsSize;
        }
    }

    public enum MonitorMode
    {
        UnitySpatialSpeakers,
        OboeLowLatency,
    }

    public enum NativeAudioBackend
    {
        UnityMicrophone,
        NativeOboeLowLatency,
    }

    public enum NativeInputProcessingMode
    {
        Natural = 0,
        Generic = 1,
        VoiceRecognition = 6,
        VoicePerformance = 10,
    }

    public enum NativeDspPreset
    {
        DryReference,
        KtvRoom,
        StrongKtv,
        SafeSmallRoom,
    }

    public enum NativeMonitorProfile
    {
        Production,
        BaselineGenericCushion1,
        VoiceRecognitionCushion0,
        VoicePerformanceCushion0,
        VoicePerformanceCushion1,
        VoicePerformanceCushion0Output1,
    }

    public enum NativeFallbackStage
    {
        None,
        SafeExclusive,
        SafeShared,
        GenericSafeShared,
        RuntimeSafe,
        ExperimentShared,
        Shared,
        GenericInput,
    }

    public enum NativeStabilityAction
    {
        None,
        RestartSafe,
        FallbackUnity,
    }

    public readonly struct NativeMonitorProfileConfiguration
    {
        public NativeMonitorProfileConfiguration(
            NativeMonitorProfile resolvedProfile,
            int inputPreset,
            int inputBurstsCushion,
            int inputBufferBursts,
            int outputBufferBursts)
        {
            ResolvedProfile = resolvedProfile;
            InputPreset = inputPreset;
            InputBurstsCushion = inputBurstsCushion;
            InputBufferBursts = inputBufferBursts;
            OutputBufferBursts = outputBufferBursts;
        }

        public NativeMonitorProfile ResolvedProfile { get; }
        public int InputPreset { get; }
        public int InputBurstsCushion { get; }
        public int InputBufferBursts { get; }
        public int OutputBufferBursts { get; }
    }

    public static class NativeMonitorProfiles
    {
        public const int GenericInputPreset = 1;
        public const int VoiceRecognitionInputPreset = 6;
        public const int UnprocessedInputPreset = 9;
        public const int VoicePerformanceInputPreset = 10;

        public static NativeMonitorProfile Normalize(int profile)
        {
            return Enum.IsDefined(typeof(NativeMonitorProfile), profile)
                ? (NativeMonitorProfile)profile
                : NativeMonitorProfile.Production;
        }

        public static NativeMonitorProfileConfiguration Resolve(NativeMonitorProfile profile)
        {
            return Normalize((int)profile) switch
            {
                NativeMonitorProfile.BaselineGenericCushion1 =>
                    new NativeMonitorProfileConfiguration(profile, GenericInputPreset, 1, 2, 2),
                NativeMonitorProfile.VoiceRecognitionCushion0 =>
                    new NativeMonitorProfileConfiguration(profile, VoiceRecognitionInputPreset, 0, 2, 2),
                NativeMonitorProfile.VoicePerformanceCushion1 =>
                    new NativeMonitorProfileConfiguration(profile, VoicePerformanceInputPreset, 1, 2, 2),
                NativeMonitorProfile.VoicePerformanceCushion0Output1 =>
                    new NativeMonitorProfileConfiguration(profile, VoicePerformanceInputPreset, 0, 2, 1),
                NativeMonitorProfile.Production =>
                    new NativeMonitorProfileConfiguration(
                        NativeMonitorProfile.Production,
                        UnprocessedInputPreset,
                        0,
                        2,
                        2),
                NativeMonitorProfile.VoicePerformanceCushion0 =>
                    new NativeMonitorProfileConfiguration(
                        NativeMonitorProfile.VoicePerformanceCushion0,
                        VoicePerformanceInputPreset,
                        0,
                        2,
                        2),
                _ => Resolve(NativeMonitorProfile.Production),
            };
        }

        public static bool IsExperimental(NativeMonitorProfile profile)
        {
            return Normalize((int)profile) != NativeMonitorProfile.Production;
        }

        public static string GetDisplayName(NativeMonitorProfile profile)
        {
            return Normalize((int)profile) switch
            {
                NativeMonitorProfile.Production => "主配置 · I2 · C0 · O2",
                NativeMonitorProfile.BaselineGenericCushion1 => "稳定配置 · I2 · C1 · O2",
                NativeMonitorProfile.VoiceRecognitionCushion0 => "Recognition · C0 · O2",
                NativeMonitorProfile.VoicePerformanceCushion0 => "Performance · C0 · O2",
                NativeMonitorProfile.VoicePerformanceCushion1 => "Performance · C1 · O2",
                NativeMonitorProfile.VoicePerformanceCushion0Output1 => "实验配置 · I2 · C0 · O1",
                _ => "Production",
            };
        }
    }

    public static class NativeInputProcessing
    {
        public static NativeInputProcessingMode Normalize(int mode)
        {
            return Enum.IsDefined(typeof(NativeInputProcessingMode), mode)
                ? (NativeInputProcessingMode)mode : NativeInputProcessingMode.Natural;
        }

        public static int ResolvePreset(NativeInputProcessingMode mode, bool unprocessedSupported = true)
        {
            var normalized = Normalize((int)mode);
            return normalized == NativeInputProcessingMode.Natural
                ? (unprocessedSupported ? NativeMonitorProfiles.UnprocessedInputPreset : NativeMonitorProfiles.GenericInputPreset)
                : (int)normalized;
        }
    }

    public static class AudioModeMigration
    {
        public const string ModeKey = "TsukiVox.Audio.MonitorMode";
        public const string MigrationKey = "TsukiVox.Audio.LowLatencyRewrite.v1";

        // Int-only persistence API makes it impossible to rewrite float tuning preferences.
        public static void Apply(Func<string, int, int> getInt, Action<string, int> setInt)
        {
            if (getInt(MigrationKey, 0) != 0) return;
            var storedMode = getInt(ModeKey, -1);
            setInt(ModeKey, (int)AudioMonitorPreference.Resolve(storedMode != -1, storedMode));
            setInt(MigrationKey, 1);
        }
    }

    public static class AudioMonitorPreference
    {
        public const MonitorMode DefaultMode = MonitorMode.UnitySpatialSpeakers;

        public static MonitorMode Resolve(bool hasStoredValue, int storedValue)
        {
            if (!hasStoredValue || !Enum.IsDefined(typeof(MonitorMode), storedValue))
            {
                return DefaultMode;
            }

            return (MonitorMode)storedValue;
        }
    }

    public static class NativeFeedbackSafetyTuning
    {
        // Keep acoustic loop gain below unity on Quest speakers. The previous
        // unity default made the low-latency path prone to runaway feedback.
        public const float DefaultOutputGain = 0.72f;
        public const float MaximumOutputGain = 1f;
        public const float MaximumInputDrive = 3.25f;
        public const float MinimumHighPassHz = 70f;
        public const float MaximumHighPassHz = 100f;

        public static float CalculateOutputGain(float requestedPreGain)
        {
            if (float.IsNaN(requestedPreGain) || float.IsInfinity(requestedPreGain) || requestedPreGain <= 0f)
            {
                return 0f;
            }

            return Math.Min(
                MaximumOutputGain,
                DefaultOutputGain * (float)Math.Sqrt(requestedPreGain));
        }

        public static float CalculateInputDrive(float dynamics)
        {
            return Lerp(1f, MaximumInputDrive, Clamp01(dynamics));
        }

        public static float CalculateHighPassHz(float dynamics)
        {
            return Lerp(MinimumHighPassHz, MaximumHighPassHz, Clamp01(dynamics));
        }

        private static float Clamp01(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Math.Max(0f, Math.Min(1f, value));
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }

    public readonly struct NativePresetDefaults
    {
        public NativePresetDefaults(float ambience, float echo, float dynamics)
        {
            Ambience = ambience;
            Echo = echo;
            Dynamics = dynamics;
        }
        public float Ambience { get; }
        public float Echo { get; }
        public float Dynamics { get; }
        public static NativePresetDefaults ForPreset(NativeDspPreset preset)
        {
            return preset switch
            {
                NativeDspPreset.DryReference => new NativePresetDefaults(0f, 0f, 0f),
                NativeDspPreset.StrongKtv => new NativePresetDefaults(0.86f, 0.62f, 0.9f),
                NativeDspPreset.SafeSmallRoom => new NativePresetDefaults(0.34f, 0.12f, 0.48f),
                _ => new NativePresetDefaults(0.55f, 0.3f, 0.65f),
            };
        }
    }

    public readonly struct NativeEffectTuning
    {
        public NativeEffectTuning(
            float dryGain,
            float reverbSend,
            float reverbPreDelayMs,
            float reverbDecay,
            float reverbWidth,
            float echoDelayMs,
            float echoFeedback,
            float echoWet,
            float doublingAmount)
        {
            DryGain = dryGain;
            ReverbSend = reverbSend;
            ReverbPreDelayMs = reverbPreDelayMs;
            ReverbDecay = reverbDecay;
            ReverbWidth = reverbWidth;
            EchoDelayMs = echoDelayMs;
            EchoFeedback = echoFeedback;
            EchoWet = echoWet;
            DoublingAmount = doublingAmount;
        }

        public float DryGain { get; }
        public float ReverbSend { get; }
        public float ReverbPreDelayMs { get; }
        public float ReverbDecay { get; }
        public float ReverbWidth { get; }
        public float EchoDelayMs { get; }
        public float EchoFeedback { get; }
        public float EchoWet { get; }
        public float DoublingAmount { get; }

        public static NativeEffectTuning ForPreset(
            NativeDspPreset preset,
            float ambience,
            float echo,
            float dynamics)
        {
            var a = Clamp01(ambience);
            var e = Clamp01(echo);
            var safe = preset == NativeDspPreset.SafeSmallRoom;
            var strong = preset == NativeDspPreset.StrongKtv;
            var dry = preset == NativeDspPreset.DryReference ? 1f : safe ? 0.98f : 0.98f - a * (strong ? 0.38f : 0.30f);
            // KTV deliberately emphasizes a long vocal tail over dry loudness.
            // Preserve the established input/output gain and HF feedback controls.
            var room = preset == NativeDspPreset.DryReference ? 0f : safe ? a * 0.42f : (float)Math.Sqrt(a) * (strong ? 0.96f : 0.90f);
            var decay = safe ? 0.6f + a * 0.8f : strong ? 3f + a * 5f : 2f + a * 4.5f;
            var predelay = safe || preset == NativeDspPreset.DryReference ? 0f : 12f + a * 23f;
            var width = preset == NativeDspPreset.DryReference ? 0f : 0.2f + a * 0.45f;
            // Wet effects are deliberately conservative in the live monitor:
            // delayed energy is re-captured by the headset microphone and can
            // otherwise turn a pleasant room tail into a tonal howl.
            var echoWet = preset == NativeDspPreset.DryReference ? 0f : e * (safe ? 0.06f : strong ? 0.12f : 0.09f);
            var feedback = safe ? e * 0.08f : e * (strong ? 0.16f : 0.12f);
            var doubling = preset == NativeDspPreset.DryReference ? 0f : (safe ? 0f : strong ? 0.10f : 0.035f) * e;
            return new NativeEffectTuning(
                dry,
                room,
                predelay,
                decay,
                width,
                48f + e * (safe ? 20f : 72f),
                feedback,
                echoWet,
                doubling);
        }

        private static float Clamp01(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
        }
    }

    public static class NativeParameterFactory
    {
        public static TsukiVoxNativeParameters Create(
            NativeMonitorProfile profile,
            NativeDspPreset dspPreset,
            bool forceSafeConfiguration,
            float requestedPreGain,
            float distanceGain,
            float safetyGain,
            float ambience,
            float echo,
            float dynamics,
            bool muted,
            NativeInputProcessingMode inputMode = NativeInputProcessingMode.Natural)
        {
            var tuning = NativeEffectTuning.ForPreset(dspPreset, ambience, echo, dynamics);
            return new TsukiVoxNativeParameters
            {
                size = NativeAudioAbi.ParameterSize,
                version = NativeAudioAbi.ApiVersion,
                requestedProfile = (int)NativeMonitorProfiles.Normalize((int)profile),
                forceSafeConfiguration = forceSafeConfiguration ? 1 : 0,
                inputProcessingMode = (int)NativeInputProcessing.Normalize((int)inputMode),
                dspPreset = (int)dspPreset,
                gain = NativeFeedbackSafetyTuning.CalculateOutputGain(requestedPreGain),
                inputDrive = NativeFeedbackSafetyTuning.CalculateInputDrive(dspPreset == NativeDspPreset.DryReference ? 0f : dynamics),
                distanceGain = Clamp01(distanceGain),
                safetyGain = Clamp01(safetyGain),
                ambience = Clamp01(ambience),
                echo = Clamp01(echo),
                dynamics = dspPreset == NativeDspPreset.DryReference ? 0f : Clamp01(dynamics),
                dryGain = tuning.DryGain,
                reverbSend = tuning.ReverbSend,
                reverbPreDelayMs = tuning.ReverbPreDelayMs,
                reverbDecay = tuning.ReverbDecay,
                reverbWidth = tuning.ReverbWidth,
                echoDelayMs = tuning.EchoDelayMs,
                echoFeedback = tuning.EchoFeedback,
                echoWet = tuning.EchoWet,
                doublingAmount = tuning.DoublingAmount,
                muted = muted ? 1 : 0,
                highPassHz = NativeFeedbackSafetyTuning.CalculateHighPassHz(dynamics),
            };
        }

        private static float Clamp01(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct TsukiVoxNativeParameters
    {
        public uint size;
        public uint version;
        public int requestedProfile;
        public int forceSafeConfiguration;
        public int inputProcessingMode;
        public int dspPreset;
        public float gain;
        public float inputDrive;
        public float distanceGain;
        public float safetyGain;
        public float ambience;
        public float echo;
        public float dynamics;
        public float dryGain;
        public float reverbSend;
        public float reverbPreDelayMs;
        public float reverbDecay;
        public float reverbWidth;
        public float echoDelayMs;
        public float echoFeedback;
        public float echoWet;
        public float doublingAmount;
        public int muted;
        public float highPassHz;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct TsukiVoxNativeStats
    {
        public uint size;
        public uint version;
        public int running;
        public int requestedProfile;
        public int resolvedProfile;
        public int fallbackStage;
        public int sampleRate;
        public int audioApi;
        public int inputPerformanceMode;
        public int outputPerformanceMode;
        public int inputSharingMode;
        public int outputSharingMode;
        public int inputMMapUsed;
        public int outputMMapUsed;
        public int inputPreset;
        public int inputFramesPerBurst;
        public int outputFramesPerBurst;
        public int inputChannelCount;
        public int outputChannelCount;
        public int inputFormat;
        public int outputFormat;
        public int inputCapacityFrames;
        public int outputCapacityFrames;
        public int requestedInputBufferFrames;
        public int requestedOutputBufferFrames;
        public int inputBufferFrames;
        public int outputBufferFrames;
        public int inputBurstsCushion;
        public int lastCallbackFrames;
        public int minCallbackFrames;
        public int maxCallbackFrames;
        public int inputXRunCount;
        public int outputXRunCount;
        public int inputXRunSupported;
        public int outputXRunSupported;
        public int lastStreamError;
        public int latencyValid;
        public int latencyErrorCount;
        public int fallbackReason;
        public int inputProcessingMode;
        public int dspPreset;
        public int dspVersion;
        public int callbackOverrunCount;
        public float actualGain;
        public float actualInputDrive;
        public float actualDistanceGain;
        public float actualSafetyGain;
        public float preDspLevel;
        public float postDspLevel;
        public float outputLevel;
        public float compressorReductionDb;
        public float limiterReductionDb;
        public float inputLatencyMs;
        public float outputLatencyMs;
        public float roundTripLatencyMs;
        public float startToFirstInputMs;
        public float dspAlgorithmLatencyMs;
        public float callbackCpuLoad;
        public float callbackMaxMs;
        public float dryGain;
        public float wetGain;
        public float callbackAverageMs;
        public ulong callbackCount;
        public ulong shortReadCount;
        public ulong frameMismatchCount;
        public ulong requestedInputFrameCount;
        public ulong receivedInputFrameCount;

        public bool IsRunning => running != 0;
        public bool IsInputExclusive => inputSharingMode != 0;
        public bool IsOutputExclusive => outputSharingMode != 0;
        public bool IsInputMMapUsed => inputMMapUsed != 0;
        public bool IsOutputMMapUsed => outputMMapUsed != 0;
        public bool IsLatencyValid => latencyValid != 0 && IsValidLatency(roundTripLatencyMs);
        public bool IsSafeConfiguration => inputBurstsCushion > 0 ||
                                           ResolvedProfile == NativeMonitorProfile.BaselineGenericCushion1 ||
                                           ResolvedProfile == NativeMonitorProfile.VoicePerformanceCushion1 ||
                                           FallbackStage == NativeFallbackStage.RuntimeSafe;
        public NativeMonitorProfile RequestedProfile => NativeMonitorProfiles.Normalize(requestedProfile);
        public NativeMonitorProfile ResolvedProfile => NativeMonitorProfiles.Normalize(resolvedProfile);
        public NativeFallbackStage FallbackStage => Enum.IsDefined(typeof(NativeFallbackStage), fallbackStage)
            ? (NativeFallbackStage)fallbackStage
            : NativeFallbackStage.None;
        public string AudioApiName => audioApi == 2 ? "AAudio" : audioApi == 1 ? "OpenSLES" : "Unknown";
        public string InputPresetName => inputPreset switch
        {
            NativeMonitorProfiles.GenericInputPreset => "Generic",
            NativeMonitorProfiles.VoiceRecognitionInputPreset => "VoiceRecognition",
            NativeMonitorProfiles.UnprocessedInputPreset => "Unprocessed",
            NativeMonitorProfiles.VoicePerformanceInputPreset => "VoicePerformance",
            _ => $"Unknown({inputPreset})",
        };
        public string FallbackStageName => FallbackStage switch
        {
            NativeFallbackStage.None => "none",
            NativeFallbackStage.SafeExclusive => "safe-exclusive",
            NativeFallbackStage.SafeShared => "safe-shared",
            NativeFallbackStage.GenericSafeShared => "generic-safe-shared",
            NativeFallbackStage.RuntimeSafe => "runtime-safe",
            NativeFallbackStage.ExperimentShared => "experiment-shared",
            NativeFallbackStage.Shared => "shared",
            NativeFallbackStage.GenericInput => "generic-input",
            _ => "unknown",
        };
        public float ShortReadRatio => callbackCount > 0 ? (float)shortReadCount / callbackCount : 0f;
        public float InputFrameFillRatio => requestedInputFrameCount > 0
            ? (float)Math.Min(receivedInputFrameCount, requestedInputFrameCount) / requestedInputFrameCount
            : 1f;

        private static bool IsValidLatency(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }

    public readonly struct NativeLatencySummary
    {
        public NativeLatencySummary(int count, float median, float p95)
        {
            Count = count;
            Median = median;
            P95 = p95;
        }

        public int Count { get; }
        public float Median { get; }
        public float P95 { get; }
        public bool IsAvailable => Count > 0;

        public static NativeLatencySummary Unavailable => new NativeLatencySummary(0, float.NaN, float.NaN);
    }

    public sealed class NativeLatencyWindow
    {
        public const double WindowSeconds = 30.0;

        private readonly Queue<LatencySample> samples = new Queue<LatencySample>(320);
        private float[] sortedValues = new float[320];

        public int Count => samples.Count;

        public void Add(double timestamp, float value)
        {
            if (double.IsNaN(timestamp) || double.IsInfinity(timestamp) ||
                float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return;
            }

            samples.Enqueue(new LatencySample(timestamp, value));
            Trim(timestamp);
        }

        public NativeLatencySummary GetSummary(double timestamp)
        {
            Trim(timestamp);
            if (samples.Count == 0)
            {
                return NativeLatencySummary.Unavailable;
            }

            if (sortedValues.Length < samples.Count)
            {
                Array.Resize(ref sortedValues, samples.Count * 2);
            }

            var index = 0;
            foreach (var sample in samples)
            {
                sortedValues[index] = sample.Value;
                index += 1;
            }

            Array.Sort(sortedValues, 0, samples.Count);
            return new NativeLatencySummary(
                samples.Count,
                Percentile(sortedValues, samples.Count, 0.5f),
                Percentile(sortedValues, samples.Count, 0.95f));
        }

        public void Clear()
        {
            samples.Clear();
        }

        private void Trim(double timestamp)
        {
            var cutoff = timestamp - WindowSeconds;
            while (samples.Count > 0 && samples.Peek().Timestamp < cutoff)
            {
                samples.Dequeue();
            }
        }

        private static float Percentile(float[] values, int count, float percentile)
        {
            var rank = Math.Max(0, Math.Min(count - 1,
                (int)Math.Ceiling(percentile * count) - 1));
            return values[rank];
        }

        private readonly struct LatencySample
        {
            public LatencySample(double timestamp, float value)
            {
                Timestamp = timestamp;
                Value = value;
            }

            public double Timestamp { get; }
            public float Value { get; }
        }
    }

    public sealed class NativeStabilityPolicy
    {
        public const double WarmupSeconds = 2.0;
        public const double CounterWindowSeconds = 10.0;
        public const int FastShortReadThreshold = 3;
        public const int SafeXRunThreshold = 3;
        public const int SafeShortReadThreshold = 10;
        public const int SafeCallbackOverrunThreshold = 3;

        private readonly Queue<CounterSample> samples = new Queue<CounterSample>(110);
        private double startedAt;
        private bool warmupComplete;
        private double callbackBusySince = double.NaN;

        public void Reset(double timestamp, TsukiVoxNativeStats stats)
        {
            samples.Clear();
            startedAt = timestamp;
            warmupComplete = false;
            callbackBusySince = double.NaN;
            samples.Enqueue(new CounterSample(timestamp, TotalXRuns(stats), stats.shortReadCount, stats.callbackOverrunCount));
        }

        public NativeStabilityAction Observe(
            double timestamp,
            TsukiVoxNativeStats stats,
            bool productionProfile,
            bool safeConfiguration)
        {
            if (!stats.IsRunning || stats.lastStreamError != 0)
            {
                return safeConfiguration
                    ? NativeStabilityAction.FallbackUnity
                    : NativeStabilityAction.RestartSafe;
            }

            if (timestamp - startedAt < WarmupSeconds)
            {
                return NativeStabilityAction.None;
            }

            if (!warmupComplete)
            {
                samples.Clear();
                samples.Enqueue(new CounterSample(timestamp, TotalXRuns(stats), stats.shortReadCount, stats.callbackOverrunCount));
                warmupComplete = true;
                return NativeStabilityAction.None;
            }

            AddAndTrim(timestamp, stats);

            var oldest = samples.Peek();
            var xRunDelta = Math.Max(0, TotalXRuns(stats) - oldest.TotalXRuns);
            var shortReadDelta = stats.shortReadCount >= oldest.ShortReads
                ? stats.shortReadCount - oldest.ShortReads
                : 0;

            var overrunDelta = Math.Max(0, stats.callbackOverrunCount - oldest.Overruns);
            if (stats.callbackCpuLoad < 0.9f) callbackBusySince = double.NaN;
            else if (double.IsNaN(callbackBusySince)) callbackBusySince = timestamp;
            var callbackBusy = !double.IsNaN(callbackBusySince) && timestamp - callbackBusySince >= 1.0;
            if (!safeConfiguration)
            {
                return xRunDelta > 0 || shortReadDelta >= FastShortReadThreshold || overrunDelta > 0 || callbackBusy
                    ? NativeStabilityAction.RestartSafe
                    : NativeStabilityAction.None;
            }

            return xRunDelta >= SafeXRunThreshold || shortReadDelta >= SafeShortReadThreshold ||
                   overrunDelta >= SafeCallbackOverrunThreshold || callbackBusy
                ? NativeStabilityAction.FallbackUnity
                : NativeStabilityAction.None;
        }

        private void AddAndTrim(double timestamp, TsukiVoxNativeStats stats)
        {
            samples.Enqueue(new CounterSample(timestamp, TotalXRuns(stats), stats.shortReadCount, stats.callbackOverrunCount));
            var cutoff = timestamp - CounterWindowSeconds;
            while (samples.Count > 1 && samples.Peek().Timestamp < cutoff)
            {
                samples.Dequeue();
            }
        }

        private static int TotalXRuns(TsukiVoxNativeStats stats)
        {
            return Math.Max(0, stats.inputXRunCount) + Math.Max(0, stats.outputXRunCount);
        }

        private readonly struct CounterSample
        {
            public CounterSample(double timestamp, int totalXRuns, ulong shortReads, int overruns)
            {
                Timestamp = timestamp;
                TotalXRuns = totalXRuns;
                ShortReads = shortReads;
                Overruns = overruns;
            }

            public double Timestamp { get; }
            public int TotalXRuns { get; }
            public ulong ShortReads { get; }
            public int Overruns { get; }
        }
    }
}
