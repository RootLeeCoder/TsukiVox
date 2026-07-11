using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

using InputSystemCommonUsages = UnityEngine.InputSystem.CommonUsages;
using InputSystemDevice = UnityEngine.InputSystem.InputDevice;

namespace TsukiVox.AudioPrototype
{
    public sealed class QuestUiPointer : MonoBehaviour
    {
        private const int PointerId = -32025;
        private const float TriggerPressThreshold = 0.65f;

        private static readonly Vector3 PanelWorldPosition = QuestAppShellPrototype.ControlPanelWorldPosition;
        private static readonly Quaternion PanelWorldRotation = QuestAppShellPrototype.ControlPanelWorldRotation;
        private static readonly Vector3 PanelWorldScale = QuestAppShellPrototype.ControlPanelWorldScale;

        private static readonly Color PointerIdleColor = new Color(0.32f, 0.92f, 1f, 0.88f);
        private static readonly Color PointerHoverColor = new Color(0.52f, 1f, 0.84f, 1f);
        private static readonly Color PointerPressedColor = new Color(1f, 0.84f, 0.28f, 1f);
        private static readonly Color PointerMissColor = new Color(0.26f, 0.42f, 0.48f, 0.45f);

        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private GraphicRaycaster raycaster;
        [SerializeField] private UnityEngine.XR.XRNode preferredHand = UnityEngine.XR.XRNode.RightHand;
        [SerializeField] private float maxPointerDistance = 4.5f;

        // VRSing controllers.ts: ray starts hidden (isPointerVisible = false), toggled via squeezeend.
        private bool isPointerVisibleLeft;
        private bool isPointerVisibleRight;
        private bool wasGripPressedLeft;
        private bool wasGripPressedRight;

        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
        private bool isConfigured;
        private bool wasPressed;
        private bool hasLastPointerPosition;
        private bool isDragging;
        private bool eligibleForClick;
        private Vector2 lastPointerPosition;
        private Vector2 pressPosition;
        private RaycastResult pressRaycast;
        private PointerEventData pointerEventData;
        private GameObject hoveredObject;
        private GameObject pressedObject;
        private GameObject rawPressedObject;
        private GameObject draggedObject;
        private LineRenderer pointerLine;
        private Transform reticle;
        private Material pointerMaterial;

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
            if (!isConfigured || targetCanvas == null || raycaster == null || targetCanvas.worldCamera == null)
            {
                ConfigureSceneReferences();
            }

            UpdateGripToggle();
            UpdateControllerPointer();
        }

        private void UpdateGripToggle()
        {
            // VRSing controllers.ts onPointerToggle: squeeze button toggles ray visibility per hand.
            UpdateGripToggleForHand(rightHand: true, ref wasGripPressedRight, ref isPointerVisibleRight);
            UpdateGripToggleForHand(rightHand: false, ref wasGripPressedLeft, ref isPointerVisibleLeft);
        }

        private static void UpdateGripToggleForHand(bool rightHand, ref bool wasPressed, ref bool isVisible)
        {
            var device = GetControllerDevice(rightHand);
            if (device == null)
            {
                wasPressed = false;
                return;
            }

            var pressed = IsButtonPressed(device, "gripButton") || IsButtonPressed(device, "gripPressed");
            if (pressed && !wasPressed)
            {
                isVisible = !isVisible;
            }

            wasPressed = pressed;
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
            EnsurePointerEventData();
            EnsurePointerVisuals();
            ConfigureSelectableFeedback(targetCanvas.transform);
            isConfigured = true;
        }

        private static void ConfigureCanvas(Canvas canvas)
        {
            var camera = Camera.main;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;
            canvas.sortingOrder = 10;

            var rect = canvas.GetComponent<RectTransform>();
            canvas.transform.SetParent(null, false);
            rect.position = PanelWorldPosition;
            rect.rotation = PanelWorldRotation;
            rect.localScale = PanelWorldScale;
            rect.sizeDelta = QuestAppShellPrototype.ControlPanelSize;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.matchWidthOrHeight = 0.5f;
                scaler.dynamicPixelsPerUnit = 12f;
            }

