using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TsukiVox.AudioPrototype
{
    public static class NativeAudioAbi
    {
        public const int ApiVersion = 4;
        public const int ParameterSize = 52;
        public const int StatsSize = 248;
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
                NativeMonitorProfile.Production or NativeMonitorProfile.VoicePerformanceCushion0 =>
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
                NativeMonitorProfile.Production => "Production",
                NativeMonitorProfile.BaselineGenericCushion1 => "Generic · C1 · O2",
                NativeMonitorProfile.VoiceRecognitionCushion0 => "Recognition · C0 · O2",
                NativeMonitorProfile.VoicePerformanceCushion0 => "Performance · C0 · O2",
                NativeMonitorProfile.VoicePerformanceCushion1 => "Performance · C1 · O2",
                NativeMonitorProfile.VoicePerformanceCushion0Output1 => "Performance · C0 · O1",
                _ => "Production",
            };
        }
    }

    public static class AudioMonitorPreference
    {
        public const MonitorMode DefaultMode = MonitorMode.OboeLowLatency;

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
        public const float DefaultOutputGain = 0.65f;
        public const float MaximumOutputGain = 1f;
        public const float MaximumInputDrive = 5f;
        public const float MinimumHighPassHz = 65f;
        public const float MaximumHighPassHz = 85f;

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

    [StructLayout(LayoutKind.Sequential)]
    public struct TsukiVoxNativeParameters
    {
        public uint size;
        public uint version;
        public int requestedProfile;
        public int forceSafeConfiguration;
        public float gain;
        public float inputDrive;
        public float distanceGain;
        public float safetyGain;
        public float ambience;
        public float echo;
        public float dynamics;
        public int muted;
        public float highPassHz;
    }

    [StructLayout(LayoutKind.Sequential)]
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

        private readonly Queue<CounterSample> samples = new Queue<CounterSample>(110);
        private double startedAt;
        private bool warmupComplete;

        public void Reset(double timestamp, TsukiVoxNativeStats stats)
        {
            samples.Clear();
            startedAt = timestamp;
            warmupComplete = false;
            samples.Enqueue(new CounterSample(timestamp, TotalXRuns(stats), stats.shortReadCount));
        }

        public NativeStabilityAction Observe(
            double timestamp,
            TsukiVoxNativeStats stats,
            bool productionProfile,
            bool safeConfiguration)
        {
            if (!productionProfile)
            {
                return NativeStabilityAction.None;
            }

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
                samples.Enqueue(new CounterSample(timestamp, TotalXRuns(stats), stats.shortReadCount));
                warmupComplete = true;
                return NativeStabilityAction.None;
            }

            AddAndTrim(timestamp, stats);

            var oldest = samples.Peek();
            var xRunDelta = Math.Max(0, TotalXRuns(stats) - oldest.TotalXRuns);
            var shortReadDelta = stats.shortReadCount >= oldest.ShortReads
                ? stats.shortReadCount - oldest.ShortReads
                : 0;

            if (!safeConfiguration)
            {
                return xRunDelta > 0 || shortReadDelta >= FastShortReadThreshold
                    ? NativeStabilityAction.RestartSafe
                    : NativeStabilityAction.None;
            }

            return xRunDelta >= SafeXRunThreshold || shortReadDelta >= SafeShortReadThreshold
                ? NativeStabilityAction.FallbackUnity
                : NativeStabilityAction.None;
        }

        private void AddAndTrim(double timestamp, TsukiVoxNativeStats stats)
        {
            samples.Enqueue(new CounterSample(timestamp, TotalXRuns(stats), stats.shortReadCount));
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
            public CounterSample(double timestamp, int totalXRuns, ulong shortReads)
            {
                Timestamp = timestamp;
                TotalXRuns = totalXRuns;
                ShortReads = shortReads;
            }

            public double Timestamp { get; }
            public int TotalXRuns { get; }
            public ulong ShortReads { get; }
        }
    }
}
