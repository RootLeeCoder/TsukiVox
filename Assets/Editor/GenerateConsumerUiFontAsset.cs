using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TsukiVox.AudioPrototype.Editor
{
    public static class GenerateConsumerUiFontAsset
    {
        private const string SourceFontPath = "Assets/Resources/Fonts/NotoSansSC-VF.ttf";
        private const string FontAssetPath = "Assets/Resources/Fonts/NotoSansSC-SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private const string UiCharacters =
            " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            "-+_.,:;!?%/\\()[]<>·…°" +
            "TsukiVoxKTVPCIPBilibiliYouTubeNativewaitingmissing" +
            "月读声域点歌方式直接请求已就绪离线播放队列为空等待歌曲未命名正在准备失败暂停中本地视频" +
            "从添加后即可开始请在设置检查麦克风开启权限调整人声返听音量建议先低后高效果" +
            "四档预设直接选择原声强效柔和安全保护会在过响时自动降低上一首下一首重播" +
            "应用默认高级输出关闭仍保留输入始终保持延迟路径不包含诊断与支持状态原始信息" +
            "复制完整展开收起详情组件缺失音频正常待机后端输入输出完整调试就绪连接配置";

        [MenuItem("TsukiVox/Generate Consumer UI Font Asset")]
        public static void Generate()
        {
            var settings = EnsureTmpSettings();
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException($"Source font not found: {SourceFontPath}");
            }

            AssetDatabase.DeleteAsset(FontAssetPath);
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                96,
                10,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
            if (fontAsset == null)
            {
                throw new InvalidOperationException("Unable to create the TsukiVox SDF font asset.");
            }

            fontAsset.name = "NotoSansSC-SDF";
            fontAsset.isMultiAtlasTexturesEnabled = true;
            fontAsset.TryAddCharacters(UiCharacters, out var missingCharacters);

            var material = fontAsset.material;
            var atlasTextures = fontAsset.atlasTextures;
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            if (material != null)
            {
                material.name = "NotoSansSC-SDF Material";
                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }

            for (var index = 0; index < atlasTextures.Length; index += 1)
            {
                var texture = atlasTextures[index];
                if (texture == null || AssetDatabase.Contains(texture))
                {
                    continue;
                }

                texture.name = $"NotoSansSC-SDF Atlas {index}";
                AssetDatabase.AddObjectToAsset(texture, fontAsset);
            }

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceSynchronousImport);

            if (!string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogWarning($"[TsukiVox UI] SDF font seed is missing: {missingCharacters}");
            }

            Debug.Log($"[TsukiVox UI] Generated {FontAssetPath}");
        }

        private static TMP_Settings EnsureTmpSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                throw new InvalidOperationException("TMP Essential Resources are missing from Assets/TextMesh Pro.");
            }

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("assetVersion").stringValue = "2";
            serializedSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TmpSettingsPath, ImportAssetOptions.ForceSynchronousImport);
            TMP_Settings.LoadDefaultSettings();
            return settings;
        }
    }
}
