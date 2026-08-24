using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    /// <summary>
    /// Two-button master stage-lighting switch beside the room theme switch.
    /// </summary>
    public sealed class QuestStageLightingSwitchController : MonoBehaviour
    {
        public const string SwitchCanvasName = "Stage Lighting Switch Canvas";

        public static readonly Vector3 SwitchWorldPosition = new Vector3(-0.69f, 0.621f, 0.659f);
        public static readonly Quaternion SwitchWorldRotation = Quaternion.Euler(78f, 0f, 0f);

        private const float SwitchCanvasScale = 0.001f;

        private static readonly string[] LightingLabels = { "开灯", "关灯" };

        [Header("Scene References")]
        [SerializeField] private QuestKtvRoomPrototype roomPrototype;
        [SerializeField] private QuestStageLightingPrototype stageLightingPrototype;
        [SerializeField] private Canvas switchCanvas;

        private readonly Button[] lightingButtons = new Button[2];
        private readonly QuestUiSurface[] lightingSurfaces = new QuestUiSurface[2];
        private readonly TMP_Text[] lightingLabels = new TMP_Text[2];
        private readonly QuestUiSurface[] lightingLights = new QuestUiSurface[2];

        private QuestUiSurface dockSurface;
        private QuestKtvRoomPrototype subscribedRoom;
        private QuestStageLightingPrototype subscribedStageLighting;
        private RoomTheme currentTheme = RoomTheme.Dark;
        private bool lightingEnabled = true;
        private TMP_FontAsset uiFont;

        public bool LightingEnabled => lightingEnabled;

        public void Configure(QuestKtvRoomPrototype room, QuestStageLightingPrototype stageLighting)
        {
            var nextRoom = room != null ? room : roomPrototype;
            nextRoom = nextRoom != null ? nextRoom : FindAnyObjectByType<QuestKtvRoomPrototype>();
            if (roomPrototype != nextRoom)
            {
                UnsubscribeRoom();
                roomPrototype = nextRoom;
            }

            var nextStageLighting = stageLighting != null ? stageLighting : stageLightingPrototype;
            nextStageLighting = nextStageLighting != null
                ? nextStageLighting
                : FindAnyObjectByType<QuestStageLightingPrototype>();
            if (stageLightingPrototype != nextStageLighting)
            {
                UnsubscribeStageLighting();
                stageLightingPrototype = nextStageLighting;
            }

            uiFont = ResolveUiFont();
            currentTheme = roomPrototype != null ? roomPrototype.CurrentTheme : RoomTheme.Dark;
            lightingEnabled = stageLightingPrototype == null || stageLightingPrototype.LightingEnabled;
            EnsureSwitchCanvas();
            SubscribeRoom();
            SubscribeStageLighting();
            RefreshLightingVisuals();
        }

        private void OnEnable()
        {
            SubscribeRoom();
            SubscribeStageLighting();
            currentTheme = roomPrototype != null ? roomPrototype.CurrentTheme : currentTheme;
            lightingEnabled = stageLightingPrototype != null
                ? stageLightingPrototype.LightingEnabled
                : lightingEnabled;
            RefreshLightingVisuals();
        }

        private void OnDisable()
        {
            UnsubscribeRoom();
            UnsubscribeStageLighting();
        }

        public void SetLightingEnabled(bool enabled)
        {
            if (stageLightingPrototype == null)
            {
                stageLightingPrototype = FindAnyObjectByType<QuestStageLightingPrototype>();
                SubscribeStageLighting();
            }

            if (stageLightingPrototype == null)
            {
                RefreshLightingVisuals();
                return;
            }

            var changed = stageLightingPrototype.LightingEnabled != enabled;
            stageLightingPrototype.SetLightingEnabled(enabled);
            lightingEnabled = stageLightingPrototype.LightingEnabled;
            RefreshLightingVisuals();
            if (changed)
            {
                SendSwitchHaptic();
            }
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

            WireLightingButtons();
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

            for (var index = 0; index < lightingButtons.Length; index += 1)
            {
                var lightingOption = dock.Find($"Lighting {index}");
                if (lightingOption == null ||
                    !lightingOption.TryGetComponent<Button>(out var button) ||
                    !lightingOption.TryGetComponent<QuestUiSurface>(out var surface))
                {
                    return false;
                }

                lightingButtons[index] = button;
                lightingSurfaces[index] = surface;
                lightingLabels[index] = lightingOption.Find("Label")?.GetComponent<TMP_Text>();
                lightingLights[index] = lightingOption.Find("Selected Light")?.GetComponent<QuestUiSurface>();
                if (lightingLabels[index] == null || lightingLights[index] == null)
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

            for (var index = 0; index < lightingButtons.Length; index += 1)
            {
                var x = -50f + index * 100f;
                var surface = CreateSurface(
                    dock.rectTransform,
                    $"Lighting {index}",
                    new Vector2(88f, 88f),
                    new Vector2(x, 0f),
                    palette.ButtonSurface,
                    8f);
                var button = surface.gameObject.AddComponent<Button>();
                button.targetGraphic = surface;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.colors = CreateButtonColors(palette);
                surface.raycastTarget = true;

                var label = CreateText(
                    surface.rectTransform,
                    "Label",
                    LightingLabels[index],
                    19,
                    palette.DockTextPrimary,
                    Vector2.zero,
                    new Vector2(82f, 72f));
                var light = CreateSurface(
                    surface.rectTransform,
                    "Selected Light",
                    new Vector2(42f, 4f),
                    new Vector2(0f, -37f),
                    palette.DockAccent,
                    2f);
                light.raycastTarget = false;

                var feedback = surface.gameObject.AddComponent<QuestUiButtonFeedback>();
                feedback.Configure((TMP_Text)null, string.Empty);

                lightingButtons[index] = button;
                lightingSurfaces[index] = surface;
                lightingLabels[index] = label;
                lightingLights[index] = light;
            }
        }

        private void WireLightingButtons()
        {
            for (var index = 0; index < lightingButtons.Length; index += 1)
            {
                var enableLighting = index == 0;
                lightingButtons[index].transition = Selectable.Transition.None;
                var feedback = lightingButtons[index].GetComponent<QuestUiButtonFeedback>() ??
                               lightingButtons[index].gameObject.AddComponent<QuestUiButtonFeedback>();
                feedback.Configure((TMP_Text)null, string.Empty);
                lightingButtons[index].onClick.RemoveAllListeners();
                lightingButtons[index].onClick.AddListener(() => SetLightingEnabled(enableLighting));
            }
        }

        private void RefreshLightingVisuals()
        {
            var palette = QuestUiThemePalette.For(currentTheme);
            if (dockSurface != null)
            {
                dockSurface.color = palette.DockSurface;
            }

            for (var index = 0; index < lightingButtons.Length; index += 1)
            {
                if (lightingButtons[index] == null ||
                    lightingSurfaces[index] == null ||
                    lightingLabels[index] == null ||
                    lightingLights[index] == null)
                {
                    continue;
                }

                var isActive = lightingEnabled == (index == 0);
                lightingButtons[index].interactable = stageLightingPrototype != null;
                lightingLabels[index].text = LightingLabels[index];
                lightingSurfaces[index].color = isActive ? palette.ActiveSurface : palette.ButtonSurface;
                lightingLabels[index].color = isActive ? palette.DockAccentInk : palette.DockTextPrimary;
                lightingLights[index].color = new Color(
                    palette.DockAccent.r,
                    palette.DockAccent.g,
                    palette.DockAccent.b,
                    isActive ? 1f : 0f);
                lightingButtons[index].colors = CreateButtonColors(palette);
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

        private void SubscribeStageLighting()
        {
            if (!isActiveAndEnabled ||
                stageLightingPrototype == null ||
                subscribedStageLighting == stageLightingPrototype)
            {
                return;
            }

            UnsubscribeStageLighting();
            subscribedStageLighting = stageLightingPrototype;
            subscribedStageLighting.LightingEnabledChanged += HandleLightingEnabledChanged;
        }

        private void UnsubscribeStageLighting()
        {
            if (subscribedStageLighting == null)
            {
                return;
            }

            subscribedStageLighting.LightingEnabledChanged -= HandleLightingEnabledChanged;
            subscribedStageLighting = null;
        }

        private void HandleRoomThemeChanged(RoomTheme theme)
        {
            currentTheme = theme;
            RefreshLightingVisuals();
        }

        private void HandleLightingEnabledChanged(bool enabled)
        {
            lightingEnabled = enabled;
            RefreshLightingVisuals();
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
