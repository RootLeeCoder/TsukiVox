using System;
using System.IO;
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
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.stereoTargetEye = previousStereoTarget;
                camera.transform.position = previousPosition;
                camera.transform.rotation = previousRotation;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(linearTexture);
                UnityEngine.Object.DestroyImmediate(outputTexture);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            Debug.Log(
                $"[TsukiVox URP] Captured rendering previews at {Path.GetFullPath(PreviewPath)} " +
                $"and {Path.GetFullPath(LoungePreviewPath)}.");
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
