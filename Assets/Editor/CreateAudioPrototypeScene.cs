using System.IO;
using TsukiVox.AudioPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SpatialTracking;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TsukiVox.AudioPrototype.Editor
{
    public static class CreateAudioPrototypeScene
    {
        private const string ScenePath = "Assets/Scenes/AudioPrototype.unity";

        [InitializeOnLoadMethod]
        private static void ScheduleRoomDesignRefresh()
        {
            EditorApplication.delayCall += RefreshOpenPrototypeSceneIfNeeded;
        }

        private static void RefreshOpenPrototypeSceneIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                return;
            }

            var room = Object.FindAnyObjectByType<QuestKtvRoomPrototype>();
            if (room == null || !room.NeedsDesignRefresh)
            {
                return;
            }

            room.ConfigureSceneReferences();
            GenerateRoomLightmapUvs(room);
            EditorUtility.SetDirty(room);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"Updated {ScenePath} to KTV room design revision {QuestKtvRoomPrototype.CurrentDesignRevision}.");
        }

        [MenuItem("TsukiVox/Create Audio Prototype Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            PlayerSettings.colorSpace = ColorSpace.Linear;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color32(38, 41, 50, 255);

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(8, 10, 15, 255);
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 40f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            cameraObject.transform.position = new Vector3(0f, 1.55f, 0f);
            cameraObject.transform.rotation = Quaternion.identity;
            cameraObject.tag = "MainCamera";
            var poseDriver = cameraObject.AddComponent<TrackedPoseDriver>();
            poseDriver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
            poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            var audioObject = new GameObject("Quest Audio Prototype");
            var source = audioObject.AddComponent<AudioSource>();
            var reverb = audioObject.AddComponent<AudioReverbFilter>();
            var echo = audioObject.AddComponent<AudioEchoFilter>();
            var highPass = audioObject.AddComponent<AudioHighPassFilter>();
            var lowPass = audioObject.AddComponent<AudioLowPassFilter>();
            var prototype = audioObject.AddComponent<QuestAudioPrototype>();

            var canvasObject = CreateCanvas(camera);
            var panel = CreatePanel(canvasObject.transform);
            var title = CreateText(panel.transform, "TsukiVox Quest Prototype", 28, FontStyle.Bold);
            title.rectTransform.anchoredPosition = new Vector2(0f, 288f);

            var audioTitle = CreateText(panel.transform, "V0.1 Audio", 20, FontStyle.Bold);
            audioTitle.rectTransform.sizeDelta = new Vector2(500f, 34f);
            audioTitle.rectTransform.anchoredPosition = new Vector2(-300f, 244f);

            var playlistTitle = CreateText(panel.transform, "Direct Request", 20, FontStyle.Bold);
            playlistTitle.rectTransform.sizeDelta = new Vector2(500f, 34f);
            playlistTitle.rectTransform.anchoredPosition = new Vector2(300f, 244f);

            var status = CreateText(panel.transform, "Ready.", 16, FontStyle.Normal);
            status.rectTransform.sizeDelta = new Vector2(520f, 62f);
            status.rectTransform.anchoredPosition = new Vector2(-300f, 198f);

            var preset = CreateText(panel.transform, "Preset", 18, FontStyle.Bold);
            preset.rectTransform.sizeDelta = new Vector2(520f, 34f);
            preset.rectTransform.anchoredPosition = new Vector2(-300f, 146f);

            var metrics = CreateText(panel.transform, "Metrics", 14, FontStyle.Normal);
            metrics.alignment = TextAnchor.UpperLeft;
            metrics.rectTransform.sizeDelta = new Vector2(520f, 142f);
            metrics.rectTransform.anchoredPosition = new Vector2(-300f, 57f);

            var inputSlider = CreateSlider(panel.transform, "Input Level", new Vector2(-430f, -70f));
            var outputSlider = CreateSlider(panel.transform, "Output Level", new Vector2(-170f, -70f));
            var volumeSlider = CreateSlider(panel.transform, "Monitor Volume", new Vector2(-300f, -130f));
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1.4f;
            volumeSlider.value = 1f;

            var startButton = CreateButton(panel.transform, "Start Mic", new Vector2(-495f, -206f));
            var stopButton = CreateButton(panel.transform, "Stop", new Vector2(-365f, -206f));
            var previousPreset = CreateButton(panel.transform, "Prev", new Vector2(-235f, -206f));
            var nextPreset = CreateButton(panel.transform, "Next", new Vector2(-105f, -206f));
            var monitorToggle = CreateToggle(panel.transform, "Monitor", new Vector2(-475f, -270f), true);
            var nativeToggle = CreateToggle(panel.transform, "Native", new Vector2(-300f, -270f), false);
            var safetyToggle = CreateToggle(panel.transform, "Safety", new Vector2(-125f, -270f), true);

            var playlistObject = new GameObject("Quest Playlist Prototype");
            var playlistPrototype = playlistObject.AddComponent<QuestPlaylistPrototype>();
            var connection = CreateText(panel.transform, "Direct request ready", 14, FontStyle.Normal);
            connection.alignment = TextAnchor.UpperLeft;
            connection.rectTransform.sizeDelta = new Vector2(520f, 58f);
            connection.rectTransform.anchoredPosition = new Vector2(300f, 200f);

            var currentSong = CreateText(panel.transform, "Current: no song selected.", 14, FontStyle.Normal);
            currentSong.alignment = TextAnchor.UpperLeft;
            currentSong.rectTransform.sizeDelta = new Vector2(520f, 102f);
            currentSong.rectTransform.anchoredPosition = new Vector2(300f, 118f);

            var queue = CreateText(panel.transform, "Queue 0 item(s)", 14, FontStyle.Normal);
            queue.alignment = TextAnchor.UpperLeft;
            queue.rectTransform.sizeDelta = new Vector2(520f, 66f);
            queue.rectTransform.anchoredPosition = new Vector2(300f, 20f);

            var playableUrl = CreateText(panel.transform, "Playable URL: none", 12, FontStyle.Normal);
            playableUrl.alignment = TextAnchor.UpperLeft;
            playableUrl.rectTransform.sizeDelta = new Vector2(520f, 62f);
            playableUrl.rectTransform.anchoredPosition = new Vector2(300f, -54f);

            var playPause = CreateButton(panel.transform, "Direct Play", "Play", new Vector2(95f, -166f));
            var playlistPrevious = CreateButton(panel.transform, "Direct Previous", "Prev", new Vector2(225f, -166f));
            var playlistNext = CreateButton(panel.transform, "Direct Next", "Next", new Vector2(355f, -166f));
            var replay = CreateButton(panel.transform, "Direct Replay", "Replay", new Vector2(485f, -166f));
            var copyVideoDebug = CreateButton(panel.transform, "Copy Debug", new Vector2(300f, -292f), new Vector2(150f, 38f), 13);
            var copyAppDebug = CreateButton(panel.transform, "Copy App Debug", new Vector2(522f, -300f), new Vector2(176f, 42f), 13);
            var videoDebugToggle = CreateToggle(panel.transform, "Video Debug", new Vector2(-240f, -300f), false);
            CreateEventSystem();
            CreateQuestPointer(canvasObject);
            CreateVideoScreen(playlistPrototype, copyVideoDebug);
            CreateAppShell(canvasObject, panel, prototype, playlistPrototype, copyAppDebug, videoDebugToggle);
            CreateKtvRoom(prototype);
            CreateHandheldProps(prototype);

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

            var playlistSo = new SerializedObject(playlistPrototype);
            playlistSo.FindProperty("connectionText").objectReferenceValue = connection;
            playlistSo.FindProperty("currentSongText").objectReferenceValue = currentSong;
            playlistSo.FindProperty("queueText").objectReferenceValue = queue;
            playlistSo.FindProperty("playableUrlText").objectReferenceValue = playableUrl;
            playlistSo.FindProperty("playPauseButton").objectReferenceValue = playPause;
            playlistSo.FindProperty("previousButton").objectReferenceValue = playlistPrevious;
            playlistSo.FindProperty("nextButton").objectReferenceValue = playlistNext;
            playlistSo.FindProperty("replayButton").objectReferenceValue = replay;
            playlistSo.ApplyModifiedPropertiesWithoutUndo();

            QuestAppShellPrototype.EnsureSceneShell();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Debug.Log($"Created {ScenePath}");
        }

        private static GameObject CreateCanvas(Camera camera)
        {
            var canvasObject = new GameObject("Prototype Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = QuestAppShellPrototype.ControlPanelSize;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 12f;
            var raycaster = canvasObject.AddComponent<GraphicRaycaster>();
            raycaster.ignoreReversedGraphics = false;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = QuestAppShellPrototype.ControlPanelSize;
            rect.position = QuestAppShellPrototype.ControlPanelWorldPosition;
            rect.rotation = QuestAppShellPrototype.ControlPanelWorldRotation;
            rect.localScale = QuestAppShellPrototype.ControlPanelWorldScale;
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
            rect.sizeDelta = new Vector2(1240f, 690f);
            var image = panelObject.AddComponent<Image>();
            image.color = new Color32(11, 13, 18, 235);
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
            text.color = new Color32(242, 239, 232, 255);
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
            backgroundImage.color = new Color32(36, 39, 46, 255);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(background.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color32(216, 196, 157, 255);

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
            return CreateButton(parent, label, position, new Vector2(116f, 48f), 16);
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size, int fontSize)
        {
            return CreateButton(parent, label, label, position, size, fontSize);
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
        {
            return CreateButton(parent, name, label, position, new Vector2(116f, 48f), 16);
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, int fontSize)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color32(26, 29, 36, 255);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateSelectableColors();

            var text = CreateText(buttonObject.transform, label, fontSize, FontStyle.Bold);
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
            backgroundImage.color = new Color32(36, 39, 46, 255);

            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform, false);
            var checkmarkRect = checkmark.AddComponent<RectTransform>();
            checkmarkRect.sizeDelta = new Vector2(18f, 18f);
            checkmarkRect.anchoredPosition = Vector2.zero;
            var checkmarkImage = checkmark.AddComponent<Image>();
            checkmarkImage.color = new Color32(216, 196, 157, 255);

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
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
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

        private static void CreateVideoScreen(QuestPlaylistPrototype playlistPrototype, Button copyDebugButton)
        {
            var videoObject = new GameObject("Quest Video Screen Prototype");
            videoObject.AddComponent<AudioSource>();
            var videoPlayer = videoObject.AddComponent<VideoPlayer>();
            videoPlayer.aspectRatio = VideoAspectRatio.Stretch;
            var videoScreen = videoObject.AddComponent<QuestVideoScreenPrototype>();

            var so = new SerializedObject(videoScreen);
            so.FindProperty("playlistPrototype").objectReferenceValue = playlistPrototype;
            so.FindProperty("copyDebugButton").objectReferenceValue = copyDebugButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateKtvRoom(QuestAudioPrototype audioPrototype)
        {
            var room = QuestKtvRoomPrototype.EnsureSceneRoom();
            var so = new SerializedObject(room);
            so.FindProperty("audioPrototype").objectReferenceValue = audioPrototype;
            so.FindProperty("videoScreenPrototype").objectReferenceValue = Object.FindAnyObjectByType<QuestVideoScreenPrototype>();
            so.ApplyModifiedPropertiesWithoutUndo();
            room.ConfigureSceneReferences();
            GenerateRoomLightmapUvs(room);
        }

        private static void CreateHandheldProps(QuestAudioPrototype audioPrototype)
        {
            var props = QuestHandheldPropsPrototype.EnsureSceneProps();
            var so = new SerializedObject(props);
            so.FindProperty("audioPrototype").objectReferenceValue = audioPrototype;
            so.ApplyModifiedPropertiesWithoutUndo();
            props.ConfigureSceneReferences();
        }

        public static QuestHandheldPropsPrototype EnsureHandheldPropsInCurrentScene()
        {
            var props = QuestHandheldPropsPrototype.EnsureSceneProps();
            var so = new SerializedObject(props);
            so.FindProperty("audioPrototype").objectReferenceValue = Object.FindAnyObjectByType<QuestAudioPrototype>();
            so.ApplyModifiedPropertiesWithoutUndo();
            props.ConfigureSceneReferences();
            EditorUtility.SetDirty(props);
            EditorSceneManager.MarkSceneDirty(props.gameObject.scene);
            return props;
        }

        public static QuestKtvRoomPrototype EnsureKtvRoomInCurrentScene(QuestVideoScreenPrototype videoScreen)
        {
            var room = QuestKtvRoomPrototype.EnsureSceneRoom();
            var so = new SerializedObject(room);
            so.FindProperty("audioPrototype").objectReferenceValue = Object.FindAnyObjectByType<QuestAudioPrototype>();
            so.FindProperty("videoScreenPrototype").objectReferenceValue = videoScreen;
            so.ApplyModifiedPropertiesWithoutUndo();
            room.ConfigureSceneReferences();
            GenerateRoomLightmapUvs(room);
            EditorUtility.SetDirty(room);
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            return room;
        }

        private static void GenerateRoomLightmapUvs(QuestKtvRoomPrototype room)
        {
            var meshFilters = room.GetComponentsInChildren<MeshFilter>(true);
            for (var index = 0; index < meshFilters.Length; index += 1)
            {
                var mesh = meshFilters[index].sharedMesh;
                if (mesh == null || !mesh.name.EndsWith(" beveled mesh"))
                {
                    continue;
                }

                Unwrapping.GenerateSecondaryUVSet(mesh);
            }
        }

        private static void CreateAppShell(
            GameObject canvasObject,
            RectTransform panel,
            QuestAudioPrototype audioPrototype,
            QuestPlaylistPrototype playlistPrototype,
            Button copyAppDebugButton,
            Toggle videoDebugToggle)
        {
            var shellObject = new GameObject("Quest App Shell Prototype");
            var shell = shellObject.AddComponent<QuestAppShellPrototype>();
            var so = new SerializedObject(shell);
            so.FindProperty("controlCanvas").objectReferenceValue = canvasObject.GetComponent<Canvas>();
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("copyAppDebugButton").objectReferenceValue = copyAppDebugButton;
            so.FindProperty("videoDebugToggle").objectReferenceValue = videoDebugToggle;
            so.FindProperty("audioPrototype").objectReferenceValue = audioPrototype;
            so.FindProperty("playlistPrototype").objectReferenceValue = playlistPrototype;
            so.FindProperty("videoScreenPrototype").objectReferenceValue = Object.FindAnyObjectByType<QuestVideoScreenPrototype>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static QuestPlaylistPrototype EnsurePlaylistPrototypeInCurrentScene()
        {
            var playlistPrototype = Object.FindAnyObjectByType<QuestPlaylistPrototype>();
            if (playlistPrototype == null)
            {
                playlistPrototype = QuestPlaylistPrototype.EnsureScenePrototype();
            }

            return playlistPrototype;
        }

        public static QuestVideoScreenPrototype EnsureVideoScreenPrototypeInCurrentScene(QuestPlaylistPrototype playlistPrototype)
        {
            var videoScreen = Object.FindAnyObjectByType<QuestVideoScreenPrototype>();
            if (videoScreen == null)
            {
                var videoObject = new GameObject("Quest Video Screen Prototype");
                videoScreen = videoObject.AddComponent<QuestVideoScreenPrototype>();
            }

            if (playlistPrototype != null)
            {
                var so = new SerializedObject(videoScreen);
                so.FindProperty("playlistPrototype").objectReferenceValue = playlistPrototype;
                so.FindProperty("copyDebugButton").objectReferenceValue = FindOrCreateCopyDebugButton();
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(videoScreen);
                EditorSceneManager.MarkSceneDirty(videoScreen.gameObject.scene);
            }

            return videoScreen;
        }

        public static QuestAppShellPrototype EnsureAppShellInCurrentScene(
            QuestPlaylistPrototype playlistPrototype,
            QuestVideoScreenPrototype videoScreen)
        {
            var shell = Object.FindAnyObjectByType<QuestAppShellPrototype>();
            if (shell == null)
            {
                var shellObject = new GameObject("Quest App Shell Prototype");
                shell = shellObject.AddComponent<QuestAppShellPrototype>();
            }

            var so = new SerializedObject(shell);
            so.FindProperty("playlistPrototype").objectReferenceValue = playlistPrototype;
            so.FindProperty("videoScreenPrototype").objectReferenceValue = videoScreen;
            so.FindProperty("audioPrototype").objectReferenceValue = Object.FindAnyObjectByType<QuestAudioPrototype>();
            so.ApplyModifiedPropertiesWithoutUndo();
            shell.ConfigureSceneReferences();
            return shell;
        }

        private static Button FindOrCreateCopyDebugButton()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            for (var i = 0; i < canvases.Length; i += 1)
            {
                if (canvases[i].name != "Prototype Canvas")
                {
                    continue;
                }

                var panel = canvases[i].transform.Find("Panel");
                if (panel != null)
                {
                    var existing = panel.Find("Copy Debug");
                    if (existing != null && existing.TryGetComponent<Button>(out var existingButton))
                    {
                        return existingButton;
                    }

                    return CreateButton(panel, "Copy Debug", new Vector2(300f, -292f), new Vector2(150f, 38f), 13);
                }
            }

            return null;
        }

        private static ColorBlock CreateSelectableColors()
        {
            return new ColorBlock
            {
                normalColor = new Color32(26, 29, 36, 255),
                highlightedColor = new Color32(240, 230, 210, 255),
                pressedColor = new Color32(188, 167, 126, 255),
                selectedColor = new Color32(226, 207, 169, 255),
                disabledColor = new Color32(21, 23, 27, 140),
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
