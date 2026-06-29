using System;
using UnityEngine;

namespace TsukiVox.AudioPrototype
{
    internal static class TsukiVoxClipboard
    {
        public static void CopyPlainText(string label, string text)
        {
            GUIUtility.systemCopyBuffer = text;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var clipboard = activity.Call<AndroidJavaObject>("getSystemService", "clipboard");
                using var clipDataClass = new AndroidJavaClass("android.content.ClipData");
                using var clip = clipDataClass.CallStatic<AndroidJavaObject>("newPlainText", label, text);
                clipboard.Call("setPrimaryClip", clip);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TsukiVox Clipboard] Android clipboard copy failed: {exception.Message}");
            }
#endif
        }
    }
}