            var canvasRaycaster = canvas.GetComponent<GraphicRaycaster>();
            if (canvasRaycaster == null)
            {
                canvasRaycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            canvasRaycaster.ignoreReversedGraphics = false;
        }

        private void UpdateControllerPointer()
        {
            if (!isConfigured || targetCanvas == null || raycaster == null || EventSystem.current == null)
            {
                HidePointerVisuals();
                return;
            }

            if (!TryGetPointerPose(out var device, out var origin, out var rotation))
            {
                if (wasPressed)
                {
                    ProcessPointerRelease(null);
                }

                ClearHover();
                HidePointerVisuals();
                wasPressed = false;
                return;
            }

            var pressed = IsControllerTriggerPressed(device);
            var ray = new Ray(origin, rotation * Vector3.forward);
            var hasCanvasHit = TryGetCanvasHit(ray, out var canvasHitPoint, out _);

            var currentRaycast = default(RaycastResult);
            var currentTarget = hasCanvasHit ? RaycastCanvas(canvasHitPoint, out currentRaycast) : null;
            if (!hasCanvasHit)
            {
                currentRaycast = default;
            }

            ProcessHover(currentTarget);

            if (pressed && !wasPressed)
            {
                ProcessPointerPress(currentTarget, currentRaycast);
            }
            else if (pressed && wasPressed)
            {
                ProcessPointerHold();
            }
            else if (!pressed && wasPressed)
            {
                ProcessPointerRelease(currentTarget);
            }

            wasPressed = pressed;
            UpdatePointerVisuals(origin, hasCanvasHit ? canvasHitPoint : origin + ray.direction * maxPointerDistance, hasCanvasHit, currentTarget != null, pressed);
        }

        private void ProcessPointerPress(GameObject currentTarget, RaycastResult currentRaycast)
        {
            if (pointerEventData == null)
            {
                return;
            }

            pressPosition = pointerEventData.position;
            pressRaycast = currentRaycast;
            eligibleForClick = currentTarget != null;
            pointerEventData.pressPosition = pressPosition;
            pointerEventData.pointerPressRaycast = pressRaycast;
            pointerEventData.eligibleForClick = eligibleForClick;

            rawPressedObject = currentTarget;
            pressedObject = currentTarget != null
                ? ExecuteEvents.ExecuteHierarchy(currentTarget, pointerEventData, ExecuteEvents.pointerDownHandler)
                : null;

            if (pressedObject == null && currentTarget != null)
            {
                pressedObject = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentTarget);
            }

            pointerEventData.pointerPress = pressedObject;
            pointerEventData.rawPointerPress = rawPressedObject;

            draggedObject = currentTarget != null ? ExecuteEvents.GetEventHandler<IDragHandler>(currentTarget) : null;
            pointerEventData.pointerDrag = draggedObject;
            isDragging = false;
            if (draggedObject != null)
            {
                ExecuteEvents.Execute(draggedObject, pointerEventData, ExecuteEvents.initializePotentialDrag);
            }

