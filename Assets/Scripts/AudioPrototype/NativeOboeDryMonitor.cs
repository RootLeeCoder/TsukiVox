using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TsukiVox.AudioPrototype
{
    public enum NativeAudioBackend
    {
        UnityMicrophone,
        NativeOboeDryMonitor,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TsukiVoxNativeStats
    {
        public int running;
        public int sampleRate;
        public int framesPerBurst;
        public int inputBufferFrames;
        public int outputBufferFrames;
        public int inputXRunCount;
        public int outputXRunCount;
        public int sharingMode;
        public float inputLevel;
        public float outputLevel;
        public ulong callbackCount;

        public bool IsRunning => running != 0;
        public bool IsExclusive => sharingMode == 1;
    }

    public static class NativeOboeDryMonitor
    {
        private const string PluginName = "tsukivox_oboe_monitor";

        public static bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static bool TryStart(float gain, bool muted, out string error)
        {
            error = string.Empty;
            if (!IsAvailable)
            {
                error = "Native Oboe backend is only available on Android player builds.";
                return false;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var started = TsukiVoxAudio_Start(gain, muted ? 1 : 0) != 0;
                if (!started)
                {
                    error = GetLastError();
                }

                return started;
            }
            catch (Exception exception) when (exception is DllNotFoundException || exception is EntryPointNotFoundException)
            {
                error = exception.Message;
                return false;
            }
#else
            return false;
#endif
        }

        public static void Stop()
        {
            if (!IsAvailable)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            TsukiVoxAudio_Stop();
#endif
        }

        public static void SetGain(float gain)
        {
            if (!IsAvailable)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            TsukiVoxAudio_SetGain(gain);
#endif
        }

        public static void SetMuted(bool muted)
        {
            if (!IsAvailable)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            TsukiVoxAudio_SetMuted(muted ? 1 : 0);
#endif
        }

        public static bool TryGetStats(out TsukiVoxNativeStats stats)
        {
            stats = default;
            if (!IsAvailable)
            {
                return false;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            return TsukiVoxAudio_GetStats(out stats) != 0;
#else
            return false;
#endif
        }

        public static string GetLastError()
        {
            if (!IsAvailable)
            {
                return string.Empty;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            var pointer = TsukiVoxAudio_GetLastError();
            return pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer);
#else
            return string.Empty;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(PluginName)]
        private static extern int TsukiVoxAudio_Start(float gain, int muted);

        [DllImport(PluginName)]
        private static extern void TsukiVoxAudio_Stop();

        [DllImport(PluginName)]
        private static extern void TsukiVoxAudio_SetGain(float gain);

        [DllImport(PluginName)]
        private static extern void TsukiVoxAudio_SetMuted(int muted);

        [DllImport(PluginName)]
        private static extern int TsukiVoxAudio_GetStats(out TsukiVoxNativeStats stats);

        [DllImport(PluginName)]
        private static extern IntPtr TsukiVoxAudio_GetLastError();
#endif
    }
}
