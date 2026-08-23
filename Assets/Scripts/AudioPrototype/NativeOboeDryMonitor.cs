using System;
using System.Runtime.InteropServices;

namespace TsukiVox.AudioPrototype
{
    public enum NativeAudioBackend
    {
        UnityMicrophone,
        NativeOboeLowLatency,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TsukiVoxNativeParameters
    {
        public uint size;
        public uint version;
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
        public int sampleRate;
        public int audioApi;
        public int inputSharingMode;
        public int outputSharingMode;
        public int inputPreset;
        public int framesPerBurst;
        public int inputCapacityFrames;
        public int outputCapacityFrames;
        public int requestedInputBufferFrames;
        public int requestedOutputBufferFrames;
        public int inputBufferFrames;
        public int outputBufferFrames;
        public int inputXRunCount;
        public int outputXRunCount;
        public int lastStreamError;
        public float actualGain;
        public float actualInputDrive;
        public float actualDistanceGain;
        public float actualSafetyGain;
        public float preDspLevel;
        public float postDspLevel;
        public float outputLevel;
        public float compressorReductionDb;
        public float limiterReductionDb;
        public ulong callbackCount;
        public ulong shortReadCount;
        public ulong frameMismatchCount;
        public ulong requestedInputFrameCount;
        public ulong receivedInputFrameCount;

        public bool IsRunning => running != 0;
        public bool IsInputExclusive => inputSharingMode == 1;
        public bool IsOutputExclusive => outputSharingMode == 1;
        public string AudioApiName => audioApi == 2 ? "AAudio" : audioApi == 1 ? "OpenSLES" : "Unknown";
        public float ShortReadRatio => callbackCount > 0 ? (float)shortReadCount / callbackCount : 0f;
        public float InputFrameFillRatio => requestedInputFrameCount > 0
            ? (float)Math.Min(receivedInputFrameCount, requestedInputFrameCount) / requestedInputFrameCount
            : 1f;
    }

    public static class NativeOboeDryMonitor
    {
        public const int RequiredApiVersion = 3;
        private const string PluginName = "tsukivox_oboe_monitor";
        private static bool isInvalid;
        private static string invalidReason = string.Empty;

        public static bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return !isInvalid;
#else
                return false;
#endif
            }
        }

        public static bool IsInvalid => isInvalid;
        public static string InvalidReason => invalidReason;

        public static TsukiVoxNativeParameters CreateParameters(
            float gain,
            float inputDrive,
            float distanceGain,
            float safetyGain,
            float ambience,
            float echo,
            float dynamics,
            bool muted)
        {
            return new TsukiVoxNativeParameters
            {
                size = (uint)Marshal.SizeOf<TsukiVoxNativeParameters>(),
                version = RequiredApiVersion,
                gain = gain,
                inputDrive = inputDrive,
                distanceGain = distanceGain,
                safetyGain = safetyGain,
                ambience = ambience,
                echo = echo,
                dynamics = dynamics,
                muted = muted ? 1 : 0,
                highPassHz = MathfLerp(70f, 110f, dynamics),
            };
        }

        public static bool TryStart(TsukiVoxNativeParameters parameters, out string error)
        {
            error = string.Empty;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!TryHandshake(out error)) return false;
            try
            {
                if (TsukiVoxAudio_StartV3(ref parameters, parameters.size) != 0) return true;
                error = GetLastErrorSafe("Native Oboe start failed.");
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Invalidate(exception.Message);
                error = invalidReason;
            }
#else
            error = "Native Oboe backend is only available on Android player builds.";
#endif
            return false;
        }

        public static bool TrySetParameters(TsukiVoxNativeParameters parameters)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAvailable) return false;
            try { return TsukiVoxAudio_SetParametersV3(ref parameters, parameters.size) != 0; }
            catch (Exception exception) when (IsInteropException(exception)) { Invalidate(exception.Message); }
#endif
            return false;
        }

        public static void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAvailable) return;
            try { TsukiVoxAudio_Stop(); }
            catch (Exception exception) when (IsInteropException(exception)) { Invalidate(exception.Message); }
#endif
        }

        public static bool TryGetStats(out TsukiVoxNativeStats stats)
        {
            stats = default;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAvailable) return false;
            try
            {
                stats.size = (uint)Marshal.SizeOf<TsukiVoxNativeStats>();
                stats.version = RequiredApiVersion;
                return TsukiVoxAudio_GetStatsV3(ref stats, stats.size) != 0 && stats.version == RequiredApiVersion;
            }
            catch (Exception exception) when (IsInteropException(exception)) { Invalidate(exception.Message); }
#endif
            return false;
        }

        public static string GetLastError()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return GetLastErrorSafe(string.Empty);
#else
            return string.Empty;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool TryHandshake(out string error)
        {
            error = string.Empty;
            if (isInvalid) { error = invalidReason; return false; }
            try
            {
                var actualVersion = TsukiVoxAudio_GetApiVersion();
                if (actualVersion == RequiredApiVersion) return true;
                Invalidate($"Native audio API mismatch: expected {RequiredApiVersion}, got {actualVersion}.");
            }
            catch (Exception exception) when (IsInteropException(exception)) { Invalidate(exception.Message); }
            error = invalidReason;
            return false;
        }

        private static string GetLastErrorSafe(string fallback)
        {
            try
            {
                var pointer = TsukiVoxAudio_GetLastError();
                var message = pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer);
                return string.IsNullOrWhiteSpace(message) ? fallback : message;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Invalidate(exception.Message);
                return invalidReason;
            }
        }
#endif

        private static float MathfLerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));

        private static bool IsInteropException(Exception exception) =>
            exception is DllNotFoundException || exception is EntryPointNotFoundException ||
            exception is BadImageFormatException || exception is MarshalDirectiveException;

        private static void Invalidate(string reason)
        {
            isInvalid = true;
            invalidReason = string.IsNullOrWhiteSpace(reason) ? "Native audio backend became unavailable." : reason;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(PluginName)] private static extern int TsukiVoxAudio_GetApiVersion();
        [DllImport(PluginName)] private static extern int TsukiVoxAudio_StartV3(ref TsukiVoxNativeParameters parameters, uint size);
        [DllImport(PluginName)] private static extern int TsukiVoxAudio_SetParametersV3(ref TsukiVoxNativeParameters parameters, uint size);
        [DllImport(PluginName)] private static extern void TsukiVoxAudio_Stop();
        [DllImport(PluginName)] private static extern int TsukiVoxAudio_GetStatsV3(ref TsukiVoxNativeStats stats, uint size);
        [DllImport(PluginName)] private static extern IntPtr TsukiVoxAudio_GetLastError();
#endif
    }
}