            if (EventSystem.current != null && (pressedObject != null || currentTarget != null))
            {
                EventSystem.current.SetSelectedGameObject(pressedObject != null ? pressedObject : currentTarget);
            }
        }

        private void ProcessPointerHold()
        {
            if (pointerEventData == null || draggedObject == null)
            {
                return;
            }

            pointerEventData.pressPosition = pressPosition;
            pointerEventData.pointerPressRaycast = pressRaycast;
            pointerEventData.pointerPress = pressedObject;
            pointerEventData.rawPointerPress = rawPressedObject;
            pointerEventData.pointerDrag = draggedObject;

            if (!isDragging)
            {
                ExecuteEvents.Execute(draggedObject, pointerEventData, ExecuteEvents.beginDragHandler);
                isDragging = true;
            }

            ExecuteEvents.Execute(draggedObject, pointerEventData, ExecuteEvents.dragHandler);
        }

        private void ProcessPointerRelease(GameObject currentTarget)
        {
            if (pointerEventData == null)
            {
                return;
            }

            pointerEventData.pressPosition = pressPosition;
            pointerEventData.pointerPressRaycast = pressRaycast;
            pointerEventData.pointerPress = pressedObject;
            pointerEventData.rawPointerPress = rawPressedObject;
            pointerEventData.pointerDrag = draggedObject;
            pointerEventData.eligibleForClick = eligibleForClick;

            if (pressedObject != null)
            {
                ExecuteEvents.Execute(pressedObject, pointerEventData, ExecuteEvents.pointerUpHandler);
            }

            var clickTarget = currentTarget != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentTarget) : null;
            if (eligibleForClick && pressedObject != null && pressedObject == clickTarget)
            {
                ExecuteEvents.Execute(pressedObject, pointerEventData, ExecuteEvents.pointerClickHandler);
            }

            if (isDragging && draggedObject != null)
            {
                ExecuteEvents.Execute(draggedObject, pointerEventData, ExecuteEvents.endDragHandler);
            }

            pressedObject = null;
            rawPressedObject = null;
            draggedObject = null;
            isDragging = false;
            eligibleForClick = false;
        }

        private void ProcessHover(GameObject currentTarget)
        {
            var nextHovered = currentTarget != null ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(currentTarget) : null;
            nextHovered = nextHovered != null ? nextHovered : currentTarget;
            if (nextHovered == hoveredObject)
            {
                if (pointerEventData != null)
                {
                    pointerEventData.pointerEnter = hoveredObject;
                }

                return;
            }

            if (hoveredObject != null && pointerEventData != null)
            {
                ExecuteEvents.Execute(hoveredObject, pointerEventData, ExecuteEvents.pointerExitHandler);
            }

            hoveredObject = nextHovered;
            if (hoveredObject != null && pointerEventData != null)
            {
                pointerEventData.pointerEnter = hoveredObject;
                ExecuteEvents.Execute(hoveredObject, pointerEventData, ExecuteEvents.pointerEnterHandler);
            }
        }

        private void ClearHover()
        {
            if (hoveredObject != null && pointerEventData != null)
            {
                ExecuteEvents.Execute(hoveredObject, pointerEventData, ExecuteEvents.pointerExitHandler);
            }

            hoveredObject = null;
        }

        private GameObject RaycastCanvas(Vector3 worldPoint, out RaycastResult currentRaycast)
        {
            currentRaycast = default;
            var camera = targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main;
            if (camera == null)
            {
                return null;
            }

            EnsurePointerEventData();
            if (pointerEventData == null)
            {
                return null;
            }

            var cameraPoint = camera.WorldToScreenPoint(worldPoint);
            if (cameraPoint.z <= camera.nearClipPlane)
            {
                return null;
            }

            var screenPoint = new Vector2(cameraPoint.x, cameraPoint.y);
            pointerEventData.Reset();
            pointerEventData.pointerId = PointerId;
            pointerEventData.button = PointerEventData.InputButton.Left;
            pointerEventData.position = screenPoint;
            pointerEventData.delta = hasLastPointerPosition ? screenPoint - lastPointerPosition : Vector2.zero;
            pointerEventData.scrollDelta = Vector2.zero;
            pointerEventData.pressPosition = pressPosition;
            pointerEventData.pointerPressRaycast = pressRaycast;
            pointerEventData.pointerPress = pressedObject;
            pointerEventData.rawPointerPress = rawPressedObject;
            pointerEventData.pointerDrag = draggedObject;
            pointerEventData.eligibleForClick = eligibleForClick;

            lastPointerPosition = screenPoint;
            hasLastPointerPosition = true;

            raycastResults.Clear();
            raycaster.Raycast(pointerEventData, raycastResults);
            if (raycastResults.Count == 0)
            {
                return null;
            }

            currentRaycast = raycastResults[0];
            pointerEventData.pointerCurrentRaycast = currentRaycast;
            return currentRaycast.gameObject;
        }

        private bool TryGetCanvasHit(Ray ray, out Vector3 hitPoint, out float distance)
        {
            hitPoint = Vector3.zero;
            distance = maxPointerDistance;
            if (targetCanvas == null)
            {
                return false;
            }

            var canvasPlane = new Plane(targetCanvas.transform.forward, targetCanvas.transform.position);
            if (!canvasPlane.Raycast(ray, out distance) || distance < 0f || distance > maxPointerDistance)
            {
                return false;
            }

            hitPoint = ray.GetPoint(distance);
            var rect = targetCanvas.GetComponent<RectTransform>();
            return rect == null || RectTransformUtility.RectangleContainsScreenPoint(rect, GetScreenPoint(hitPoint), targetCanvas.worldCamera);
        }

        private Vector2 GetScreenPoint(Vector3 worldPoint)
        {
            var camera = targetCanvas != null && targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main;
            return camera != null
                ? (Vector2)camera.WorldToScreenPoint(worldPoint)
                : RectTransformUtility.WorldToScreenPoint(null, worldPoint);
        }

        private bool TryGetPointerPose(out InputSystemDevice device, out Vector3 position, out Quaternion rotation)
        {
            var preferRightHand = preferredHand != UnityEngine.XR.XRNode.LeftHand;
            if (TryGetPointerPoseForHand(preferRightHand, out device, out position, out rotation))
            {
                // Only return the pose if this hand's ray is toggled on.
                var isVisible = preferRightHand ? isPointerVisibleRight : isPointerVisibleLeft;
                if (isVisible)
                {
                    return true;
                }
            }

            // Fallback to the other hand if the preferred hand's ray is hidden.
            if (TryGetPointerPoseForHand(!preferRightHand, out device, out position, out rotation))
            {
                var isVisible = preferRightHand ? isPointerVisibleLeft : isPointerVisibleRight;
                return isVisible;
            }

            return false;
        }

        private static bool TryGetPointerPoseForHand(bool rightHand, out InputSystemDevice device, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            var controller = rightHand ? XRController.rightHand : XRController.leftHand;
            if (controller != null && TryReadPointerPose(controller, out position, out rotation))
            {
                device = controller;
                return true;
            }

            var devices = InputSystem.devices;
            for (var i = 0; i < devices.Count; i += 1)
            {
                var candidate = devices[i];
                if (candidate is not XRController || !HasHandUsage(candidate, rightHand))
                {
                    continue;
                }

                if (TryReadPointerPose(candidate, out position, out rotation))
                {
                    device = candidate;
                    return true;
                }
            }

            device = null;
            return false;
        }

        private static InputSystemDevice GetControllerDevice(bool rightHand)
        {
            var controller = rightHand ? XRController.rightHand : XRController.leftHand;
            if (controller != null && IsControllerTracked(controller))
            {
                return controller;
            }

            var devices = InputSystem.devices;
            for (var i = 0; i < devices.Count; i += 1)
            {
                var candidate = devices[i];
                if (candidate is XRController && HasHandUsage(candidate, rightHand) && IsControllerTracked(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool TryReadPointerPose(InputSystemDevice device, out Vector3 position, out Quaternion rotation)
        {
            if (device == null || !IsControllerTracked(device))
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }

            return TryReadPoseControls(device, "pointerPosition", "pointerRotation", out position, out rotation) ||
                   TryReadPoseControls(device, "devicePosition", "deviceRotation", out position, out rotation);
        }

        private static bool TryReadPoseControls(InputSystemDevice device, string positionName, string rotationName, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            var positionControl = device.TryGetChildControl<Vector3Control>(positionName);
            var rotationControl = device.TryGetChildControl<QuaternionControl>(rotationName);
            if (positionControl == null || rotationControl == null)
            {
                return false;
            }

            var rawRotation = rotationControl.ReadValue();
            if (!IsUsableRotation(rawRotation))
            {
                return false;
            }

            position = positionControl.ReadValue();
            rotation = NormalizeRotation(rawRotation);
            return IsFinite(position) && IsUsableRotation(rotation);
        }

        private static bool IsControllerTracked(InputSystemDevice device)
        {
            var isTracked = device.TryGetChildControl<ButtonControl>("isTracked");
            if (isTracked != null && !isTracked.isPressed)
            {
                return false;
            }

            var trackingState = device.TryGetChildControl<IntegerControl>("trackingState");
            if (trackingState == null)
            {
                return true;
            }

            var state = trackingState.ReadValue();
            var required = (int)(UnityEngine.XR.InputTrackingState.Position | UnityEngine.XR.InputTrackingState.Rotation);
            return (state & required) == required;
        }

        private static bool IsControllerTriggerPressed(InputSystemDevice device)
        {
            if (IsButtonPressed(device, "triggerPressed"))
            {
                return true;
            }

            var trigger = device.TryGetChildControl<AxisControl>("trigger");
            return trigger != null && trigger.ReadValue() >= TriggerPressThreshold;
        }

        private static bool IsButtonPressed(InputSystemDevice device, string controlName)
        {
            var button = device.TryGetChildControl<ButtonControl>(controlName);
            return button != null && button.isPressed;
        }

        private static bool HasHandUsage(InputSystemDevice device, bool rightHand)
        {
            var targetUsage = rightHand ? InputSystemCommonUsages.RightHand : InputSystemCommonUsages.LeftHand;
            var usages = device.usages;
            for (var i = 0; i < usages.Count; i += 1)
            {
                if (usages[i] == targetUsage)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsUsableRotation(Quaternion rotation)
        {
            return IsFinite(rotation.x) &&
                   IsFinite(rotation.y) &&
                   IsFinite(rotation.z) &&
                   IsFinite(rotation.w) &&
                   rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w > 0.0001f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Quaternion NormalizeRotation(Quaternion rotation)
        {
            var magnitude = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w);
            if (magnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            return new Quaternion(rotation.x / magnitude, rotation.y / magnitude, rotation.z / magnitude, rotation.w / magnitude);
        }

        private void EnsurePointerEventData()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            if (pointerEventData == null || pointerEventData.pointerId != PointerId)
            {
                pointerEventData = new PointerEventData(EventSystem.current)
                {
                    pointerId = PointerId,
                    button = PointerEventData.InputButton.Left,
                };
            }
        }

        private void EnsurePointerVisuals()
        {
            if (pointerLine != null && reticle != null)
            {
                return;
            }

            var shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            pointerMaterial = new Material(shader)
            {
                color = PointerIdleColor,
            };

            var lineObject = new GameObject("Quest Controller UI Ray");
            lineObject.transform.SetParent(transform, false);
            pointerLine = lineObject.AddComponent<LineRenderer>();
            pointerLine.sharedMaterial = pointerMaterial;
            pointerLine.positionCount = 2;
            pointerLine.useWorldSpace = true;
            pointerLine.startWidth = 0.012f;
            pointerLine.endWidth = 0.004f;
            pointerLine.numCapVertices = 4;
            pointerLine.enabled = false;

            var reticleObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reticleObject.name = "Quest UI Reticle";
            reticleObject.transform.SetParent(transform, false);
            reticle = reticleObject.transform;
            reticle.localScale = Vector3.one * 0.035f;
            var collider = reticleObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = reticleObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = pointerMaterial;
            }

            reticleObject.SetActive(false);
        }

        private void UpdatePointerVisuals(Vector3 origin, Vector3 endPoint, bool hasCanvasHit, bool hasTarget, bool pressed)
        {
            // The grip toggle now controls ray visibility; always draw when a pose is active.
            EnsurePointerVisuals();
            var color = !hasCanvasHit ? PointerMissColor : pressed ? PointerPressedColor : hasTarget ? PointerHoverColor : PointerIdleColor;
            if (pointerMaterial != null)
            {
                pointerMaterial.color = color;
            }

            pointerLine.enabled = true;
            pointerLine.SetPosition(0, origin);
            pointerLine.SetPosition(1, endPoint);

            if (reticle != null)
            {
                reticle.gameObject.SetActive(hasCanvasHit);
                reticle.position = endPoint;
            }
        }

        private void HidePointerVisuals()
        {
            if (pointerLine != null)
            {
                pointerLine.enabled = false;
            }

            if (reticle != null)
            {
                reticle.gameObject.SetActive(false);
            }

            hasLastPointerPosition = false;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                var standalone = EventSystem.current.GetComponent<StandaloneInputModule>();
                if (standalone != null)
                {
                    Destroy(standalone);
                }

                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                {
                    EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
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
