using System;
using System.Runtime.InteropServices;

namespace TsukiVox.AudioPrototype
{
    public static class NativeOboeDryMonitor
    {
        public const int RequiredApiVersion = NativeAudioAbi.ApiVersion;
        public const int ExpectedParameterSize = NativeAudioAbi.ParameterSize;
        public const int ExpectedStatsSize = NativeAudioAbi.StatsSize;

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
        public static int ManagedParameterSize => Marshal.SizeOf<TsukiVoxNativeParameters>();
        public static int ManagedStatsSize => Marshal.SizeOf<TsukiVoxNativeStats>();

        public static TsukiVoxNativeParameters CreateParameters(
            NativeMonitorProfile profile,
            bool forceSafeConfiguration,
            float requestedPreGain,
            float distanceGain,
            float safetyGain,
            float ambience,
            float echo,
            float dynamics,
            bool muted)
        {
            return new TsukiVoxNativeParameters
            {
                size = (uint)ManagedParameterSize,
                version = RequiredApiVersion,
                requestedProfile = (int)NativeMonitorProfiles.Normalize((int)profile),
                forceSafeConfiguration = forceSafeConfiguration ? 1 : 0,
                gain = NativeFeedbackSafetyTuning.CalculateOutputGain(requestedPreGain),
                inputDrive = NativeFeedbackSafetyTuning.CalculateInputDrive(dynamics),
                distanceGain = distanceGain,
                safetyGain = safetyGain,
                ambience = ambience,
                echo = echo,
                dynamics = dynamics,
                muted = muted ? 1 : 0,
                highPassHz = NativeFeedbackSafetyTuning.CalculateHighPassHz(dynamics),
            };
        }

        public static bool TryStart(TsukiVoxNativeParameters parameters, out string error)
        {
            error = string.Empty;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!TryHandshake(out error))
            {
                return false;
            }

            try
            {
                if (TsukiVoxAudio_StartV4(ref parameters, parameters.size) != 0)
                {
                    return true;
                }

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
            if (!IsAvailable)
            {
                return false;
            }

            try
            {
                return TsukiVoxAudio_SetParametersV4(ref parameters, parameters.size) != 0;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Invalidate(exception.Message);
            }
#endif
            return false;
        }

        public static void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAvailable)
            {
                return;
            }

            try
            {
                TsukiVoxAudio_Stop();
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Invalidate(exception.Message);
            }
#endif
        }

        public static bool TryGetStats(out TsukiVoxNativeStats stats)
        {
            stats = default;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAvailable)
            {
                return false;
            }

            try
            {
                stats.size = (uint)ManagedStatsSize;
                stats.version = RequiredApiVersion;
                return TsukiVoxAudio_GetStatsV4(ref stats, stats.size) != 0 &&
                       stats.version == RequiredApiVersion &&
                       stats.size == ManagedStatsSize;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Invalidate(exception.Message);
            }
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
            if (isInvalid)
            {
                error = invalidReason;
                return false;
            }

            if (ManagedParameterSize != ExpectedParameterSize || ManagedStatsSize != ExpectedStatsSize)
            {
                Invalidate(
                    $"Managed native audio ABI size mismatch: parameters {ManagedParameterSize}/{ExpectedParameterSize}, " +
                    $"stats {ManagedStatsSize}/{ExpectedStatsSize}.");
                error = invalidReason;
                return false;
            }

            try
            {
                var actualVersion = TsukiVoxAudio_GetApiVersion();
                if (actualVersion == RequiredApiVersion)
                {
                    return true;
                }

                Invalidate($"Native audio API mismatch: expected {RequiredApiVersion}, got {actualVersion}.");
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Invalidate(exception.Message);
            }

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

        private static bool IsInteropException(Exception exception)
        {
            return exception is DllNotFoundException ||
                   exception is EntryPointNotFoundException ||
                   exception is BadImageFormatException ||
                   exception is MarshalDirectiveException;
        }

        private static void Invalidate(string reason)
        {
            isInvalid = true;
            invalidReason = string.IsNullOrWhiteSpace(reason)
                ? "Native audio backend became unavailable."
                : reason;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(PluginName)] private static extern int TsukiVoxAudio_GetApiVersion();
        [DllImport(PluginName)] private static extern int TsukiVoxAudio_StartV4(ref TsukiVoxNativeParameters parameters, uint size);
        [DllImport(PluginName)] private static extern int TsukiVoxAudio_SetParametersV4(ref TsukiVoxNativeParameters parameters, uint size);
        [DllImport(PluginName)] private static extern void TsukiVoxAudio_Stop();
        [DllImport(PluginName)] private static extern int TsukiVoxAudio_GetStatsV4(ref TsukiVoxNativeStats stats, uint size);
        [DllImport(PluginName)] private static extern IntPtr TsukiVoxAudio_GetLastError();
#endif
    }
}
