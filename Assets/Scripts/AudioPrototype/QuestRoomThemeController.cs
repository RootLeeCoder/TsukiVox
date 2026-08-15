using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    /// <summary>
    /// Two-button room theme switch on the coffee table's left-front corner,
    /// mirroring the tablet tilt switch on the right-front corner. Clicking a
    /// button retints the KTV room palette in place and persists the choice.
    /// </summary>
    public sealed class QuestRoomThemeController : MonoBehaviour
    {
        public const string SwitchCanvasName = "Room Theme Switch Canvas";

        public static readonly Vector3 SwitchWorldPosition = new Vector3(-0.79f, 0.621f, 0.659f);
        public static readonly Quaternion SwitchWorldRotation = Quaternion.Euler(78f, 0f, 0f);

        private const float SwitchCanvasScale = 0.001f;

        private static readonly string[] ThemeLabels = { "暗色\n夜场", "亮色\n日光" };


        [Header("Scene References")]
        [SerializeField] private QuestKtvRoomPrototype roomPrototype;
        [SerializeField] private Canvas switchCanvas;

        private readonly Button[] themeButtons = new Button[2];
        private readonly QuestUiSurface[] themeSurfaces = new QuestUiSurface[2];
        private readonly TMP_Text[] themeLabels = new TMP_Text[2];
        private readonly QuestUiSurface[] themeLights = new QuestUiSurface[2];

        private QuestUiSurface dockSurface;
        private QuestKtvRoomPrototype subscribedRoom;

        private RoomTheme currentTheme = RoomTheme.Dark;
        private TMP_FontAsset uiFont;

        public RoomTheme CurrentTheme => currentTheme;

        public void Configure(QuestKtvRoomPrototype room)
        {
            var nextRoom = room != null ? room : roomPrototype;
            if (nextRoom == null)
            {
                nextRoom = FindAnyObjectByType<QuestKtvRoomPrototype>();
            }

            if (roomPrototype != nextRoom)
            {
                UnsubscribeRoom();
                roomPrototype = nextRoom;
            }

            uiFont = ResolveUiFont();
            currentTheme = roomPrototype != null ? roomPrototype.CurrentTheme : LoadPersistedTheme();
            EnsureSwitchCanvas();
            SubscribeRoom();
            RefreshThemeVisuals();
        }

        private void OnEnable()
        {
            SubscribeRoom();
            if (roomPrototype != null)
            {
                currentTheme = roomPrototype.CurrentTheme;
                RefreshThemeVisuals();
            }
        }

        private void OnDisable()
        {
            UnsubscribeRoom();
        }

        public void SetTheme(int themeIndex)
        {
            var theme = themeIndex == (int)RoomTheme.Bright ? RoomTheme.Bright : RoomTheme.Dark;
            var changed = theme != currentTheme;

            if (Application.isPlaying)
            {
                PlayerPrefs.SetInt(QuestKtvRoomPrototype.ThemePrefsKey, (int)theme);
                PlayerPrefs.Save();
            }

            if (roomPrototype == null)
            {
                roomPrototype = FindAnyObjectByType<QuestKtvRoomPrototype>();
                SubscribeRoom();
            }

            if (roomPrototype != null)
            {
                roomPrototype.ApplyTheme(theme);
                currentTheme = roomPrototype.CurrentTheme;
            }
            else
            {
                currentTheme = theme;
            }

            RefreshThemeVisuals();
            if (changed)
            {
                SendSwitchHaptic();
            }
        }

        public void RestoreDefaultSetting()
        {
            PlayerPrefs.DeleteKey(QuestKtvRoomPrototype.ThemePrefsKey);
            currentTheme = RoomTheme.Dark;
            if (roomPrototype == null)
            {
                roomPrototype = FindAnyObjectByType<QuestKtvRoomPrototype>();
                SubscribeRoom();
            }

            roomPrototype?.ApplyTheme(RoomTheme.Dark);
            currentTheme = roomPrototype != null ? roomPrototype.CurrentTheme : RoomTheme.Dark;
            RefreshThemeVisuals();
        }

        private void LateUpdate()
        {
            if (switchCanvas == null)
            {
                return;
            }

            var switchRect = switchCanvas.GetComponent<RectTransform>();
            switchRect.position = SwitchWorldPosition;
            switchRect.rotation = SwitchWorldRotation;
            switchRect.localScale = Vector3.one * SwitchCanvasScale;
        }

        private static RoomTheme LoadPersistedTheme()
        {
            if (!Application.isPlaying)
            {
                return RoomTheme.Dark;
            }

            var stored = PlayerPrefs.GetInt(QuestKtvRoomPrototype.ThemePrefsKey, (int)RoomTheme.Dark);
            return stored == (int)RoomTheme.Bright ? RoomTheme.Bright : RoomTheme.Dark;
        }

        private void EnsureSwitchCanvas()
        {
            if (switchCanvas == null)
            {
                var existing = GameObject.Find(SwitchCanvasName);
                switchCanvas = existing != null ? existing.GetComponent<Canvas>() : null;
            }

            if (switchCanvas == null)
            {
                var canvasObject = new GameObject(SwitchCanvasName);
                switchCanvas = canvasObject.AddComponent<Canvas>();
            }

            switchCanvas.renderMode = RenderMode.WorldSpace;
            switchCanvas.worldCamera = Camera.main;
            switchCanvas.planeDistance = 100f;
            switchCanvas.sortingOrder = 12;

            var switchRect = switchCanvas.GetComponent<RectTransform>();
            switchRect.sizeDelta = new Vector2(208f, 112f);
            switchRect.position = SwitchWorldPosition;
            switchRect.rotation = SwitchWorldRotation;
            switchRect.localScale = Vector3.one * SwitchCanvasScale;

            var scaler = switchCanvas.GetComponent<CanvasScaler>() ?? switchCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = switchRect.sizeDelta;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 32f;

            var raycaster = switchCanvas.GetComponent<GraphicRaycaster>() ?? switchCanvas.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.ignoreReversedGraphics = false;

            if (!TryBindExistingSwitch())
            {
                BuildSwitchUi(switchRect);
            }

            WireThemeButtons();
        }

        private bool TryBindExistingSwitch()
        {
            var dock = switchCanvas.transform.Find("Dock");
            if (dock == null)
            {
                return false;
            }

            dockSurface = dock.GetComponent<QuestUiSurface>();
            if (dockSurface == null)
            {
                return false;
            }

            for (var index = 0; index < themeButtons.Length; index += 1)
            {
                var themeOption = dock.Find($"Theme {index}");
                if (themeOption == null ||
                    !themeOption.TryGetComponent<Button>(out var button) ||
                    !themeOption.TryGetComponent<QuestUiSurface>(out var surface))
                {
                    return false;
                }

                themeButtons[index] = button;
                themeSurfaces[index] = surface;
                themeLabels[index] = themeOption.Find("Label")?.GetComponent<TMP_Text>();
                themeLights[index] = themeOption.Find("Selected Light")?.GetComponent<QuestUiSurface>();
                if (themeLabels[index] == null || themeLights[index] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private void BuildSwitchUi(RectTransform parent)
        {
            var existingDock = parent.Find("Dock");
            if (existingDock != null)
            {
                existingDock.name = "Dock Legacy";
                existingDock.gameObject.SetActive(false);
                DestroyForCurrentMode(existingDock.gameObject);
            }

            var palette = QuestUiThemePalette.For(currentTheme);
            var dock = CreateSurface(parent, "Dock", parent.sizeDelta, Vector2.zero, palette.DockSurface, 10f);
            dock.raycastTarget = false;
            dockSurface = dock;

            for (var index = 0; index < themeButtons.Length; index += 1)
            {
                var x = -50f + index * 100f;
                var surface = CreateSurface(dock.rectTransform, $"Theme {index}", new Vector2(88f, 88f), new Vector2(x, 0f), palette.ButtonSurface, 8f);
                var button = surface.gameObject.AddComponent<Button>();
                button.targetGraphic = surface;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.colors = CreateButtonColors(palette);
                surface.raycastTarget = true;

                var label = CreateText(surface.rectTransform, "Label", ThemeLabels[index], 19, palette.DockTextPrimary, Vector2.zero, new Vector2(82f, 72f));
                var light = CreateSurface(surface.rectTransform, "Selected Light", new Vector2(42f, 4f), new Vector2(0f, -37f), palette.DockAccent, 2f);
                light.raycastTarget = false;

                var feedback = surface.gameObject.AddComponent<QuestUiButtonFeedback>();
                feedback.Configure((TMP_Text)null, string.Empty);

                themeButtons[index] = button;
                themeSurfaces[index] = surface;
                themeLabels[index] = label;
                themeLights[index] = light;
            }
        }

        private void WireThemeButtons()
        {
            for (var index = 0; index < themeButtons.Length; index += 1)
            {
                var capturedIndex = index;
                themeButtons[index].transition = Selectable.Transition.None;
                var feedback = themeButtons[index].GetComponent<QuestUiButtonFeedback>() ??
                               themeButtons[index].gameObject.AddComponent<QuestUiButtonFeedback>();
                feedback.Configure((TMP_Text)null, string.Empty);
                themeButtons[index].onClick.RemoveAllListeners();
                themeButtons[index].onClick.AddListener(() => SetTheme(capturedIndex));
            }
        }

        private void RefreshThemeVisuals()
        {
            var palette = QuestUiThemePalette.For(currentTheme);
            if (dockSurface != null)
            {
                dockSurface.color = palette.DockSurface;
            }

            for (var index = 0; index < themeButtons.Length; index += 1)
            {
                if (themeSurfaces[index] == null || themeLabels[index] == null || themeLights[index] == null)
                {
                    continue;
                }

                var isActive = index == (int)currentTheme;
                themeLabels[index].text = ThemeLabels[index];
                themeSurfaces[index].color = isActive ? palette.ActiveSurface : palette.ButtonSurface;
                themeLabels[index].color = isActive ? palette.DockAccentInk : palette.DockTextPrimary;
                themeLights[index].color = new Color(palette.DockAccent.r, palette.DockAccent.g, palette.DockAccent.b, isActive ? 1f : 0f);
                if (themeButtons[index] != null)
                {
                    themeButtons[index].colors = CreateButtonColors(palette);
                }
            }
        }

        private void SubscribeRoom()
        {
            if (!isActiveAndEnabled || roomPrototype == null || subscribedRoom == roomPrototype)
            {
                return;
            }

            UnsubscribeRoom();
            subscribedRoom = roomPrototype;
            subscribedRoom.ThemeChanged += HandleRoomThemeChanged;
        }

        private void UnsubscribeRoom()
        {
            if (subscribedRoom == null)
            {
                return;
            }

            subscribedRoom.ThemeChanged -= HandleRoomThemeChanged;
            subscribedRoom = null;
        }

        private void HandleRoomThemeChanged(RoomTheme theme)
        {
            currentTheme = theme;
            RefreshThemeVisuals();
        }

        private static QuestUiSurface CreateSurface(
            Transform parent,
            string objectName,
            Vector2 size,
            Vector2 position,
            Color color,
            float cornerRadius)
        {
            var surfaceObject = new GameObject(objectName, typeof(RectTransform));
            surfaceObject.transform.SetParent(parent, false);
            var rect = surfaceObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var surface = surfaceObject.AddComponent<QuestUiSurface>();
            surface.color = color;
            surface.SetCornerRadius(cornerRadius);
            return surface;
        }

        private TMP_Text CreateText(
            Transform parent,
            string objectName,
            string value,
            int fontSize,
            Color color,
            Vector2 position,
            Vector2 size)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = uiFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.extraPadding = true;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = size;
            text.rectTransform.anchoredPosition = position;
            return text;
        }

        private static TMP_FontAsset ResolveUiFont()
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts/NotoSansSC-SDF");
            if (font != null)
            {
                font.isMultiAtlasTexturesEnabled = true;
                return font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static ColorBlock CreateButtonColors(QuestUiThemePalette palette)
        {
            return new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = palette.ButtonHighlighted,
                pressedColor = palette.ButtonPressed,
                selectedColor = Color.white,
                disabledColor = palette.ButtonDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        private static void SendSwitchHaptic()
        {
            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            if (device.isValid)
            {
                device.SendHapticImpulse(0u, 0.18f, 0.045f);
            }
        }

        private static void DestroyForCurrentMode(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
