using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    public sealed class QuestUiPointer : MonoBehaviour
    {
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private GraphicRaycaster raycaster;

        private bool isConfigured;

        public static QuestUiPointer EnsureScenePointer()
        {
            var existing = FindAnyObjectByType<QuestUiPointer>();
            if (existing != null)
            {
                existing.ConfigureSceneReferences();
                return existing;
            }

            var pointerObject = new GameObject("Quest UI Pointer");
            var pointer = pointerObject.AddComponent<QuestUiPointer>();
            pointer.ConfigureSceneReferences();
            return pointer;
        }

        private void Awake()
        {
            ConfigureSceneReferences();
        }

        private void Update()
        {
            if (!isConfigured)
            {
                ConfigureSceneReferences();
            }
        }

        private void ConfigureSceneReferences()
        {
            targetCanvas = targetCanvas != null ? targetCanvas : FindPrototypeCanvas();
            if (targetCanvas == null)
            {
                return;
            }

            ConfigureCanvas(targetCanvas);
            raycaster = raycaster != null ? raycaster : targetCanvas.GetComponent<GraphicRaycaster>();
            EnsureEventSystem();
            ConfigureSelectableFeedback(targetCanvas.transform);
            isConfigured = true;
        }

        private static void ConfigureCanvas(Canvas canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.planeDistance = 100f;
            canvas.sortingOrder = 10;

            var rect = canvas.GetComponent<RectTransform>();
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(1280f, 720f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.matchWidthOrHeight = 0.5f;
                scaler.dynamicPixelsPerUnit = 12f;
            }

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static Canvas FindPrototypeCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            Canvas firstRaycastCanvas = null;
            for (var i = 0; i < canvases.Length; i += 1)
            {
                if (canvases[i].name == "Prototype Canvas")
                {
                    return canvases[i];
                }

                if (firstRaycastCanvas == null && canvases[i].GetComponent<GraphicRaycaster>() != null)
                {
                    firstRaycastCanvas = canvases[i];
                }
            }

            return firstRaycastCanvas != null ? firstRaycastCanvas : (canvases.Length > 0 ? canvases[0] : null);
        }

        private static void ConfigureSelectableFeedback(Transform root)
        {
            var colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.5f, 1f, 0.95f, 1f),
                pressedColor = new Color(1f, 0.88f, 0.34f, 1f),
                selectedColor = new Color(0.6f, 0.95f, 1f, 1f),
                disabledColor = new Color(0.45f, 0.5f, 0.52f, 0.45f),
                colorMultiplier = 1f,
                fadeDuration = 0.05f,
            };

            var selectables = root.GetComponentsInChildren<Selectable>(true);
            for (var i = 0; i < selectables.Length; i += 1)
            {
                selectables[i].colors = colors;
            }
        }
    }
}
