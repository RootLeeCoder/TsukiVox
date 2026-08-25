using System;
using System.IO;
using System.Linq;
using TMPro;
using TsukiVox.AudioPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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
        private const string FlatTabletDarkPreviewPath = "Logs/QuestFlatTabletDarkPreview.png";
        private const string FlatTabletBrightPreviewPath = "Logs/QuestFlatTabletBrightPreview.png";
        private const string StageLightingPanelPreviewPath = "Logs/QuestStageLightingPanelPreview.png";
        private const string SettingsPanelPreviewPath = "Logs/QuestSettingsPanelPreview.png";
        private const string SettingsPanelDarkPreviewPath = "Logs/QuestSettingsPanelDarkPreview.png";
        private const string RoomAmbiencePanelPreviewPath = "Logs/QuestRoomAmbiencePanelPreview.png";

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
            ValidateSplitTruss(stageLighting);
            stageLighting.ApplyPreset(StageLightingPreset.Live);
            appShell.ConfigureSceneReferences();
            ValidateThemeLightingCoupling(room, stageLighting);
            var coffeeTable = FindRequiredDescendant(room.transform, "coffee table");
            var tabletPivot = FindRequiredDescendant(coffeeTable, QuestTabletTiltController.TabletPivotName);
            var tabletBody = FindRequiredDescendant(tabletPivot, "tablet body");
            var tabletScreen = FindRequiredDescendant(tabletPivot, "tablet screen");
            var moonstoneTop = FindRequiredDescendant(coffeeTable, "moonstone top");
            var tableRimFront = FindRequiredDescendant(coffeeTable, "table rim front");
            ValidateCoffeeTableStructure(coffeeTable, tabletBody, moonstoneTop);
            ValidateStableControlPanelShell(tabletBody, tabletScreen);
            ValidateCoffeeTableContrast(tabletBody, moonstoneTop, RoomTheme.Dark);
            ValidateStableTableFrame(tableRimFront);
            ValidateCoffeeTableControlSymmetry(coffeeTable);
            ValidateStageLightingInteraction(stageLighting);
            ValidatePointerIsolation(QuestUiPointer.EnsureScenePointer(), moonstoneTop);

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

            ValidateStableStoneRendering(moonstoneTop);

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
            var previousTabletRotation = tabletPivot.localRotation;
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

                tabletPivot.localRotation = Quaternion.Euler(90f, 0f, 0f);
                camera.transform.position = new Vector3(0f, 1.36f, -0.12f);
                camera.transform.LookAt(new Vector3(0f, 0.61f, 1.1f));
                camera.fieldOfView = 56f;
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, FlatTabletDarkPreviewPath);
                tabletPivot.localRotation = previousTabletRotation;

                camera.transform.position = new Vector3(0f, 1.12f, -0.05f);
                camera.transform.LookAt(new Vector3(0f, 2.82f, 2.28f));
                camera.fieldOfView = previousFieldOfView;
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, CeilingPreviewPath);

                room.ApplyTheme(RoomTheme.Bright);
                ValidateCelestialTheme(room, RoomTheme.Bright);
                ValidateCoffeeTableContrast(tabletBody, moonstoneTop, RoomTheme.Bright);
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

                tabletPivot.localRotation = Quaternion.Euler(90f, 0f, 0f);
                camera.transform.position = new Vector3(0f, 1.36f, -0.12f);
                camera.transform.LookAt(new Vector3(0f, 0.61f, 1.1f));
                camera.fieldOfView = 56f;
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, FlatTabletBrightPreviewPath);
                tabletPivot.localRotation = previousTabletRotation;

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
                RectTransform settingsPage = null;
                RectTransform roomAmbiencePage = null;
                for (var index = 0; index < pageRects.Length; index += 1)
                {
                    if (pageRects[index].parent == null || pageRects[index].parent.name != "Consumer UI" ||
                        !pageRects[index].name.EndsWith(" Page", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (pageRects[index].name == "Stage Lighting Page")
                    {
                        stageLightingPage = pageRects[index];
                    }
                    else if (pageRects[index].name == "Settings Page")
                    {
                        settingsPage = pageRects[index];
                    }
                    else if (pageRects[index].name == "Room Ambience Page")
                    {
                        roomAmbiencePage = pageRects[index];
                    }
                }

                if (stageLightingPage == null)
                {
                    throw new InvalidOperationException("Stage Lighting control page was not generated.");
                }
                if (settingsPage == null)
                {
                    throw new InvalidOperationException("Settings control page was not generated.");
                }
                if (roomAmbiencePage == null)
                {
                    throw new InvalidOperationException("Room ambience control page was not generated.");
                }

                SetPreviewPage(pageRects, "Stage Lighting Page");

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

                SetPreviewPage(pageRects, "Settings Page");
                ValidateSettingsPage(settingsPage, roomAmbiencePage);
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, SettingsPanelPreviewPath);

                SetPreviewPage(pageRects, "Room Ambience Page");
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, RoomAmbiencePanelPreviewPath);

                room.ApplyTheme(RoomTheme.Dark);
                SetPreviewPage(pageRects, "Settings Page");
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                linearTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                linearTexture.Apply(false, false);
                WriteSrgbPng(linearTexture, outputTexture, SettingsPanelDarkPreviewPath);
            }
            finally
            {
                room.ApplyTheme(previousTheme);
                camera.targetTexture = previousTarget;
                camera.stereoTargetEye = previousStereoTarget;
                camera.transform.position = previousPosition;
                camera.transform.rotation = previousRotation;
                camera.fieldOfView = previousFieldOfView;
                tabletPivot.localRotation = previousTabletRotation;
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
                    $"{Path.GetFullPath(FlatTabletDarkPreviewPath)}, {Path.GetFullPath(FlatTabletBrightPreviewPath)}, " +
                    $"{Path.GetFullPath(StageLightingPanelPreviewPath)}, {Path.GetFullPath(SettingsPanelPreviewPath)}, " +
                    $"{Path.GetFullPath(RoomAmbiencePanelPreviewPath)} and {Path.GetFullPath(SettingsPanelDarkPreviewPath)}.");
        }

        private static void ValidateCoffeeTableStructure(
            Transform coffeeTable,
            Transform tabletBody,
            Transform moonstoneTop)
        {
            if (coffeeTable.GetComponentsInChildren<Transform>(true).Any(item => item.name == "moonstone reveal"))
            {
                throw new InvalidOperationException("Coffee table still contains the legacy dark moonstone reveal.");
            }

            var tabletMaterial = GetRequiredMaterial(tabletBody);
            var stoneMaterial = GetRequiredMaterial(moonstoneTop);
            if (tabletMaterial.name != "V0.5 Tablet Body")
            {
                throw new InvalidOperationException($"Tablet body uses unexpected material {tabletMaterial.name}.");
            }

            if (stoneMaterial.name != "V0.5 Moonstone" || tabletMaterial == stoneMaterial)
            {
                throw new InvalidOperationException("Tablet body and coffee-table stone must use independent materials.");
            }

            if (!stoneMaterial.HasProperty("_StableLighting") || stoneMaterial.GetFloat("_StableLighting") < 0.999f)
            {
                throw new InvalidOperationException("Coffee-table stone must keep stable theme lighting.");
            }
        }

        private static void ValidateCoffeeTableContrast(
            Transform tabletBody,
            Transform moonstoneTop,
            RoomTheme theme)
        {
            var tabletColor = GetBaseColor(GetRequiredMaterial(tabletBody));
            var stoneColor = GetBaseColor(GetRequiredMaterial(moonstoneTop));
            var tabletValue = Mathf.Max(tabletColor.r, tabletColor.g, tabletColor.b);
            var stoneValue = Mathf.Max(stoneColor.r, stoneColor.g, stoneColor.b);
            if (Mathf.Abs(tabletValue - stoneValue) < 0.2f)
            {
                throw new InvalidOperationException($"{theme} tablet body does not contrast enough with the coffee-table stone.");
            }
        }

        private static void ValidateStableTableFrame(Transform tableRimFront)
        {
            var material = GetRequiredMaterial(tableRimFront);
            if (material.name != "V0.5 Table Frame" ||
                !material.HasProperty("_StableLighting") ||
                material.GetFloat("_StableLighting") < 0.999f)
            {
                throw new InvalidOperationException("The user-facing coffee-table rim must use stable theme lighting.");
            }
        }

        private static void ValidateStableControlPanelShell(Transform tabletBody, Transform tabletScreen)
        {
            var bodyMaterial = GetRequiredMaterial(tabletBody);
            var screenMaterial = GetRequiredMaterial(tabletScreen);
            if (!bodyMaterial.HasProperty("_StableLighting") || bodyMaterial.GetFloat("_StableLighting") < 0.999f ||
                !screenMaterial.HasProperty("_StableLighting") || screenMaterial.GetFloat("_StableLighting") < 0.999f)
            {
                throw new InvalidOperationException("The control-panel shell must keep stable theme lighting.");
            }
        }

        private static Transform FindRequiredDescendant(Transform root, string objectName)
        {
            var result = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == objectName);
            if (result == null)
            {
                throw new InvalidOperationException($"Required rendering object {objectName} was not generated.");
            }

            return result;
        }

        private static Material GetRequiredMaterial(Transform target)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null)
            {
                throw new InvalidOperationException($"Rendering object {target.name} has no material.");
            }

            return renderer.sharedMaterial;
        }

        private static Color GetBaseColor(Material material)
        {
            return material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
        }

        private static void ValidateStableStoneRendering(Transform moonstoneTop)
        {
            var stoneRenderer = moonstoneTop.GetComponent<MeshRenderer>();
            var stoneMaterial = GetRequiredMaterial(moonstoneTop);
            var bounds = stoneRenderer.bounds;
            var samplePoint = new Vector3(bounds.max.x - 0.16f, bounds.max.y, bounds.max.z - 0.18f);
            var previousActive = RenderTexture.active;
            var previousStability = stoneMaterial.GetFloat("_StableLighting");
            var cameraObject = new GameObject("Stable stone validation camera");
            var lightObject = new GameObject("Stable stone validation light");
            var renderTexture = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var texture = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 0.06f;
                camera.aspect = 1f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 0.7f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = true;
                camera.targetTexture = renderTexture;
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.transform.position = samplePoint + Vector3.up * 0.34f;
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                var testLight = lightObject.AddComponent<Light>();
                testLight.type = LightType.Point;
                testLight.range = 0.9f;
                testLight.intensity = 8f;
                testLight.shadows = LightShadows.None;
                testLight.renderMode = LightRenderMode.ForcePixel;
                testLight.transform.position = samplePoint + new Vector3(0.03f, 0.22f, 0.02f);

                stoneMaterial.SetFloat("_StableLighting", 0f);
                var unstableRed = RenderStoneSample(camera, testLight, renderTexture, texture, Color.red);
                var unstableBlue = RenderStoneSample(camera, testLight, renderTexture, texture, Color.blue);
                if (ColorDistance(unstableRed, unstableBlue) < 0.025f)
                {
                    throw new InvalidOperationException("Stable-stone validation light did not exercise dynamic material lighting.");
                }

                stoneMaterial.SetFloat("_StableLighting", 1f);
                var stableRed = RenderStoneSample(camera, testLight, renderTexture, texture, Color.red);
                var stableBlue = RenderStoneSample(camera, testLight, renderTexture, texture, Color.blue);
                if (ColorDistance(stableRed, stableBlue) > 0.002f)
                {
                    throw new InvalidOperationException("Coffee-table stone still changes color under dynamic lighting.");
                }
            }
            finally
            {
                stoneMaterial.SetFloat("_StableLighting", previousStability);
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static Color RenderStoneSample(
            Camera camera,
            Light testLight,
            RenderTexture renderTexture,
            Texture2D texture,
            Color lightColor)
        {
            testLight.color = lightColor;
            camera.Render();
            camera.Render();
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0, false);
            texture.Apply(false, false);

            var pixels = texture.GetPixels(24, 24, 16, 16);
            var color = Color.clear;
            for (var index = 0; index < pixels.Length; index += 1)
            {
                color += pixels[index];
            }

            return color / pixels.Length;
        }

        private static float ColorDistance(Color left, Color right)
        {
            return Mathf.Max(
                Mathf.Abs(left.r - right.r),
                Mathf.Abs(left.g - right.g),
                Mathf.Abs(left.b - right.b));
        }

        private static void ValidatePointerIsolation(QuestUiPointer pointer, Transform moonstoneTop)
        {
            var pointerLine = FindRequiredDescendant(pointer.transform, "Quest Controller UI Ray")
                .GetComponent<LineRenderer>();
            var reticleRenderer = FindRequiredDescendant(pointer.transform, "Quest UI Reticle")
                .GetComponent<MeshRenderer>();
            var stoneMaterial = GetRequiredMaterial(moonstoneTop);
            if (pointerLine == null || reticleRenderer == null)
            {
                throw new InvalidOperationException("Quest pointer renderers were not generated.");
            }

            if (pointerLine.sharedMaterial == null ||
                reticleRenderer.sharedMaterial == null ||
                pointerLine.sharedMaterial == stoneMaterial ||
                reticleRenderer.sharedMaterial == stoneMaterial)
            {
                throw new InvalidOperationException("Quest pointer material must be isolated from the coffee-table material.");
            }

            ValidatePointerRenderer(pointerLine);
            ValidatePointerRenderer(reticleRenderer);

            var pointerMaterialColor = GetBaseColor(pointerLine.sharedMaterial);
            var stoneColor = GetBaseColor(stoneMaterial);
            var applyPointerColor = typeof(QuestUiPointer).GetMethod(
                "ApplyPointerColor",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (applyPointerColor == null)
            {
                throw new InvalidOperationException("Quest pointer color isolation method is unavailable.");
            }

            var testColor = new Color(0.17f, 0.73f, 0.91f, 0.64f);
            applyPointerColor.Invoke(pointer, new object[] { testColor });
            var propertyBlock = new MaterialPropertyBlock();
            pointerLine.GetPropertyBlock(propertyBlock);
            if (!ColorsApproximately(propertyBlock.GetColor("_Color"), testColor) ||
                !ColorsApproximately(GetBaseColor(pointerLine.sharedMaterial), pointerMaterialColor) ||
                !ColorsApproximately(GetBaseColor(stoneMaterial), stoneColor))
            {
                throw new InvalidOperationException("Quest pointer state color leaked into a shared environment material.");
            }

            applyPointerColor.Invoke(pointer, new object[] { pointerMaterialColor });
        }

        private static void ValidatePointerRenderer(Renderer renderer)
        {
            if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off ||
                renderer.receiveShadows ||
                renderer.lightProbeUsage != UnityEngine.Rendering.LightProbeUsage.Off ||
                renderer.reflectionProbeUsage != UnityEngine.Rendering.ReflectionProbeUsage.Off ||
                renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
            {
                throw new InvalidOperationException($"Pointer renderer {renderer.name} can still affect environment lighting.");
            }
        }

        private static bool ColorsApproximately(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) < 0.0001f &&
                   Mathf.Abs(left.g - right.g) < 0.0001f &&
                   Mathf.Abs(left.b - right.b) < 0.0001f &&
                   Mathf.Abs(left.a - right.a) < 0.0001f;
        }

        private static void SetPreviewPage(RectTransform[] pages, string visiblePageName)
        {
            for (var index = 0; index < pages.Length; index += 1)
            {
                if (pages[index].parent == null || pages[index].parent.name != "Consumer UI" ||
                    !pages[index].name.EndsWith(" Page", StringComparison.Ordinal))
                {
                    continue;
                }

                var isVisible = pages[index].name == visiblePageName;
                pages[index].gameObject.SetActive(isVisible);
                var group = pages[index].GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = isVisible ? 1f : 0f;
                    group.interactable = isVisible;
                    group.blocksRaycasts = isVisible;
                }
            }

            Canvas.ForceUpdateCanvases();
            for (var index = 0; index < pages.Length; index += 1)
            {
                if (pages[index].name != visiblePageName)
                {
                    continue;
                }

                var switchVisuals = pages[index].GetComponentsInChildren<QuestUiSwitchVisual>(true);
                for (var switchIndex = 0; switchIndex < switchVisuals.Length; switchIndex += 1)
                {
                    switchVisuals[switchIndex].RefreshState(true);
                }
            }
        }

        private static void ValidateSettingsPage(RectTransform settingsPage, RectTransform roomAmbiencePage)
        {
            var legacyNames = new[]
            {
                "Request Mode Label",
                "Request Mode Value",
                "Room Ambience Label",
                "Ceiling Stars Label",
                "Ceiling Stars Switch",
                "Ceiling Aurora Label",
                "Ceiling Aurora Switch",
            };
            for (var index = 0; index < legacyNames.Length; index += 1)
            {
                var legacy = settingsPage.Find(legacyNames[index]);
                if (legacy != null && legacy.gameObject.activeSelf)
                {
                    throw new InvalidOperationException(
                        $"Legacy settings control '{legacyNames[index]}' is still active on the Settings Page.");
                }
            }

            var navigationNames = new[]
            {
                "Open Room Ambience",
                "Open Stage Lighting",
                "Open Voice Settings",
                "Open Mic Protection",
            };
            var previousY = float.PositiveInfinity;
            for (var index = 0; index < navigationNames.Length; index += 1)
            {
                var navigation = settingsPage.Find(navigationNames[index]);
                if (navigation == null || !navigation.gameObject.activeSelf || navigation.GetComponent<Button>() == null)
                {
                    throw new InvalidOperationException(
                        $"Settings navigation row '{navigationNames[index]}' is missing or not interactive.");
                }

                var rect = navigation.GetComponent<RectTransform>();
                if (rect == null || rect.sizeDelta.x < 991f || rect.sizeDelta.y < 74f)
                {
                    throw new InvalidOperationException(
                        $"Settings navigation row '{navigationNames[index]}' does not span the control width.");
                }

                if (rect.anchoredPosition.y >= previousY)
                {
                    throw new InvalidOperationException(
                        $"Settings navigation rows are not ordered: '{navigationNames[index]}'.");
                }

                var title = navigation.Find("Title");
                var description = navigation.Find("Description");
                var divider = navigation.Find("Divider");
                if (title == null || !title.gameObject.activeSelf || title.GetComponent<TMP_Text>() == null ||
                    description == null || !description.gameObject.activeSelf || description.GetComponent<TMP_Text>() == null ||
                    divider == null || divider.GetComponent<QuestUiSurface>() == null)
                {
                    throw new InvalidOperationException(
                        $"Settings navigation row '{navigationNames[index]}' is missing its title, description or divider.");
                }

                if (divider.gameObject.activeSelf == (index == 0))
                {
                    throw new InvalidOperationException(
                        $"Settings navigation row '{navigationNames[index]}' has an incorrect top divider state.");
                }

                var rowButton = navigation.GetComponent<Button>();
                if (rowButton.transition != Selectable.Transition.None)
                {
                    throw new InvalidOperationException(
                        $"Settings navigation row '{navigationNames[index]}' still has a hover transition.");
                }

                if (rowButton.targetGraphic is QuestUiSurface rowSurface && rowSurface.color.a > 0.001f)
                {
                    throw new InvalidOperationException(
                        $"Settings navigation row '{navigationNames[index]}' still has a visible background.");
                }

                var feedback = navigation.GetComponent<QuestUiButtonFeedback>();
                if (feedback != null && feedback.enabled)
                {
                    throw new InvalidOperationException(
                        $"Settings navigation row '{navigationNames[index]}' still has hover feedback enabled.");
                }

                var icon = navigation.Find("Icon")?.GetComponent<QuestUiIcon>();
                var chevron = navigation.Find("Chevron")?.GetComponent<QuestUiIcon>();
                if ((icon != null && icon.gameObject.activeSelf) || chevron == null || !chevron.gameObject.activeSelf)
                {
                    throw new InvalidOperationException(
                        $"Settings navigation row '{navigationNames[index]}' does not use the expected right chevron.");
                }

                previousY = rect.anchoredPosition.y;
            }

            var builtIn = settingsPage.Find("Play Built-in Default")?.GetComponent<RectTransform>();
            var stopBuiltIn = settingsPage.Find("Stop Built-in Default")?.GetComponent<RectTransform>();
            if (builtIn == null || stopBuiltIn == null || builtIn.anchoredPosition.y <= previousY ||
                stopBuiltIn.anchoredPosition.y <= previousY || builtIn.anchoredPosition.x <= stopBuiltIn.anchoredPosition.x)
            {
                throw new InvalidOperationException("Built-in video controls are not retained at the top-right of Settings.");
            }

            var exitButton = settingsPage.Find("Exit Application")?.GetComponent<Button>();
            var exitOutline = exitButton?.GetComponent<Outline>();
            if (exitButton == null || exitButton.transition != Selectable.Transition.None ||
                exitButton.targetGraphic is not QuestUiSurface exitSurface ||
                exitSurface.color.r <= exitSurface.color.g || exitSurface.color.r - exitSurface.color.g >= 0.15f ||
                exitOutline == null || !exitOutline.enabled || exitOutline.effectColor.a < 0.9f ||
                exitOutline.effectColor.r <= exitOutline.effectColor.g)
            {
                throw new InvalidOperationException("Exit Application is not using the restrained danger treatment.");
            }

            var roomRequiredNames = new[]
            {
                "Room Ambience Label",
                "Ceiling Stars Label",
                "Ceiling Stars Switch",
                "Ceiling Aurora Label",
                "Ceiling Aurora Switch",
            };
            for (var index = 0; index < roomRequiredNames.Length; index += 1)
            {
                var roomControl = roomAmbiencePage.Find(roomRequiredNames[index]);
                if (roomControl == null || !roomControl.gameObject.activeSelf)
                {
                    throw new InvalidOperationException(
                        $"Room ambience page is missing active control '{roomRequiredNames[index]}'.");
                }
            }
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

        private static void ValidateSplitTruss(QuestStageLightingPrototype stageLighting)
        {
            var rig = stageLighting.transform.Find(QuestStageLightingPrototype.RigRootName);
            if (rig == null)
            {
                throw new InvalidOperationException("Stage lighting rig was not generated.");
            }

            var requiredParts = new[]
            {
                "left truss upper rail",
                "left truss lower rail",
                "right truss upper rail",
                "right truss lower rail",
            };
            for (var index = 0; index < requiredParts.Length; index += 1)
            {
                if (rig.Find(requiredParts[index]) == null)
                {
                    throw new InvalidOperationException($"Stage lighting rig is missing '{requiredParts[index]}'.");
                }
            }

            if (rig.Find("truss upper rail") != null || rig.Find("truss lower rail") != null)
            {
                throw new InvalidOperationException("Legacy full-width truss rails still obstruct the ceiling logo.");
            }
        }

        private static void ValidateCoffeeTableControlSymmetry(Transform coffeeTable)
        {
            var leftHousing = FindRequiredDescendant(coffeeTable, "room controls switch housing");
            var rightHousing = FindRequiredDescendant(coffeeTable, "tablet tilt switch housing");
            var leftBounds = leftHousing.GetComponent<MeshRenderer>()?.bounds ?? default;
            var rightBounds = rightHousing.GetComponent<MeshRenderer>()?.bounds ?? default;
            if (Mathf.Abs(leftBounds.size.x - rightBounds.size.x) > 0.01f ||
                Mathf.Abs(leftBounds.center.x + rightBounds.center.x) > 0.01f)
            {
                throw new InvalidOperationException("Coffee-table switch housings are not horizontally symmetric.");
            }

            var themeCanvas = GameObject.Find(QuestRoomThemeController.SwitchCanvasName);
            var lightingCanvas = GameObject.Find(QuestStageLightingSwitchController.SwitchCanvasName);
            var tiltCanvas = GameObject.Find(QuestTabletTiltController.SwitchCanvasName);
            if (themeCanvas == null || lightingCanvas == null || tiltCanvas == null)
            {
                throw new InvalidOperationException("One or more coffee-table switch canvases were not generated.");
            }

            var leftButtons = new[]
            {
                themeCanvas.transform.Find("Dock/Theme 0"),
                themeCanvas.transform.Find("Dock/Theme 1"),
                lightingCanvas.transform.Find("Dock/Lighting 0"),
                lightingCanvas.transform.Find("Dock/Lighting 1"),
            };
            var rightButtons = new[]
            {
                tiltCanvas.transform.Find("Dock/Angle 0"),
                tiltCanvas.transform.Find("Dock/Angle 1"),
                tiltCanvas.transform.Find("Dock/Angle 2"),
                tiltCanvas.transform.Find("Dock/Angle 3"),
            };
            for (var index = 0; index < leftButtons.Length; index += 1)
            {
                var left = leftButtons[index];
                var right = rightButtons[rightButtons.Length - 1 - index];
                if (left == null || right == null ||
                    Mathf.Abs(left.position.x + right.position.x) > 0.002f ||
                    Mathf.Abs(left.position.y - right.position.y) > 0.002f ||
                    Mathf.Abs(left.position.z - right.position.z) > 0.002f)
                {
                    throw new InvalidOperationException("Coffee-table switch buttons are not arranged symmetrically.");
                }
            }
        }

        private static void ValidateStageLightingInteraction(QuestStageLightingPrototype stageLighting)
        {
            var rig = stageLighting.transform.Find(QuestStageLightingPrototype.RigRootName);
            QuestStageLightingFixtureTarget firstTarget = null;
            for (var index = 0; index < 6; index += 1)
            {
                var aimPivot = rig?.Find($"fixture {index}/aim pivot");
                var hitCollider = aimPivot?.GetComponent<BoxCollider>();
                var target = aimPivot?.GetComponent<QuestStageLightingFixtureTarget>();
                if (hitCollider == null || target == null || !hitCollider.enabled || hitCollider.isTrigger)
                {
                    throw new InvalidOperationException($"Stage-lighting fixture {index} has no direct pointer target.");
                }

                firstTarget ??= target;
            }

            var initialState = stageLighting.LightingEnabled;
            var pointer = QuestUiPointer.EnsureScenePointer();
            var raycastTargets = typeof(QuestUiPointer).GetMethod(
                "TryRaycastTargets",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var targetTransform = firstTarget.transform;
            var raycastArguments = new object[]
            {
                new Ray(targetTransform.position - targetTransform.forward, targetTransform.forward),
                Vector3.zero,
                null,
                default(UnityEngine.EventSystems.RaycastResult),
            };
            if (raycastTargets == null ||
                !(bool)raycastTargets.Invoke(pointer, raycastArguments) ||
                raycastArguments[2] as GameObject != firstTarget.gameObject)
            {
                throw new InvalidOperationException("The controller pointer did not resolve the stage-lighting fixture target.");
            }

            firstTarget.OnPointerClick(null);
            if (stageLighting.LightingEnabled == initialState)
            {
                throw new InvalidOperationException("Clicking a stage-lighting fixture did not toggle the master state.");
            }

            firstTarget.OnPointerClick(null);
            var switchController = UnityEngine.Object.FindAnyObjectByType<QuestStageLightingSwitchController>();
            if (stageLighting.LightingEnabled != initialState ||
                switchController == null ||
                switchController.LightingEnabled != initialState)
            {
                throw new InvalidOperationException("Stage-lighting controls did not return to a synchronized state.");
            }
        }

        private static void ValidateThemeLightingCoupling(
            QuestKtvRoomPrototype room,
            QuestStageLightingPrototype stageLighting)
        {
            room.ApplyTheme(RoomTheme.Bright);
            if (stageLighting.LightingEnabled)
            {
                throw new InvalidOperationException("Daylight theme did not turn off the stage lighting.");
            }

            room.ApplyTheme(RoomTheme.Dark);
            if (!stageLighting.LightingEnabled)
            {
                throw new InvalidOperationException("Moonlight theme did not turn on the stage lighting.");
            }
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
