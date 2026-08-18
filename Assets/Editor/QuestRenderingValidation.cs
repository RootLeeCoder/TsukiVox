using System;
using System.IO;
using System.Linq;
using TsukiVox.AudioPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TsukiVox.AudioPrototype.Editor
{
    public static class QuestRenderingValidation
    {
        private const string ScenePath = "Assets/Scenes/AudioPrototype.unity";
        private const string PreviewPath = "Logs/QuestUrpPreview.png";
        private const string LoungePreviewPath = "Logs/QuestUrpLoungePreview.png";
        private const string CeilingPreviewPath = "Logs/QuestUrpCeilingPreview.png";
        private const string BrightPreviewPath = "Logs/QuestUrpBrightPreview.png";
        private const string BrightCeilingPreviewPath = "Logs/QuestUrpBrightCeilingPreview.png";
        private const string StageLightingPanelPreviewPath = "Logs/QuestStageLightingPanelPreview.png";

        [MenuItem("TsukiVox/Capture Quest Rendering Preview")]
        public static void CapturePreview()
        {
            QuestUrpProjectSettings.EnsureConfigured();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var room = UnityEngine.Object.FindAnyObjectByType<QuestKtvRoomPrototype>();
            if (room == null)
            {
                throw new InvalidOperationException("AudioPrototype scene has no KTV room prototype.");
            }

            room.ConfigureSceneReferences();
            var appShell = QuestAppShellPrototype.EnsureSceneShell();
            ValidateCelestialTheme(room, RoomTheme.Dark);
            var stageLighting = QuestStageLightingPrototype.EnsureSceneLighting(
                room,
                UnityEngine.Object.FindAnyObjectByType<QuestAudioPrototype>());
            stageLighting.ApplyPreset(StageLightingPreset.Live);
            appShell.ConfigureSceneReferences();

            var shader = Shader.Find(QuestStylizedMaterial.ShaderName);
            if (shader == null || !shader.isSupported)
            {
                throw new InvalidOperationException($"Shader {QuestStylizedMaterial.ShaderName} is unavailable or unsupported.");
            }

            var camera = Camera.main;
            if (camera == null)
            {
                throw new InvalidOperationException("AudioPrototype scene has no Main Camera.");
            }

            const int width = 1600;
            const int height = 900;
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));

            var previousTarget = camera.targetTexture;
            var previousStereoTarget = camera.stereoTargetEye;
            var previousActive = RenderTexture.active;
            var previousPosition = camera.transform.position;
            var previousRotation = camera.transform.rotation;
            var previousFieldOfView = camera.fieldOfView;
            var previousTheme = room.CurrentTheme;
            var renderTexture = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear)
            {
                name = "TsukiVox Quest URP Preview",
                antiAliasing = 1,
            };
            var linearTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            var outputTexture = new Texture2D(width, height, TextureFormat.RGB24, false, true);

            try
            {
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.targetTexture = renderTexture;
                camera.Render();
                camera.Render();

                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, PreviewPath);

                camera.transform.position = new Vector3(2.72f, 1.58f, 3.72f);
                camera.transform.LookAt(new Vector3(0f, 0.72f, -0.1f));
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, LoungePreviewPath);

                camera.transform.position = new Vector3(0f, 1.12f, -0.05f);
                camera.transform.LookAt(new Vector3(0f, 2.82f, 2.28f));
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, CeilingPreviewPath);

                room.ApplyTheme(RoomTheme.Bright);
                ValidateCelestialTheme(room, RoomTheme.Bright);
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, BrightCeilingPreviewPath);

                camera.transform.position = previousPosition;
                camera.transform.rotation = previousRotation;
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, BrightPreviewPath);

                var controlCanvas = UnityEngine.Object.FindAnyObjectByType<QuestAppShellPrototype>()
                    ?.GetComponent<QuestConsumerUiPrototype>()
                    ?.GetComponentInParent<Canvas>();
                controlCanvas = controlCanvas != null
                    ? controlCanvas
                    : UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)
                        .FirstOrDefault(item => item.name == "Prototype Canvas");
                if (controlCanvas == null)
                {
                    throw new InvalidOperationException("AudioPrototype scene has no control Canvas.");
                }

                var pageRects = controlCanvas.GetComponentsInChildren<RectTransform>(true);
                RectTransform stageLightingPage = null;
                for (var index = 0; index < pageRects.Length; index += 1)
                {
                    if (pageRects[index].parent == null || pageRects[index].parent.name != "Consumer UI" ||
                        !pageRects[index].name.EndsWith(" Page", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var isStageLightingPage = pageRects[index].name == "Stage Lighting Page";
                    pageRects[index].gameObject.SetActive(isStageLightingPage);
                    var group = pageRects[index].GetComponent<CanvasGroup>();
                    if (group != null)
                    {
                        group.alpha = isStageLightingPage ? 1f : 0f;
                        group.interactable = isStageLightingPage;
                        group.blocksRaycasts = isStageLightingPage;
                    }

                    if (isStageLightingPage)
                    {
                        stageLightingPage = pageRects[index];
                    }
                }

                if (stageLightingPage == null)
                {
                    throw new InvalidOperationException("Stage Lighting control page was not generated.");
                }

                var queueButton = controlCanvas.transform.Find("Panel/Consumer UI/Open Queue Drawer");
                if (queueButton != null)
                {
                    queueButton.gameObject.SetActive(false);
                }

                var canvasTransform = controlCanvas.transform;
                camera.transform.position = canvasTransform.position - canvasTransform.forward * 0.95f;
                camera.transform.rotation = Quaternion.LookRotation(
                    canvasTransform.position - camera.transform.position,
                    canvasTransform.up);
                camera.fieldOfView = 52f;
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, StageLightingPanelPreviewPath);
            }
            finally
            {
                room.ApplyTheme(previousTheme);
                camera.targetTexture = previousTarget;
                camera.stereoTargetEye = previousStereoTarget;
                camera.transform.position = previousPosition;
                camera.transform.rotation = previousRotation;
                camera.fieldOfView = previousFieldOfView;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(linearTexture);
                UnityEngine.Object.DestroyImmediate(outputTexture);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            Debug.Log(
                $"[TsukiVox URP] Captured rendering previews at {Path.GetFullPath(PreviewPath)} " +
                $"{Path.GetFullPath(LoungePreviewPath)}, {Path.GetFullPath(CeilingPreviewPath)} and " +
                $"{Path.GetFullPath(BrightPreviewPath)}, {Path.GetFullPath(BrightCeilingPreviewPath)}, " +
                $"{Path.GetFullPath(StageLightingPanelPreviewPath)}.");
        }

        private static void ValidateCelestialTheme(QuestKtvRoomPrototype room, RoomTheme theme)
        {
            var moonNight = theme == RoomTheme.Dark;
            AssertActiveState(room, "ceiling moon fixture", moonNight);
            AssertActiveState(room, "ceiling sun fixture", !moonNight);
            AssertActiveState(room, "ceiling starfield", moonNight);
            AssertActiveState(room, "ceiling aurora", moonNight);
            AssertActiveState(room, "ceiling sunset clouds", !moonNight);
            AssertActiveState(room, "left corner moon relic", moonNight);
            AssertActiveState(room, "right corner moon relic", moonNight);
            AssertActiveState(room, "left corner sun relic", !moonNight);
            AssertActiveState(room, "right corner sun relic", !moonNight);
        }

        private static void AssertActiveState(QuestKtvRoomPrototype room, string objectName, bool expectedActive)
        {
            var transforms = room.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index += 1)
            {
                if (transforms[index].name != objectName)
                {
                    continue;
                }

                if (transforms[index].gameObject.activeInHierarchy != expectedActive)
                {
                    throw new InvalidOperationException(
                        $"Celestial object '{objectName}' active state did not match the selected room theme.");
                }

                return;
            }

            throw new InvalidOperationException($"Celestial object '{objectName}' was not generated.");
        }

        private static void WriteSrgbPng(Texture2D linearTexture, Texture2D outputTexture, string path)
        {
            var linearPixels = linearTexture.GetPixels();
            var outputPixels = new Color32[linearPixels.Length];
            var convertToSrgb = PlayerSettings.colorSpace == ColorSpace.Linear;
            for (var index = 0; index < linearPixels.Length; index += 1)
            {
                var pixel = linearPixels[index];
                if (!IsFinite(pixel.r) || !IsFinite(pixel.g) || !IsFinite(pixel.b))
                {
                    throw new InvalidOperationException($"Rendering preview contains a non-finite pixel at index {index}.");
                }

                outputPixels[index] = new Color32(
                    ToDisplayByte(pixel.r, convertToSrgb),
                    ToDisplayByte(pixel.g, convertToSrgb),
                    ToDisplayByte(pixel.b, convertToSrgb),
                    255);
            }

            outputTexture.SetPixels32(outputPixels);
            outputTexture.Apply(false, false);
            File.WriteAllBytes(path, outputTexture.EncodeToPNG());
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static byte ToDisplayByte(float value, bool convertToSrgb)
        {
            var displayValue = Mathf.Max(0f, value);
            if (convertToSrgb)
            {
                displayValue = Mathf.LinearToGammaSpace(displayValue);
            }

            return (byte)Mathf.RoundToInt(Mathf.Clamp01(displayValue) * 255f);
        }
    }
}
