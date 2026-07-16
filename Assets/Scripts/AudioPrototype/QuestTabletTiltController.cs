using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    public sealed class QuestTabletTiltController : MonoBehaviour
    {
        public const string TabletPivotName = "Tablet Pivot";
        public const string SwitchCanvasName = "Tablet Tilt Switch Canvas";

        public static readonly Vector3 HingeWorldPosition = new Vector3(0f, 0.62f, 0.78f);
        public static readonly Vector3 SwitchWorldPosition = new Vector3(0.79f, 0.621f, 0.659f);
        public static readonly Quaternion SwitchWorldRotation = Quaternion.Euler(78f, 0f, 0f);

        private const string TiltStepPrefsKey = "TsukiVox.TabletTiltStep.v2";
        private const int DefaultStepIndex = 1;
        private const float CanvasCenterFromHinge = 0.39f;
        private const float SwitchCanvasScale = 0.001f;

        private static readonly float[] TiltAngles = { 0f, 30f, 60f, 90f };
        private static readonly string[] TiltLabels = { "0°\n平放", "30°\n低角", "60°\n阅读", "90°\n直立" };

        private static readonly Color DockSurface = new Color(0.025f, 0.045f, 0.047f, 0.98f);
        private static readonly Color ButtonSurface = new Color(0.07f, 0.095f, 0.1f, 1f);
        private static readonly Color ActiveSurface = new Color(0.24f, 0.9f, 0.74f, 1f);
        private static readonly Color TextPrimary = new Color(0.9f, 0.96f, 0.96f, 1f);
        private static readonly Color TextSecondary = new Color(0.57f, 0.68f, 0.69f, 1f);
        private static readonly Color Accent = new Color(0.25f, 0.95f, 0.72f, 1f);
        private static readonly Color AccentInk = new Color(0.012f, 0.075f, 0.059f, 1f);

        [Header("Scene References")]
        [SerializeField] private Canvas controlCanvas;
        [SerializeField] private RectTransform controlPanel;
        [SerializeField] private Transform tabletPivot;
        [SerializeField] private Canvas switchCanvas;

        [Header("Motion")]
        [SerializeField, Range(0, 3)] private int currentStepIndex = DefaultStepIndex;
        [SerializeField] private float currentTiltAngle = 30f;
        [SerializeField] private bool isAnimating;

        private readonly Button[] stepButtons = new Button[4];
        private readonly QuestUiSurface[] stepSurfaces = new QuestUiSurface[4];
        private readonly TMP_Text[] stepLabels = new TMP_Text[4];
        private readonly QuestUiSurface[] stepLights = new QuestUiSurface[4];

        private CanvasGroup controlPanelGroup;
        private bool panelWasInteractable = true;
        private bool panelWasBlockingRaycasts = true;
        private float animationStartedAt;
        private float animationDuration;
        private float animationStartAngle;
        private float animationTargetAngle;
        private float nextHierarchyResolveAt;
        private TMP_FontAsset uiFont;

        public float CurrentTiltAngle => currentTiltAngle;
        public int CurrentStepIndex => currentStepIndex;
        public bool IsAnimating => isAnimating;

        public void Configure(Canvas canvas, RectTransform panel)
        {
            controlCanvas = canvas != null ? canvas : controlCanvas;
            controlPanel = panel != null ? panel : controlPanel;
            uiFont = ResolveUiFont();

            currentStepIndex = Application.isPlaying
                ? Mathf.Clamp(PlayerPrefs.GetInt(TiltStepPrefsKey, DefaultStepIndex), 0, TiltAngles.Length - 1)
                : DefaultStepIndex;
            currentTiltAngle = TiltAngles[currentStepIndex];
            animationTargetAngle = currentTiltAngle;

            EnsureSwitchCanvas();
            ResolvePanelGroup();
            TryConfigureTabletHierarchy();
            ApplyTiltImmediate(currentTiltAngle);
            RefreshStepVisuals(-1f);
        }

        public void SetTiltStep(int stepIndex)
        {
            stepIndex = Mathf.Clamp(stepIndex, 0, TiltAngles.Length - 1);
            var nextAngle = TiltAngles[stepIndex];
            if (stepIndex == currentStepIndex && !isAnimating)
            {
                return;
            }

            if (tabletPivot == null && !TryConfigureTabletHierarchy())
            {
                return;
            }

            if (!isAnimating)
            {
                LockMovingPanel();
            }

            currentStepIndex = stepIndex;
            animationStartAngle = currentTiltAngle;
            animationTargetAngle = nextAngle;
            animationStartedAt = Time.unscaledTime;
            animationDuration = 0.34f + Mathf.Abs(animationTargetAngle - animationStartAngle) * 0.004f;
            isAnimating = !Mathf.Approximately(animationStartAngle, animationTargetAngle);

            if (Application.isPlaying)
            {
                PlayerPrefs.SetInt(TiltStepPrefsKey, currentStepIndex);
                PlayerPrefs.Save();
            }

            if (!isAnimating)
            {
                CompleteAnimation();
            }

            RefreshStepVisuals(0f);
        }

        private void Update()
        {
            if (tabletPivot == null && Time.unscaledTime >= nextHierarchyResolveAt)
            {
                nextHierarchyResolveAt = Time.unscaledTime + 0.25f;
                if (TryConfigureTabletHierarchy())
                {
                    ApplyTiltImmediate(currentTiltAngle);
                }
            }
            else if (tabletPivot != null && !IsControlCanvasAttached())
            {
                TryConfigureTabletHierarchy();
                ApplyTiltImmediate(currentTiltAngle);
            }

            if (!isAnimating || tabletPivot == null)
            {
                return;
            }

            var progress = Mathf.Clamp01((Time.unscaledTime - animationStartedAt) / Mathf.Max(0.01f, animationDuration));
            var eased = SmootherStep(progress);
            currentTiltAngle = Mathf.LerpAngle(animationStartAngle, animationTargetAngle, eased);
            ApplyTiltImmediate(currentTiltAngle);
            RefreshStepVisuals(progress);

            if (progress >= 1f)
            {
                CompleteAnimation();
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

        private bool TryConfigureTabletHierarchy()
        {
            tabletPivot = tabletPivot != null ? tabletPivot : FindTransform(TabletPivotName);
            if (tabletPivot == null)
            {
                return false;
            }

            var tabletAnchor = tabletPivot.Find("Tablet Anchor");
            if (controlCanvas != null)
            {
                var rect = controlCanvas.GetComponent<RectTransform>();
                var panelMount = tabletAnchor != null ? tabletAnchor : tabletPivot;
                rect.SetParent(panelMount, false);
                rect.localPosition = tabletAnchor != null
                    ? Vector3.zero
                    : new Vector3(0f, CanvasCenterFromHinge, 0f);
                rect.localRotation = Quaternion.identity;
                rect.localScale = QuestAppShellPrototype.ControlPanelWorldScale;
                rect.sizeDelta = QuestAppShellPrototype.ControlPanelSize;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            if (tabletAnchor != null)
            {
                tabletAnchor.localPosition = new Vector3(0f, CanvasCenterFromHinge, 0f);
                tabletAnchor.localRotation = Quaternion.identity;
            }

            return true;
        }

        private bool IsControlCanvasAttached()
        {
            if (controlCanvas == null || tabletPivot == null)
            {
                return true;
            }

            var tabletAnchor = tabletPivot.Find("Tablet Anchor");
            var expectedParent = tabletAnchor != null ? tabletAnchor : tabletPivot;
            return controlCanvas.transform.parent == expectedParent &&
                   Quaternion.Angle(controlCanvas.transform.localRotation, Quaternion.identity) < 0.01f;
        }

        private void ApplyTiltImmediate(float tiltAngle)
        {
            if (tabletPivot != null)
            {
                tabletPivot.localRotation = Quaternion.Euler(90f - tiltAngle, 0f, 0f);
            }
        }

        private void CompleteAnimation()
        {
            currentTiltAngle = animationTargetAngle;
            ApplyTiltImmediate(currentTiltAngle);
            isAnimating = false;
            UnlockMovingPanel();
            RefreshStepVisuals(-1f);
            SendCompletionHaptic();
        }

        private void ResolvePanelGroup()
        {
            if (controlPanel == null)
            {
                return;
            }

            controlPanelGroup = controlPanel.GetComponent<CanvasGroup>() ?? controlPanel.gameObject.AddComponent<CanvasGroup>();
        }

        private void LockMovingPanel()
        {
            ResolvePanelGroup();
            if (controlPanelGroup == null)
            {
                return;
            }

            panelWasInteractable = controlPanelGroup.interactable;
            panelWasBlockingRaycasts = controlPanelGroup.blocksRaycasts;
            controlPanelGroup.interactable = false;
            controlPanelGroup.blocksRaycasts = false;
        }

        private void UnlockMovingPanel()
        {
            if (controlPanelGroup == null)
            {
                return;
            }

            controlPanelGroup.interactable = panelWasInteractable;
            controlPanelGroup.blocksRaycasts = panelWasBlockingRaycasts;
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
            switchRect.sizeDelta = new Vector2(408f, 112f);
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

            WireStepButtons();
        }

        private bool TryBindExistingSwitch()
        {
            var dock = switchCanvas.transform.Find("Dock");
            if (dock == null)
            {
                return false;
            }

            for (var index = 0; index < stepButtons.Length; index += 1)
            {
                var step = dock.Find($"Angle {index}");
                if (step == null ||
                    !step.TryGetComponent<Button>(out var button) ||
                    !step.TryGetComponent<QuestUiSurface>(out var surface))
                {
                    return false;
                }

                stepButtons[index] = button;
                stepSurfaces[index] = surface;
                stepLabels[index] = step.Find("Label")?.GetComponent<TMP_Text>();
                stepLights[index] = step.Find("Selected Light")?.GetComponent<QuestUiSurface>();
                if (stepLabels[index] == null || stepLights[index] == null)
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

            var dock = CreateSurface(parent, "Dock", parent.sizeDelta, Vector2.zero, DockSurface, 10f);
            dock.raycastTarget = false;

            for (var index = 0; index < stepButtons.Length; index += 1)
            {
                var x = -150f + index * 100f;
                var surface = CreateSurface(dock.rectTransform, $"Angle {index}", new Vector2(88f, 88f), new Vector2(x, 0f), ButtonSurface, 8f);
                var button = surface.gameObject.AddComponent<Button>();
                button.targetGraphic = surface;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.colors = CreateButtonColors();
                surface.raycastTarget = true;

                var label = CreateText(surface.rectTransform, "Label", TiltLabels[index], 19, TextPrimary, Vector2.zero, new Vector2(82f, 72f));
                var light = CreateSurface(surface.rectTransform, "Selected Light", new Vector2(42f, 4f), new Vector2(0f, -37f), Accent, 2f);
                light.raycastTarget = false;

                var feedback = surface.gameObject.AddComponent<QuestUiButtonFeedback>();
                feedback.Configure((TMP_Text)null, string.Empty);

                stepButtons[index] = button;
                stepSurfaces[index] = surface;
                stepLabels[index] = label;
                stepLights[index] = light;
            }
        }

        private void WireStepButtons()
        {
            for (var index = 0; index < stepButtons.Length; index += 1)
            {
                var capturedIndex = index;
                stepButtons[index].transition = Selectable.Transition.None;
                var feedback = stepButtons[index].GetComponent<QuestUiButtonFeedback>() ??
                               stepButtons[index].gameObject.AddComponent<QuestUiButtonFeedback>();
                feedback.Configure((TMP_Text)null, string.Empty);
                stepButtons[index].onClick.RemoveAllListeners();
                stepButtons[index].onClick.AddListener(() => SetTiltStep(capturedIndex));
            }
        }

        private void RefreshStepVisuals(float motionProgress)
        {
            for (var index = 0; index < stepButtons.Length; index += 1)
            {
                if (stepSurfaces[index] == null || stepLabels[index] == null || stepLights[index] == null)
                {
                    continue;
                }

                var isActive = index == currentStepIndex;
                stepLabels[index].text = TiltLabels[index];
                stepSurfaces[index].color = isActive ? ActiveSurface : ButtonSurface;
                stepLabels[index].color = isActive ? AccentInk : TextPrimary;
                var lightAlpha = isActive ? 1f : 0f;
                if (isActive && motionProgress >= 0f)
                {
                    lightAlpha = 0.58f + Mathf.Sin(motionProgress * Mathf.PI) * 0.42f;
                }

                stepLights[index].color = new Color(Accent.r, Accent.g, Accent.b, lightAlpha);
            }
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

        private static ColorBlock CreateButtonColors()
        {
            return new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.72f, 1f, 0.9f, 1f),
                pressedColor = new Color(0.42f, 0.92f, 0.72f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.42f, 0.48f, 0.48f, 0.42f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        private static float SmootherStep(float value)
        {
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }

        private static void SendCompletionHaptic()
        {
            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            if (device.isValid)
            {
                device.SendHapticImpulse(0u, 0.18f, 0.045f);
            }
        }

        private static Transform FindTransform(string objectName)
        {
            var gameObject = GameObject.Find(objectName);
            return gameObject != null ? gameObject.transform : null;
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
