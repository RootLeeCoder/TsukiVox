using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TsukiVox.AudioPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class QuestAndroidKeyboardInput : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_InputField inputField;

        private TouchScreenKeyboard fallbackKeyboard;
        private string originalText = string.Empty;
        private bool isClosing;

#if UNITY_ANDROID
        private const int AndroidInputTypeText = 0x00000001;
        private const int AndroidInputTypeNumber = 0x00000002;
        private const int AndroidTextFlagAutoCorrect = 0x00008000;
        private const int AndroidTextVariationPassword = 0x00000080;
        private const int AndroidImeActionDone = 0x00000006;
        private const int AndroidImeFlagNoExtractUi = 0x10000000;
        private const int AndroidShowImplicit = 0x00000001;

        private readonly object nativeStateLock = new object();
        private AndroidJavaObject androidActivity;
        private AndroidJavaObject nativeEditText;
        private NativeTextWatcher nativeTextWatcher;
        private NativeEditorActionListener nativeEditorActionListener;
        private string pendingNativeText;
        private string pendingNativeError;
        private bool hasPendingNativeText;
        private bool pendingNativeSubmit;
        private bool pendingFallback;
        private volatile bool nativeInputAttached;
        private volatile int nativeRequestId;
#endif

        public bool IsKeyboardOpen
        {
            get
            {
#if UNITY_ANDROID
                return nativeInputAttached || fallbackKeyboard != null;
#else
                return inputField != null && inputField.isFocused;
#endif
            }
        }

        public static QuestAndroidKeyboardInput Configure(TMP_InputField field)
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
            keyboardInput.ConfigureInputField();
            return keyboardInput;
        }

        private void Awake()
        {
            inputField = inputField != null ? inputField : GetComponent<TMP_InputField>();
            ConfigureInputField();
        }

        private void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            DrainNativeState();
            UpdateFallbackKeyboard();
#endif
        }

        private void OnDisable()
        {
            ReleaseKeyboard();
        }

        private void OnDestroy()
        {
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
                HideKeyboard();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                ShowKeyboard();
            }
        }

        public void ShowKeyboard()
        {
            if (inputField == null || !inputField.IsActive() || !inputField.IsInteractable() || inputField.readOnly)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (nativeInputAttached || fallbackKeyboard != null)
            {
                return;
            }

            originalText = inputField.text ?? string.Empty;
            if (!TryOpenNativeKeyboard(originalText))
            {
                OpenFallbackKeyboard();
            }
#else
            inputField.ActivateInputField();
#endif
        }

        public void HideKeyboard(bool submit = false)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (fallbackKeyboard != null)
            {
                ApplyKeyboardText(fallbackKeyboard.text);
            }

            FinishEditing(submit, canceled: false);
#else
            if (inputField == null)
            {
                return;
            }

            if (submit)
            {
                inputField.onSubmit?.Invoke(inputField.text);
            }

            inputField.DeactivateInputField();
#endif
        }

        private void ConfigureInputField()
        {
            if (inputField == null)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            inputField.shouldHideSoftKeyboard = true;
#endif
        }

        private void ApplyKeyboardText(string value)
        {
            if (inputField == null)
            {
                return;
            }

            var nextValue = value ?? string.Empty;
            if (inputField.characterLimit > 0 && nextValue.Length > inputField.characterLimit)
            {
                nextValue = nextValue[..inputField.characterLimit];
            }

            if (string.Equals(inputField.text, nextValue, StringComparison.Ordinal))
            {
                return;
            }

            inputField.text = nextValue;
            inputField.caretPosition = nextValue.Length;
            inputField.selectionAnchorPosition = nextValue.Length;
            inputField.selectionFocusPosition = nextValue.Length;
        }

        private void FinishEditing(bool submit, bool canceled)
        {
            if (isClosing)
            {
                return;
            }

            isClosing = true;
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
#if UNITY_ANDROID && !UNITY_EDITOR
            nativeRequestId += 1;
            CloseNativeInput();
            if (fallbackKeyboard != null)
            {
                fallbackKeyboard.active = false;
                fallbackKeyboard = null;
            }

            lock (nativeStateLock)
            {
                pendingFallback = false;
                pendingNativeSubmit = false;
                hasPendingNativeText = false;
            }
#else
            fallbackKeyboard = null;
#endif
        }

        private string ResolvePlaceholder()
        {
            if (inputField?.placeholder is TMP_Text placeholderText)
            {
                return placeholderText.text ?? string.Empty;
            }

            return string.Empty;
        }

