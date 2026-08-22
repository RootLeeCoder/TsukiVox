using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Toggle))]
    public sealed class QuestUiSwitchVisual : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private const float TransitionDuration = 0.16f;
        private const float KnobOffset = 14f;
        private const float PressedScale = 0.96f;

        [SerializeField] private Toggle toggle;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private RectTransform knob;
        [SerializeField] private QuestUiSurface focusRingSurface;
        [SerializeField] private QuestUiSurface borderSurface;
        [SerializeField] private QuestUiSurface trackSurface;
        [SerializeField] private QuestUiSurface knobSurface;
        [SerializeField] private Shadow knobShadow;

        private QuestUiThemePalette palette;
        private bool listenerRegistered;
        private bool isHovered;
        private bool isFocused;
        private bool isPressed;
        private bool visualIsOn;
        private bool visualIsInteractable;
        private bool isTransitioning;
        private float transitionElapsed;
        private Vector2 knobStartPosition;
        private Vector2 knobTargetPosition;
        private Color focusRingStartColor;
        private Color focusRingTargetColor;
        private Color borderStartColor;
        private Color borderTargetColor;
        private Color trackStartColor;
        private Color trackTargetColor;
        private Color knobStartColor;
        private Color knobTargetColor;
        private Color shadowStartColor;
        private Color shadowTargetColor;
        private Vector3 scaleStart;
        private Vector3 scaleTarget;

        public void Configure(
            Toggle targetToggle,
            RectTransform targetVisualRoot,
            RectTransform targetKnob,
            QuestUiSurface targetFocusRingSurface,
            QuestUiSurface targetBorderSurface,
            QuestUiSurface targetTrackSurface,
            QuestUiSurface targetKnobSurface,
            Shadow targetKnobShadow,
            QuestUiThemePalette themePalette)
        {
            UnregisterListener();
            toggle = targetToggle;
            visualRoot = targetVisualRoot;
            knob = targetKnob;
            focusRingSurface = targetFocusRingSurface;
            borderSurface = targetBorderSurface;
            trackSurface = targetTrackSurface;
            knobSurface = targetKnobSurface;
            knobShadow = targetKnobShadow;
            palette = themePalette;
            isHovered = false;
            isFocused = false;
            isPressed = false;
            visualIsOn = toggle != null && toggle.isOn;
            visualIsInteractable = toggle != null && toggle.IsInteractable();
            RegisterListener();
            BeginTransition(true);
        }

        public void SetPalette(QuestUiThemePalette themePalette, bool instant = false)
        {
            if (themePalette == null)
            {
                return;
            }

            var changed = !ReferenceEquals(palette, themePalette);
            palette = themePalette;
            if (changed || instant)
            {
                BeginTransition(instant);
            }
        }

        public void RefreshState(bool instant = false)
        {
            if (toggle == null)
            {
                return;
            }

            var nextIsOn = toggle.isOn;
            var nextIsInteractable = toggle.IsInteractable();
            if (!instant && nextIsOn == visualIsOn && nextIsInteractable == visualIsInteractable)
            {
                return;
            }

            visualIsOn = nextIsOn;
            visualIsInteractable = nextIsInteractable;
            if (!visualIsInteractable)
            {
                isHovered = false;
                isPressed = false;
            }

            BeginTransition(instant);
        }

        public void RebindToggleListener()
        {
            UnregisterListener();
            RegisterListener();
        }

        private void OnEnable()
        {
            RegisterListener();
            RefreshState(true);
        }

        private void OnDisable()
        {
            UnregisterListener();
            isHovered = false;
            isFocused = false;
            isPressed = false;
            isTransitioning = false;
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one;
            }
        }

        private void OnDestroy()
        {
            UnregisterListener();
        }

        private void Update()
        {
            if (toggle == null || palette == null)
            {
                return;
            }

            if (toggle.isOn != visualIsOn || toggle.IsInteractable() != visualIsInteractable)
            {
                RefreshState();
            }

            if (!isTransitioning)
            {
                return;
            }

            transitionElapsed += Time.unscaledDeltaTime;
            var normalized = Mathf.Clamp01(transitionElapsed / TransitionDuration);
            var eased = normalized * normalized * (3f - 2f * normalized);
            ApplyVisuals(eased);
            if (normalized >= 1f)
            {
                isTransitioning = false;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!visualIsInteractable)
            {
                return;
            }

            isHovered = true;
            BeginTransition(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            BeginTransition(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!visualIsInteractable)
            {
                return;
            }

            isPressed = true;
            BeginTransition(false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            BeginTransition(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!visualIsInteractable)
            {
                return;
            }

            isFocused = true;
            BeginTransition(false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isFocused = false;
            BeginTransition(false);
        }

        private void HandleValueChanged(bool isOn)
        {
            visualIsOn = isOn;
            visualIsInteractable = toggle != null && toggle.IsInteractable();
            BeginTransition(false);
        }

        private void RegisterListener()
        {
            if (listenerRegistered || toggle == null || !isActiveAndEnabled)
            {
                return;
            }

            toggle.onValueChanged.AddListener(HandleValueChanged);
            listenerRegistered = true;
        }

        private void UnregisterListener()
        {
            if (!listenerRegistered || toggle == null)
            {
                listenerRegistered = false;
                return;
            }

            toggle.onValueChanged.RemoveListener(HandleValueChanged);
            listenerRegistered = false;
        }

        private void BeginTransition(bool instant)
        {
            if (palette == null || visualRoot == null || knob == null ||
                focusRingSurface == null || borderSurface == null || trackSurface == null || knobSurface == null)
            {
                return;
            }

            knobStartPosition = knob.anchoredPosition;
            knobTargetPosition = new Vector2(visualIsOn ? KnobOffset : -KnobOffset, 0f);
            focusRingStartColor = focusRingSurface.color;
            focusRingTargetColor = visualIsInteractable && isFocused ? palette.SwitchFocusRing : Color.clear;
            borderStartColor = borderSurface.color;
            borderTargetColor = ResolveBorderColor();
            trackStartColor = trackSurface.color;
            trackTargetColor = ResolveTrackColor();
            knobStartColor = knobSurface.color;
            knobTargetColor = ResolveKnobColor();
            shadowStartColor = knobShadow != null ? knobShadow.effectColor : Color.clear;
            shadowTargetColor = visualIsInteractable ? palette.SwitchThumbShadow : Color.clear;
            scaleStart = visualRoot.localScale;
            scaleTarget = Vector3.one * (visualIsInteractable && isPressed ? PressedScale : 1f);
            transitionElapsed = 0f;
            isTransitioning = !instant && Application.isPlaying;
            ApplyVisuals(isTransitioning ? 0f : 1f);
        }

        private Color ResolveTrackColor()
        {
            if (!visualIsInteractable)
            {
                return palette.SwitchDisabledTrack;
            }

            if (visualIsOn)
            {
                return palette.Accent;
            }

            return isHovered ? palette.SwitchOffTrackHover : palette.SwitchOffTrack;
        }

        private Color ResolveBorderColor()
        {
            if (!visualIsInteractable)
            {
                return palette.SwitchDisabledTrack;
            }

            if (isHovered || isFocused)
            {
                return palette.AccentStrong;
            }

            return visualIsOn ? palette.Accent : palette.SwitchOffBorder;
        }

        private Color ResolveKnobColor()
        {
            if (!visualIsInteractable)
            {
                return palette.SwitchDisabledThumb;
            }

            return visualIsOn ? palette.AccentInk : palette.SwitchOffThumb;
        }

        private void ApplyVisuals(float normalized)
        {
            knob.anchoredPosition = Vector2.LerpUnclamped(knobStartPosition, knobTargetPosition, normalized);
            focusRingSurface.color = Color.LerpUnclamped(focusRingStartColor, focusRingTargetColor, normalized);
            borderSurface.color = Color.LerpUnclamped(borderStartColor, borderTargetColor, normalized);
            trackSurface.color = Color.LerpUnclamped(trackStartColor, trackTargetColor, normalized);
            knobSurface.color = Color.LerpUnclamped(knobStartColor, knobTargetColor, normalized);
            visualRoot.localScale = Vector3.LerpUnclamped(scaleStart, scaleTarget, normalized);
            if (knobShadow != null)
            {
                knobShadow.effectColor = Color.LerpUnclamped(shadowStartColor, shadowTargetColor, normalized);
            }
        }
    }
}
