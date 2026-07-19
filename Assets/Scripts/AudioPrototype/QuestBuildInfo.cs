using System;
using UnityEngine;

namespace TsukiVox.AudioPrototype
{
    [Serializable]
    public sealed class QuestBuildInfoData
    {
        public string productVersion;
        public string buildId;
        public string buildTimeUtc;
        public string gitCommit;
        public bool gitDirty;
    }

    public static class QuestBuildInfo
    {
        public const string ProductVersion = "0.65";
        public const string ResourceName = "TsukiVoxBuildInfo";

        private static readonly QuestBuildInfoData Data = Load();

        public static string Version => ValueOrFallback(Data.productVersion, ProductVersion);
        public static string BuildId => ValueOrFallback(Data.buildId, "editor");
        public static string BuildTimeUtc => ValueOrFallback(Data.buildTimeUtc, "unavailable");
        public static string GitCommit => ValueOrFallback(Data.gitCommit, "unavailable");
        public static bool GitDirty => Data.gitDirty;

        public static string BuildTimeDisplay
        {
            get
            {
                return DateTimeOffset.TryParse(BuildTimeUtc, out var timestamp)
                    ? timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    : "未生成";
            }
        }

        public static string SettingsSummary
        {
            get
            {
                var shortId = BuildId.Length > 6 ? BuildId.Substring(BuildId.Length - 6) : BuildId;
                return $"V{Version} · 构建于 {BuildTimeDisplay} · #{shortId}";
            }
        }

        public static string RawSummary => $"V{Version} · {BuildTimeDisplay} · {BuildId}";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LogCurrentBuild()
        {
            Debug.Log(
                $"[TsukiVox Build] id={BuildId} timeUtc={BuildTimeUtc} " +
                $"version={Version} git={GitCommit} dirty={GitDirty} guid={Application.buildGUID}");
        }

        private static QuestBuildInfoData Load()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return new QuestBuildInfoData
                {
                    productVersion = ProductVersion,
                    buildId = "editor",
                    buildTimeUtc = string.Empty,
                    gitCommit = string.Empty,
                    gitDirty = false,
                };
            }

            try
            {
                return JsonUtility.FromJson<QuestBuildInfoData>(asset.text) ?? new QuestBuildInfoData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TsukiVox Build] Could not read embedded build info: {exception.Message}");
                return new QuestBuildInfoData();
            }
        }

        private static string ValueOrFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