#if UNITY_ANDROID
        private bool TryOpenNativeKeyboard(string initialText)
        {
            try
            {
                if (androidActivity == null)
                {
                    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    androidActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }

                if (androidActivity == null)
                {
                    return false;
                }

                nativeRequestId += 1;
                var requestId = nativeRequestId;
                androidActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    CreateNativeInputOnUiThread(requestId, initialText)));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TsukiVox Keyboard] Native keyboard setup failed: {exception.Message}");
                return false;
            }
        }

        private void CreateNativeInputOnUiThread(int requestId, string initialText)
        {
            if (requestId != nativeRequestId || androidActivity == null)
            {
                return;
            }

            try
            {
                var editText = new AndroidJavaObject("android.widget.EditText", androidActivity);
                editText.Call("setSingleLine", true);
                editText.Call("setMaxLines", 1);
                editText.Call("setFocusable", true);
                editText.Call("setFocusableInTouchMode", true);
                editText.Call("setShowSoftInputOnFocus", true);
                editText.Call("setInputType", ResolveAndroidInputType());
                editText.Call("setImeOptions", AndroidImeActionDone | AndroidImeFlagNoExtractUi);
                editText.Call("setTextColor", 0x00000000);
                editText.Call("setBackgroundColor", 0x00000000);
                editText.Call("setCursorVisible", false);
                editText.Call("setAlpha", 0.01f);
                editText.Call("setText", initialText ?? string.Empty);

                using var layoutParams = new AndroidJavaObject("android.widget.FrameLayout$LayoutParams", 1, 1);
                androidActivity.Call("addContentView", editText, layoutParams);
                nativeEditText = editText;
                nativeInputAttached = true;

                nativeTextWatcher = new NativeTextWatcher(value =>
                {
                    if (requestId == nativeRequestId)
                    {
                        QueueNativeText(value);
                    }
                });
                nativeEditorActionListener = new NativeEditorActionListener(() =>
                {
                    if (requestId == nativeRequestId)
                    {
                        QueueNativeSubmit();
                    }
                });
                editText.Call("addTextChangedListener", nativeTextWatcher);
                editText.Call("setOnEditorActionListener", nativeEditorActionListener);
                editText.Call("setSelection", (initialText ?? string.Empty).Length);
                editText.Call<bool>("requestFocus");

                editText.Call<bool>("postDelayed", new AndroidJavaRunnable(() =>
                    ShowNativeKeyboardOnUiThread(requestId, allowRetry: true)), 100L);
            }
            catch (Exception exception)
            {
                QueueNativeFailure(requestId, exception.Message);
            }
        }

        private void ShowNativeKeyboardOnUiThread(int requestId, bool allowRetry)
        {
            if (requestId != nativeRequestId || nativeEditText == null || androidActivity == null)
            {
                return;
            }

            try
            {
                nativeEditText.Call<bool>("requestFocus");
                using var inputMethodManager = androidActivity.Call<AndroidJavaObject>("getSystemService", "input_method");
                var shown = inputMethodManager != null &&
                            inputMethodManager.Call<bool>("showSoftInput", nativeEditText, AndroidShowImplicit);
                if (!shown && allowRetry)
                {
                    nativeEditText.Call<bool>("postDelayed", new AndroidJavaRunnable(() =>
                        ShowNativeKeyboardOnUiThread(requestId, allowRetry: false)), 250L);
                }
                else if (!shown)
                {
                    QueueNativeFailure(requestId, "InputMethodManager rejected the keyboard request.");
                }
            }
            catch (Exception exception)
            {
                QueueNativeFailure(requestId, exception.Message);
            }
        }

        private void CloseNativeInput()
        {
            nativeInputAttached = false;
            var editText = nativeEditText;
            nativeEditText = null;
            var textWatcher = nativeTextWatcher;
            nativeTextWatcher = null;
            nativeEditorActionListener = null;
            if (editText == null || androidActivity == null)
            {
                return;
            }

            androidActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    using var inputMethodManager = androidActivity.Call<AndroidJavaObject>("getSystemService", "input_method");
                    using var windowToken = editText.Call<AndroidJavaObject>("getWindowToken");
                    inputMethodManager?.Call<bool>("hideSoftInputFromWindow", windowToken, 0);
                    if (textWatcher != null)
                    {
                        editText.Call("removeTextChangedListener", textWatcher);
                    }

                    using var parent = editText.Call<AndroidJavaObject>("getParent");
                    parent?.Call("removeView", editText);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[TsukiVox Keyboard] Native keyboard cleanup failed: {exception.Message}");
                }
                finally
                {
                    editText.Dispose();
                }
            }));
        }

        private int ResolveAndroidInputType()
        {
            if (inputField == null)
            {
                return AndroidInputTypeText | AndroidTextFlagAutoCorrect;
            }

            switch (inputField.contentType)
            {
                case TMP_InputField.ContentType.IntegerNumber:
                case TMP_InputField.ContentType.DecimalNumber:
                    return AndroidInputTypeNumber;
                case TMP_InputField.ContentType.Password:
                case TMP_InputField.ContentType.Pin:
                    return AndroidInputTypeText | AndroidTextVariationPassword;
                default:
                    return AndroidInputTypeText | AndroidTextFlagAutoCorrect;
            }
        }

        private void QueueNativeText(string value)
        {
            lock (nativeStateLock)
            {
                pendingNativeText = value ?? string.Empty;
                hasPendingNativeText = true;
            }
        }

        private void QueueNativeSubmit()
        {
            lock (nativeStateLock)
            {
                pendingNativeSubmit = true;
            }
        }

        private void QueueNativeFailure(int requestId, string error)
        {
            if (requestId != nativeRequestId)
            {
                return;
            }

            lock (nativeStateLock)
            {
                pendingNativeError = error;
                pendingFallback = true;
            }
        }

        private void DrainNativeState()
        {
            string nextText = null;
            string nativeError = null;
            var submit = false;
            var useFallback = false;
            lock (nativeStateLock)
            {
                if (hasPendingNativeText)
                {
                    nextText = pendingNativeText;
                    hasPendingNativeText = false;
                }

                submit = pendingNativeSubmit;
                pendingNativeSubmit = false;
                useFallback = pendingFallback;
                pendingFallback = false;
                nativeError = pendingNativeError;
                pendingNativeError = null;
            }

            if (nextText != null)
            {
                ApplyKeyboardText(nextText);
            }

            if (submit)
            {
                FinishEditing(submit: true, canceled: false);
                return;
            }

            if (useFallback)
            {
                Debug.LogWarning($"[TsukiVox Keyboard] Native IME unavailable, using Unity fallback: {nativeError}");
                CloseNativeInput();
                OpenFallbackKeyboard();
            }
        }

        private void OpenFallbackKeyboard()
        {
            if (fallbackKeyboard != null || inputField == null)
            {
                return;
            }

            TouchScreenKeyboard.hideInput = false;
            fallbackKeyboard = TouchScreenKeyboard.Open(
                inputField.text ?? string.Empty,
                inputField.keyboardType,
                inputField.inputType == TMP_InputField.InputType.AutoCorrect,
                inputField.lineType != TMP_InputField.LineType.SingleLine,
                inputField.inputType == TMP_InputField.InputType.Password,
                false,
                ResolvePlaceholder(),
                inputField.characterLimit);
        }

        private void UpdateFallbackKeyboard()
        {
            if (fallbackKeyboard == null)
            {
                return;
            }

            ApplyKeyboardText(fallbackKeyboard.text);
            switch (fallbackKeyboard.status)
            {
                case TouchScreenKeyboard.Status.Done:
                    FinishEditing(submit: true, canceled: false);
                    break;
                case TouchScreenKeyboard.Status.Canceled:
                    FinishEditing(submit: false, canceled: true);
                    break;
                case TouchScreenKeyboard.Status.LostFocus:
                    FinishEditing(submit: false, canceled: false);
                    break;
            }
        }

        private sealed class NativeTextWatcher : AndroidJavaProxy
        {
            private readonly Action<string> onTextChangedCallback;

            public NativeTextWatcher(Action<string> onTextChanged)
                : base("android.text.TextWatcher")
            {
                onTextChangedCallback = onTextChanged;
            }

            public void beforeTextChanged(AndroidJavaObject value, int start, int count, int after)
            {
            }

            public void onTextChanged(AndroidJavaObject value, int start, int before, int count)
            {
                onTextChangedCallback?.Invoke(value?.Call<string>("toString") ?? string.Empty);
            }

            public void afterTextChanged(AndroidJavaObject value)
            {
            }
        }

        private sealed class NativeEditorActionListener : AndroidJavaProxy
        {
            private readonly Action onSubmitCallback;

            public NativeEditorActionListener(Action onSubmit)
                : base("android.widget.TextView$OnEditorActionListener")
            {
                onSubmitCallback = onSubmit;
            }

            public bool onEditorAction(AndroidJavaObject view, int actionId, AndroidJavaObject keyEvent)
            {
                if (actionId == AndroidImeActionDone || keyEvent != null)
                {
                    onSubmitCallback?.Invoke();
                    return true;
                }

                return false;
            }
        }
#endif
    }
}
