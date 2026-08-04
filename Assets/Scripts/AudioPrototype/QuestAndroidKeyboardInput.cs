using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class QuestAndroidKeyboardInput : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerClickHandler
    {
        private const float KeyboardWidth = 1060f;
        private const float KeyboardHeight = 260f;
        private const float KeyGap = 8f;


        [SerializeField] private TMP_InputField inputField;

        private static QuestAndroidKeyboardInput activeInput;

        private readonly List<LetterLabel> letterLabels = new List<LetterLabel>(26);
        private readonly List<KeyboardKeyVisual> keyVisuals = new List<KeyboardKeyVisual>(64);
        private QuestKtvRoomPrototype roomPrototype;
        private QuestKtvRoomPrototype subscribedRoom;
        private QuestUiThemePalette palette = QuestUiThemePalette.For(RoomTheme.Dark);
        private RectTransform keyboardRoot;
        private RectTransform alphabetLayout;
        private RectTransform symbolLayout;
        private string originalText = string.Empty;
        private Coroutine dismissCoroutine;
        private int dismissRequestId;
        private float keyboardAnchoredY = -150f;
        private bool isUppercase;
        private bool isClosing;

        public bool IsKeyboardOpen => keyboardRoot != null && keyboardRoot.gameObject.activeInHierarchy;

        public static bool IsKeyboardTarget(GameObject target)
        {
            return target != null &&
                   activeInput != null &&
                   activeInput.keyboardRoot != null &&
                   target.transform.IsChildOf(activeInput.keyboardRoot);
        }

        public static QuestAndroidKeyboardInput Configure(TMP_InputField field, QuestKtvRoomPrototype room = null)
        {
            if (field == null)
            {
                return null;
            }

            var keyboardInput = field.GetComponent<QuestAndroidKeyboardInput>();
            keyboardInput = keyboardInput != null
                ? keyboardInput
                : field.gameObject.AddComponent<QuestAndroidKeyboardInput>();
            keyboardInput.inputField = field;
            keyboardInput.SetRoom(room);
            keyboardInput.ConfigureInputField();
            return keyboardInput;
        }

        private void Awake()
        {
            inputField = inputField != null ? inputField : GetComponent<TMP_InputField>();
            ConfigureInputField();
        }

        private void OnEnable()
        {
            SubscribeRoom();
        }

        private void OnDisable()
        {
            UnsubscribeRoom();
            ReleaseKeyboard();
        }

        private void OnDestroy()
        {
            UnsubscribeRoom();
            ReleaseKeyboard();
        }

        public void OnSelect(BaseEventData eventData)
        {
            ShowKeyboard();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (!isClosing)
            {
                RequestDeferredDismiss();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                ShowKeyboard();
            }
        }

        public void SetKeyboardAnchoredY(float anchoredY)
        {
            keyboardAnchoredY = anchoredY;
            if (keyboardRoot != null)
            {
                keyboardRoot.anchoredPosition = new Vector2(keyboardRoot.anchoredPosition.x, keyboardAnchoredY);
            }
        }

        public void ShowKeyboard()
        {
            if (inputField == null || !inputField.IsActive() || !inputField.IsInteractable() || inputField.readOnly)
            {
                return;
            }

            CancelPendingDismiss();
            if (activeInput != null && activeInput != this)
            {
                activeInput.HideKeyboard();
            }

            inputField.ActivateInputField();
            if (keyboardRoot != null)
            {
                keyboardRoot.SetAsLastSibling();
                return;
            }

            originalText = inputField.text ?? string.Empty;
            isUppercase = false;
            activeInput = this;
            CreateKeyboard();
            Debug.Log($"[TsukiVox Keyboard] In-app keyboard opened for '{inputField.name}'.");
        }

        public void HideKeyboard(bool submit = false)
        {
            FinishEditing(submit, canceled: false);
        }

        private void SetRoom(QuestKtvRoomPrototype room)
        {
            if (roomPrototype == room)
            {
                SubscribeRoom();
                return;
            }

            UnsubscribeRoom();
            roomPrototype = room;
            palette = QuestUiThemePalette.For(roomPrototype != null ? roomPrototype.CurrentTheme : RoomTheme.Dark);
            SubscribeRoom();
            ApplyThemeToKeyboard();
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
            palette = QuestUiThemePalette.For(theme);
            ApplyThemeToKeyboard();
        }

        private void ApplyThemeToKeyboard()
        {
            if (keyboardRoot == null)
            {
                return;
            }

            var panel = keyboardRoot.GetComponent<QuestUiSurface>();
            if (panel != null)
            {
                panel.color = palette.KeyboardPanel;
            }

            var panelOutline = keyboardRoot.GetComponent<Outline>();
            if (panelOutline != null)
            {
                panelOutline.effectColor = palette.KeyboardBorder;
            }

            for (var index = 0; index < keyVisuals.Count; index += 1)
            {
                var visual = keyVisuals[index];
                if (visual.Surface != null)
                {
                    visual.Surface.color = visual.IsAccent
                        ? palette.KeyboardAccentKey
                        : visual.IsUtility ? palette.KeyboardUtilityKey : palette.KeyboardKey;
                }
                if (visual.Outline != null)
                {
                    visual.Outline.effectColor = visual.IsAccent ? palette.KeyboardHighlighted : palette.KeyboardBorder;
                }
                if (visual.Button != null)
                {
                    visual.Button.colors = CreateKeyColors();
                }
                if (visual.Label != null)
                {
                    visual.Label.color = visual.IsAccent ? palette.KeyboardAccentText : palette.KeyboardText;
                }
            }
        }
        private void ConfigureInputField()
        {
            if (inputField == null)
            {
                return;
            }

            inputField.onFocusSelectAll = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            inputField.shouldHideSoftKeyboard = true;
#endif
        }

        private void CreateKeyboard()
        {
            var parent = inputField.transform.parent;
            if (parent == null)
            {
                Debug.LogWarning("[TsukiVox Keyboard] Cannot create the in-app keyboard without a UI parent.");
                return;
            }

            palette = QuestUiThemePalette.For(roomPrototype != null ? roomPrototype.CurrentTheme : RoomTheme.Dark);
            keyboardRoot = CreateRect(parent, "TsukiVox Soft Keyboard", new Vector2(0f, keyboardAnchoredY), new Vector2(KeyboardWidth, KeyboardHeight));
            keyboardRoot.SetAsLastSibling();

            var panel = keyboardRoot.gameObject.AddComponent<QuestUiSurface>();
            panel.color = palette.KeyboardPanel;
            panel.SetCornerRadius(8f);
            panel.raycastTarget = true;

            var outline = keyboardRoot.gameObject.AddComponent<Outline>();
            outline.effectColor = palette.KeyboardBorder;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = false;

            var panelButton = keyboardRoot.gameObject.AddComponent<Button>();
            panelButton.targetGraphic = panel;
            panelButton.transition = Selectable.Transition.None;
            panelButton.navigation = new Navigation { mode = Navigation.Mode.None };
            panelButton.onClick.AddListener(FocusInputField);

            alphabetLayout = CreateRect(keyboardRoot, "Letters", Vector2.zero, new Vector2(KeyboardWidth, KeyboardHeight));
            symbolLayout = CreateRect(keyboardRoot, "Symbols", Vector2.zero, new Vector2(KeyboardWidth, KeyboardHeight));

            BuildAlphabetLayout();
            BuildSymbolLayout();
            symbolLayout.gameObject.SetActive(false);
        }

        private void BuildAlphabetLayout()
        {
            CreateLetterRow(alphabetLayout, "QWERTYUIOP", 91f, 92f);
            CreateLetterRow(alphabetLayout, "ASDFGHJKL", 33f, 96f);
            CreateRow(
                alphabetLayout,
                -25f,
                new KeyDefinition("大写", 120f, ToggleCase, true),
                LetterKey('Z'),
                LetterKey('X'),
                LetterKey('C'),
                LetterKey('V'),
                LetterKey('B'),
                LetterKey('N'),
                LetterKey('M'),
                new KeyDefinition("退格", 132f, Backspace, true));
            CreateBottomRow(alphabetLayout, "符号", () => SetSymbolMode(true));
        }

        private void BuildSymbolLayout()
        {
            CreateCharacterRow(symbolLayout, "1234567890", 91f, 92f);
            CreateCharacterRow(symbolLayout, "@#$%&*()_", 33f, 96f);
            CreateRow(
                symbolLayout,
                -25f,
                new KeyDefinition("字母", 120f, () => SetSymbolMode(false), true),
                CharacterKey(':'),
                CharacterKey(';'),
                CharacterKey('?'),
                CharacterKey('!'),
                CharacterKey(','),
                CharacterKey('.'),
                CharacterKey('='),
                new KeyDefinition("退格", 132f, Backspace, true));
            CreateBottomRow(symbolLayout, "字母", () => SetSymbolMode(false));
        }

        private void CreateLetterRow(Transform parent, string characters, float y, float width)
        {
            var definitions = new KeyDefinition[characters.Length];
            for (var index = 0; index < characters.Length; index += 1)
            {
                definitions[index] = LetterKey(characters[index], width);
            }

            CreateRow(parent, y, definitions);
        }

        private void CreateCharacterRow(Transform parent, string characters, float y, float width)
        {
            var definitions = new KeyDefinition[characters.Length];
            for (var index = 0; index < characters.Length; index += 1)
            {
                definitions[index] = CharacterKey(characters[index], width);
            }

            CreateRow(parent, y, definitions);
        }

        private void CreateBottomRow(Transform parent, string modeLabel, UnityAction modeAction)
        {
            CreateRow(
                parent,
                -88f,
                new KeyDefinition(modeLabel, 100f, modeAction, true),
                new KeyDefinition("清空", 100f, ClearText, true),
                new KeyDefinition("空格", 300f, () => InsertText(" ")),
                new KeyDefinition(".", 64f, () => InsertText(".")),
                new KeyDefinition("-", 64f, () => InsertText("-")),
                new KeyDefinition("/", 64f, () => InsertText("/")),
                new KeyDefinition("取消", 112f, CancelEditing, true),
                new KeyDefinition("完成", 132f, SubmitEditing, true, true));
        }

        private void CreateRow(Transform parent, float y, params KeyDefinition[] definitions)
        {
            var totalWidth = KeyGap * Mathf.Max(0, definitions.Length - 1);
            for (var index = 0; index < definitions.Length; index += 1)
            {
                totalWidth += definitions[index].Width;
            }

            var x = -totalWidth * 0.5f;
            for (var index = 0; index < definitions.Length; index += 1)
            {
                var definition = definitions[index];
                var keyX = x + definition.Width * 0.5f;
                var label = CreateKey(parent, $"Key {y} {index}", definition, new Vector2(keyX, y));
                if (definition.Letter != '\0')
                {
                    letterLabels.Add(new LetterLabel(definition.Letter, label));
                }

                x += definition.Width + KeyGap;
            }
        }

        private TMP_Text CreateKey(Transform parent, string name, KeyDefinition definition, Vector2 position)
        {
            var rect = CreateRect(parent, name, position, new Vector2(definition.Width, 50f));
            var surface = rect.gameObject.AddComponent<QuestUiSurface>();
            surface.color = definition.IsAccent ? palette.KeyboardAccentKey : definition.IsUtility ? palette.KeyboardUtilityKey : palette.KeyboardKey;
            surface.SetCornerRadius(6f);
            surface.raycastTarget = true;

            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = definition.IsAccent ? palette.KeyboardHighlighted : palette.KeyboardBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateKeyColors();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(definition.Action);
            rect.gameObject.AddComponent<QuestUiButtonFeedback>();

            var labelRect = CreateRect(rect, "Label", Vector2.zero, new Vector2(definition.Width - 12f, 42f));
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = inputField.textComponent != null ? inputField.textComponent.font : null;
            label.text = definition.Label;
            label.fontSize = definition.Label.Length > 2 ? 17f : 20f;
            label.fontStyle = FontStyles.Bold;
            label.color = definition.IsAccent ? palette.KeyboardAccentText : palette.KeyboardText;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            keyVisuals.Add(new KeyboardKeyVisual(surface, outline, button, label, definition.IsUtility, definition.IsAccent));
            return label;
        }

        private KeyDefinition LetterKey(char character, float width = 92f)
        {
            return new KeyDefinition(
                character.ToString().ToLowerInvariant(),
                width,
                () => InsertText(ResolveLetter(character)),
                letter: character);
        }

        private KeyDefinition CharacterKey(char character, float width = 92f)
        {
            var value = character.ToString();
            return new KeyDefinition(value, width, () => InsertText(value));
        }

        private string ResolveLetter(char character)
        {
            return isUppercase
                ? character.ToString().ToUpperInvariant()
                : character.ToString().ToLowerInvariant();
        }

        private void ToggleCase()
        {
            isUppercase = !isUppercase;
            for (var index = 0; index < letterLabels.Count; index += 1)
            {
                var letterLabel = letterLabels[index];
                if (letterLabel.Label != null)
                {
                    letterLabel.Label.text = ResolveLetter(letterLabel.Character);
                }
            }

            FocusInputField();
        }

        private void SetSymbolMode(bool symbols)
        {
            if (alphabetLayout != null)
            {
                alphabetLayout.gameObject.SetActive(!symbols);
            }

            if (symbolLayout != null)
            {
                symbolLayout.gameObject.SetActive(symbols);
            }

            FocusInputField();
        }

        private void InsertText(string value)
        {
            ReplaceSelection(value ?? string.Empty);
            Debug.Log($"[TsukiVox Keyboard] Key applied to '{inputField.name}', length={(inputField.text ?? string.Empty).Length}.");
            FocusInputField();
        }

        private void Backspace()
        {
            if (inputField == null)
            {
                return;
            }

            var text = inputField.text ?? string.Empty;
            GetSelection(text.Length, out var start, out var end);
            if (start != end)
            {
                ReplaceSelection(string.Empty);
                FocusInputField();
                return;
            }

            if (start <= 0)
            {
                FocusInputField();
                return;
            }

            var textElements = StringInfo.ParseCombiningCharacters(text);
            var previous = 0;
            for (var index = 0; index < textElements.Length; index += 1)
            {
                if (textElements[index] >= start)
                {
                    break;
                }

                previous = textElements[index];
            }

            inputField.selectionStringAnchorPosition = previous;
            inputField.selectionStringFocusPosition = start;
            ReplaceSelection(string.Empty);
            FocusInputField();
        }

        private void ClearText()
        {
            if (inputField == null)
            {
                return;
            }

            inputField.text = string.Empty;
            SetCaret(0);
            FocusInputField();
        }

        private void ReplaceSelection(string value)
        {
            if (inputField == null)
            {
                return;
            }

            var current = inputField.text ?? string.Empty;
            GetSelection(current.Length, out var start, out var end);
            var insert = value;
            if (inputField.characterLimit > 0)
            {
                var available = Mathf.Max(0, inputField.characterLimit - (current.Length - (end - start)));
                if (insert.Length > available)
                {
                    insert = insert[..available];
                }
            }

            var next = current.Remove(start, end - start).Insert(start, insert);
            inputField.text = next;
            SetCaret(start + insert.Length);
        }

        private void GetSelection(int textLength, out int start, out int end)
        {
            var anchor = inputField != null ? inputField.selectionStringAnchorPosition : textLength;
            var focus = inputField != null ? inputField.selectionStringFocusPosition : textLength;
            start = Mathf.Clamp(Mathf.Min(anchor, focus), 0, textLength);
            end = Mathf.Clamp(Mathf.Max(anchor, focus), 0, textLength);
        }

        private void SetCaret(int position)
        {
            if (inputField == null)
            {
                return;
            }

            var clamped = Mathf.Clamp(position, 0, (inputField.text ?? string.Empty).Length);
            inputField.selectionStringAnchorPosition = clamped;
            inputField.selectionStringFocusPosition = clamped;
            inputField.stringPosition = clamped;
        }

        private void FocusInputField()
        {
            if (inputField == null || !inputField.IsActive() || !inputField.IsInteractable())
            {
                return;
            }

            CancelPendingDismiss();
            if (EventSystem.current != null &&
                !EventSystem.current.alreadySelecting &&
                EventSystem.current.currentSelectedGameObject != inputField.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(inputField.gameObject);
            }

            if (!inputField.isFocused)
            {
                inputField.ActivateInputField();
            }
            if (keyboardRoot != null)
            {
                keyboardRoot.SetAsLastSibling();
            }
        }

        private void SubmitEditing()
        {
            FinishEditing(submit: true, canceled: false);
        }

        private void CancelEditing()
        {
            FinishEditing(submit: false, canceled: true);
        }

        private void FinishEditing(bool submit, bool canceled)
        {
            if (isClosing)
            {
                return;
            }

            isClosing = true;
            CancelPendingDismiss();
            if (canceled && inputField != null)
            {
                inputField.text = originalText;
            }

            ReleaseKeyboard();
            if (inputField != null)
            {
                if (submit)
                {
                    inputField.onSubmit?.Invoke(inputField.text);
                }

                inputField.DeactivateInputField();
            }

            isClosing = false;
        }

        private void ReleaseKeyboard()
        {
            CancelPendingDismiss();
            if (activeInput == this)
            {
                activeInput = null;
            }

            letterLabels.Clear();
            keyVisuals.Clear();
            alphabetLayout = null;
            symbolLayout = null;
            if (keyboardRoot == null)
            {
                return;
            }

            var rootObject = keyboardRoot.gameObject;
            keyboardRoot = null;
            rootObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(rootObject);
            }
            else
            {
                DestroyImmediate(rootObject);
            }
        }

        private void RequestDeferredDismiss()
        {
            if (keyboardRoot == null)
            {
                return;
            }

            dismissRequestId += 1;
            if (dismissCoroutine != null)
            {
                StopCoroutine(dismissCoroutine);
            }

            dismissCoroutine = StartCoroutine(DismissAfterSelectionSettles(dismissRequestId));
            Debug.Log($"[TsukiVox Keyboard] Deselect deferred for '{inputField.name}'.");
        }

        private IEnumerator DismissAfterSelectionSettles(int requestId)
        {
            yield return null;
            dismissCoroutine = null;
            if (requestId != dismissRequestId || isClosing || keyboardRoot == null)
            {
                yield break;
            }

            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null ||
                selected == inputField.gameObject ||
                (selected != null && selected.transform.IsChildOf(keyboardRoot)))
            {
                yield break;
            }

            Debug.Log($"[TsukiVox Keyboard] Closing after focus moved to '{(selected != null ? selected.name : "none")}'.");
            HideKeyboard();
        }

        private void CancelPendingDismiss()
        {
            dismissRequestId += 1;
            if (dismissCoroutine == null)
            {
                return;
            }

            StopCoroutine(dismissCoroutine);
            dismissCoroutine = null;
        }

        private ColorBlock CreateKeyColors()
        {
            return new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = palette.KeyboardHighlighted,
                pressedColor = palette.KeyboardPressed,
                selectedColor = palette.KeyboardHighlighted,
                disabledColor = palette.KeyboardDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.04f,
            };
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var childObject = new GameObject(name, typeof(RectTransform));
            childObject.transform.SetParent(parent, false);
            var rect = childObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private readonly struct KeyDefinition
        {
            public KeyDefinition(
                string label,
                float width,
                UnityAction action,
                bool isUtility = false,
                bool isAccent = false,
                char letter = '\0')
            {
                Label = label;
                Width = width;
                Action = action;
                IsUtility = isUtility;
                IsAccent = isAccent;
                Letter = letter;
            }

            public string Label { get; }
            public float Width { get; }
            public UnityAction Action { get; }
            public bool IsUtility { get; }
            public bool IsAccent { get; }
            public char Letter { get; }
        }

        private readonly struct KeyboardKeyVisual
        {
            public KeyboardKeyVisual(
                QuestUiSurface surface,
                Outline outline,
                Button button,
                TMP_Text label,
                bool isUtility,
                bool isAccent)
            {
                Surface = surface;
                Outline = outline;
                Button = button;
                Label = label;
                IsUtility = isUtility;
                IsAccent = isAccent;
            }

            public QuestUiSurface Surface { get; }
            public Outline Outline { get; }
            public Button Button { get; }
            public TMP_Text Label { get; }
            public bool IsUtility { get; }
            public bool IsAccent { get; }
        }
        private readonly struct LetterLabel
        {
            public LetterLabel(char character, TMP_Text label)
            {
                Character = character;
                Label = label;
            }

            public char Character { get; }
            public TMP_Text Label { get; }
        }
    }
}
