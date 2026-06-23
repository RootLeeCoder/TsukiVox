using System.IO;
using TsukiVox.AudioPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype.Editor
{
    public static class CreateAudioPrototypeScene
    {
        private const string ScenePath = "Assets/Scenes/AudioPrototype.unity";

        [MenuItem("TsukiVox/Create Audio Prototype Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.18f, 0.2f, 0.22f);

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.05f);
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 40f;
            cameraObject.transform.position = new Vector3(0f, 1.55f, -2.9f);
            cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            cameraObject.tag = "MainCamera";

            var lightObject = new GameObject("Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            CreateMeters();

            var audioObject = new GameObject("Quest Audio Prototype");
            var source = audioObject.AddComponent<AudioSource>();
            var reverb = audioObject.AddComponent<AudioReverbFilter>();
            var echo = audioObject.AddComponent<AudioEchoFilter>();
            var highPass = audioObject.AddComponent<AudioHighPassFilter>();
            var lowPass = audioObject.AddComponent<AudioLowPassFilter>();
            var prototype = audioObject.AddComponent<QuestAudioPrototype>();

            var canvasObject = CreateCanvas(camera);
            var panel = CreatePanel(canvasObject.transform);
            var title = CreateText(panel.transform, "TsukiVox Quest Audio Prototype", 30, FontStyle.Bold);
            title.rectTransform.anchoredPosition = new Vector2(0f, 205f);

            var status = CreateText(panel.transform, "Ready.", 18, FontStyle.Normal);
            status.rectTransform.anchoredPosition = new Vector2(0f, 152f);

            var preset = CreateText(panel.transform, "Preset", 20, FontStyle.Bold);
            preset.rectTransform.anchoredPosition = new Vector2(0f, 106f);

            var metrics = CreateText(panel.transform, "Metrics", 16, FontStyle.Normal);
            metrics.alignment = TextAnchor.UpperLeft;
            metrics.rectTransform.sizeDelta = new Vector2(680f, 116f);
            metrics.rectTransform.anchoredPosition = new Vector2(0f, 23f);

            var inputSlider = CreateSlider(panel.transform, "Input Level", new Vector2(-190f, -78f));
            var outputSlider = CreateSlider(panel.transform, "Output Level", new Vector2(190f, -78f));
            var volumeSlider = CreateSlider(panel.transform, "Monitor Volume", new Vector2(0f, -138f));
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1.4f;
            volumeSlider.value = 1f;

            var startButton = CreateButton(panel.transform, "Start Mic", new Vector2(-270f, -214f));
            var stopButton = CreateButton(panel.transform, "Stop", new Vector2(-90f, -214f));
            var previousPreset = CreateButton(panel.transform, "Prev", new Vector2(90f, -214f));
            var nextPreset = CreateButton(panel.transform, "Next", new Vector2(270f, -214f));
            var monitorToggle = CreateToggle(panel.transform, "Monitor", new Vector2(-220f, -276f), true);
            var nativeToggle = CreateToggle(panel.transform, "Native", new Vector2(0f, -276f), false);
            var safetyToggle = CreateToggle(panel.transform, "Safety", new Vector2(220f, -276f), true);
            CreateEventSystem();
            CreateQuestPointer(canvasObject);

            var so = new SerializedObject(prototype);
            so.FindProperty("monitorSource").objectReferenceValue = source;
            so.FindProperty("reverbFilter").objectReferenceValue = reverb;
            so.FindProperty("echoFilter").objectReferenceValue = echo;
            so.FindProperty("highPassFilter").objectReferenceValue = highPass;
            so.FindProperty("lowPassFilter").objectReferenceValue = lowPass;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("metricsText").objectReferenceValue = metrics;
            so.FindProperty("presetText").objectReferenceValue = preset;
            so.FindProperty("monitorVolumeSlider").objectReferenceValue = volumeSlider;
            so.FindProperty("inputLevelSlider").objectReferenceValue = inputSlider;
            so.FindProperty("outputLevelSlider").objectReferenceValue = outputSlider;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("stopButton").objectReferenceValue = stopButton;
            so.FindProperty("previousPresetButton").objectReferenceValue = previousPreset;
            so.FindProperty("nextPresetButton").objectReferenceValue = nextPreset;
            so.FindProperty("monitorToggle").objectReferenceValue = monitorToggle;
            so.FindProperty("nativeToggle").objectReferenceValue = nativeToggle;
            so.FindProperty("safetyToggle").objectReferenceValue = safetyToggle;
            so.FindProperty("monitorVolume").floatValue = 1f;
            so.FindProperty("preferNativeOboeBackend").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Debug.Log($"Created {ScenePath}");
        }

        private static void CreateMeters()
        {
            CreateMeter("Input Meter Preview", new Vector3(-0.72f, 1.1f, 0.75f), new Color(0.2f, 0.92f, 0.8f));
            CreateMeter("Output Meter Preview", new Vector3(0.72f, 1.1f, 0.75f), new Color(1f, 0.78f, 0.28f));
        }

        private static void CreateMeter(string name, Vector3 position, Color color)
        {
            var meter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meter.name = name;
            meter.transform.position = position;
            meter.transform.localScale = new Vector3(0.22f, 1.25f, 0.04f);
            var renderer = meter.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Standard"))
            {
                color = color * 0.65f,
            };
            renderer.sharedMaterial.SetColor("_EmissionColor", color * 0.35f);
            renderer.sharedMaterial.EnableKeyword("_EMISSION");
        }

        private static GameObject CreateCanvas(Camera camera)
        {
            var canvasObject = new GameObject("Prototype Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 12f;
            canvasObject.AddComponent<GraphicRaycaster>();
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1280f, 720f);
            rect.localScale = Vector3.one;
            return canvasObject;
        }

        private static RectTransform CreatePanel(Transform parent)
        {
            var panelObject = new GameObject("Panel");
            panelObject.transform.SetParent(parent, false);
            var rect = panelObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(820f, 650f);
            var image = panelObject.AddComponent<Image>();
            image.color = new Color(0.04f, 0.055f, 0.062f, 0.92f);
            return rect;
        }

        private static Text CreateText(Transform parent, string label, int size, FontStyle style)
        {
            var textObject = new GameObject(label);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = new Color(0.93f, 0.97f, 0.98f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.sizeDelta = new Vector2(740f, 64f);
            return text;
        }

        private static Slider CreateSlider(Transform parent, string label, Vector2 position)
        {
            var root = new GameObject(label);
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 52f);
            rect.anchoredPosition = position;

            var labelText = CreateText(root.transform, label, 13, FontStyle.Normal);
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.rectTransform.sizeDelta = new Vector2(320f, 22f);
            labelText.rectTransform.anchoredPosition = new Vector2(0f, 17f);

            var background = new GameObject("Background");
            background.transform.SetParent(root.transform, false);
            var backgroundRect = background.AddComponent<RectTransform>();
            backgroundRect.sizeDelta = new Vector2(320f, 14f);
            backgroundRect.anchoredPosition = new Vector2(0f, -10f);
            var backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.16f, 0.18f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(background.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.26f, 0.95f, 0.78f);

            var slider = root.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.targetGraphic = backgroundImage;
            slider.fillRect = fillRect;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150f, 52f);
            rect.anchoredPosition = position;
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.1f, 0.16f, 0.18f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateSelectableColors();

            var text = CreateText(buttonObject.transform, label, 16, FontStyle.Bold);
            text.rectTransform.sizeDelta = rect.sizeDelta;
            text.rectTransform.anchoredPosition = Vector2.zero;
            return button;
        }

        private static Toggle CreateToggle(Transform parent, string label, Vector2 position, bool value)
        {
            var toggleObject = new GameObject(label);
            toggleObject.transform.SetParent(parent, false);
            var rect = toggleObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(170f, 38f);
            rect.anchoredPosition = position;

            var background = new GameObject("Checkmark Background");
            background.transform.SetParent(toggleObject.transform, false);
            var backgroundRect = background.AddComponent<RectTransform>();
            backgroundRect.sizeDelta = new Vector2(28f, 28f);
            backgroundRect.anchoredPosition = new Vector2(-58f, 0f);
            var backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.16f, 0.18f);

            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform, false);
            var checkmarkRect = checkmark.AddComponent<RectTransform>();
            checkmarkRect.sizeDelta = new Vector2(18f, 18f);
            checkmarkRect.anchoredPosition = Vector2.zero;
            var checkmarkImage = checkmark.AddComponent<Image>();
            checkmarkImage.color = new Color(0.28f, 0.95f, 0.72f);

            var text = CreateText(toggleObject.transform, label, 16, FontStyle.Normal);
            text.alignment = TextAnchor.MiddleLeft;
            text.rectTransform.sizeDelta = new Vector2(116f, 36f);
            text.rectTransform.anchoredPosition = new Vector2(22f, 0f);

            var toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            toggle.isOn = value;
            toggle.colors = CreateSelectableColors();
            return toggle;
        }

        private static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static void CreateQuestPointer(GameObject canvasObject)
        {
            var pointerObject = new GameObject("Quest UI Pointer");
            var pointer = pointerObject.AddComponent<QuestUiPointer>();
            var so = new SerializedObject(pointer);
            so.FindProperty("targetCanvas").objectReferenceValue = canvasObject.GetComponent<Canvas>();
            so.FindProperty("raycaster").objectReferenceValue = canvasObject.GetComponent<GraphicRaycaster>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ColorBlock CreateSelectableColors()
        {
            return new ColorBlock
            {
                normalColor = new Color(0.1f, 0.16f, 0.18f),
                highlightedColor = new Color(0.16f, 0.3f, 0.32f),
                pressedColor = new Color(0.28f, 0.46f, 0.42f),
                selectedColor = new Color(0.13f, 0.22f, 0.24f),
                disabledColor = new Color(0.08f, 0.1f, 0.11f, 0.55f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        private static void AddSceneToBuildSettings(string path)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(path, true),
            };
        }
    }
}
