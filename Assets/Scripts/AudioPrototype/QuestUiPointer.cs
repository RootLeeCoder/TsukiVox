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
        private const float ClickReleaseTolerancePixels = 42f;

        private static readonly Vector3 PanelWorldPosition = QuestAppShellPrototype.ControlPanelWorldPosition;
        private static readonly Quaternion PanelWorldRotation = QuestAppShellPrototype.ControlPanelWorldRotation;
        private static readonly Vector3 PanelWorldScale = QuestAppShellPrototype.ControlPanelWorldScale;

        private static readonly Color PointerIdleColor = new Color32(146, 156, 175, 224);
        private static readonly Color PointerHoverColor = new Color32(237, 226, 204, 255);
        private static readonly Color PointerPressedColor = new Color32(184, 158, 109, 255);
        private static readonly Color PointerMissColor = new Color32(89, 97, 111, 115);

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
        private readonly List<Canvas> interactionCanvases = new List<Canvas>();
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
        private float nextCanvasRefreshAt;

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

            if (Time.unscaledTime >= nextCanvasRefreshAt)
            {
                nextCanvasRefreshAt = Time.unscaledTime + 0.5f;
                if (NeedsCanvasRefresh())
                {
                    RefreshInteractionCanvases();
                }
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
            RefreshInteractionCanvases();
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
            if (!IsMountedOnTablet(canvas.transform))
            {
                rect.position = PanelWorldPosition;
                rect.rotation = PanelWorldRotation;
                rect.localScale = PanelWorldScale;
            }

            rect.sizeDelta = QuestAppShellPrototype.ControlPanelSize;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = QuestAppShellPrototype.ControlPanelSize;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.dynamicPixelsPerUnit = 24f;
            }

            var canvasRaycaster = canvas.GetComponent<GraphicRaycaster>();
            if (canvasRaycaster == null)
            {
                canvasRaycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            canvasRaycaster.ignoreReversedGraphics = false;
        }

        private static bool IsMountedOnTablet(Transform canvasTransform)
        {
            var current = canvasTransform.parent;
            while (current != null)
            {
                if (current.name == QuestTabletTiltController.TabletPivotName)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void UpdateControllerPointer()
        {
            if (!isConfigured || interactionCanvases.Count == 0 || EventSystem.current == null)
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
            var hasCanvasHit = TryRaycastCanvases(ray, out var canvasHitPoint, out var currentTarget, out var currentRaycast);

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
                var selectionTarget = pressedObject != null ? pressedObject : currentTarget;
                if (!QuestAndroidKeyboardInput.IsKeyboardTarget(selectionTarget))
                {
                    EventSystem.current.SetSelectedGameObject(selectionTarget, pointerEventData);
                }
            }
        }

        private void ProcessPointerHold()
        {
            if (pointerEventData == null)
            {
                return;
            }

            if (eligibleForClick &&
                Vector2.Distance(pointerEventData.position, pressPosition) > ClickReleaseTolerancePixels)
            {
                eligibleForClick = false;
                pointerEventData.eligibleForClick = false;
            }

            if (draggedObject == null)
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
            if (!pointerEventData.eligibleForClick)
            {
                eligibleForClick = false;
            }
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
            var releasedNearPress = Vector2.Distance(pointerEventData.position, pressPosition) <= ClickReleaseTolerancePixels;
            if (eligibleForClick &&
                pressedObject != null &&
                (pressedObject == clickTarget || (clickTarget == null && releasedNearPress)))
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

        private GameObject RaycastCanvas(
            Canvas canvas,
            GraphicRaycaster canvasRaycaster,
            Vector3 worldPoint,
            out RaycastResult currentRaycast)
        {
            currentRaycast = default;
            if (canvas == null || canvasRaycaster == null)
            {
                return null;
            }

            if (!PreparePointerEventData(canvas, worldPoint))
            {
                return null;
            }

            raycastResults.Clear();
            canvasRaycaster.Raycast(pointerEventData, raycastResults);
            if (raycastResults.Count == 0)
            {
                return null;
            }

            currentRaycast = raycastResults[0];
            pointerEventData.pointerCurrentRaycast = currentRaycast;
            return currentRaycast.gameObject;
        }

        private bool TryRaycastCanvases(
            Ray ray,
            out Vector3 hitPoint,
            out GameObject currentTarget,
            out RaycastResult currentRaycast)
        {
            hitPoint = ray.origin + ray.direction * maxPointerDistance;
            currentTarget = null;
            currentRaycast = default;
            var nearestSurfaceDistance = float.MaxValue;
            var nearestTargetDistance = float.MaxValue;
            var nearestSurfacePoint = hitPoint;
            var nearestTargetPoint = hitPoint;
            Canvas nearestSurfaceCanvas = null;
            Canvas nearestTargetCanvas = null;
            var hasSurfaceHit = false;

            for (var index = 0; index < interactionCanvases.Count; index += 1)
            {
                var canvas = interactionCanvases[index];
                if (!TryGetCanvasHit(canvas, ray, out var candidatePoint, out var candidateDistance))
                {
                    continue;
                }

                hasSurfaceHit = true;
                if (candidateDistance < nearestSurfaceDistance)
                {
                    nearestSurfaceDistance = candidateDistance;
                    nearestSurfacePoint = candidatePoint;
                    nearestSurfaceCanvas = canvas;
                }

                var canvasRaycaster = canvas.GetComponent<GraphicRaycaster>();
                var candidateTarget = RaycastCanvas(canvas, canvasRaycaster, candidatePoint, out var candidateRaycast);
                if (candidateTarget == null || candidateDistance >= nearestTargetDistance)
                {
                    continue;
                }

                nearestTargetDistance = candidateDistance;
                nearestTargetPoint = candidatePoint;
                nearestTargetCanvas = canvas;
                currentTarget = candidateTarget;
                currentRaycast = candidateRaycast;
            }

            if (!hasSurfaceHit)
            {
                return false;
            }

            var selectedCanvas = currentTarget != null ? nearestTargetCanvas : nearestSurfaceCanvas;
            hitPoint = currentTarget != null ? nearestTargetPoint : nearestSurfacePoint;
            if (selectedCanvas != null && PreparePointerEventData(selectedCanvas, hitPoint))
            {
                pointerEventData.pointerCurrentRaycast = currentRaycast;
                lastPointerPosition = pointerEventData.position;
                hasLastPointerPosition = true;
            }

            return true;
        }

        private bool TryGetCanvasHit(Canvas canvas, Ray ray, out Vector3 hitPoint, out float distance)
        {
            hitPoint = Vector3.zero;
            distance = maxPointerDistance;
            if (canvas == null || !canvas.isActiveAndEnabled)
            {
                return false;
            }

            var camera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            var canvasPlane = new Plane(canvas.transform.forward, canvas.transform.position);
            if (camera == null ||
                !canvasPlane.Raycast(ray, out distance) ||
                distance < 0f ||
                distance > maxPointerDistance)
            {
                return false;
            }

            hitPoint = ray.GetPoint(distance);
            var rect = canvas.GetComponent<RectTransform>();
            var screenPoint = camera.WorldToScreenPoint(hitPoint);
            return screenPoint.z > camera.nearClipPlane &&
                   (rect == null || RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, camera));
        }

        private bool PreparePointerEventData(Canvas canvas, Vector3 worldPoint)
        {
            var camera = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (camera == null)
            {
                return false;
            }

            EnsurePointerEventData();
            if (pointerEventData == null)
            {
                return false;
            }

            var cameraPoint = camera.WorldToScreenPoint(worldPoint);
            if (cameraPoint.z <= camera.nearClipPlane)
            {
                return false;
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
            return true;
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
            if (!IsFinite(position) || !IsUsableRotation(rotation))
            {
                return false;
            }

            QuestXrBootstrap.TransformTrackingPose(ref position, ref rotation);
            return true;
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
            if (pointerLine == null)
            {
                var existingLine = transform.Find("Quest Controller UI Ray");
                pointerLine = existingLine != null ? existingLine.GetComponent<LineRenderer>() : null;
            }

            if (reticle == null)
            {
                reticle = transform.Find("Quest UI Reticle");
            }

            if (pointerMaterial == null)
            {
                pointerMaterial = pointerLine != null ? pointerLine.sharedMaterial : null;
                if (pointerMaterial == null && reticle != null && reticle.TryGetComponent<MeshRenderer>(out var existingRenderer))
                {
                    pointerMaterial = existingRenderer.sharedMaterial;
                }
            }

            if (pointerLine != null && reticle != null)
            {
                return;
            }

            if (pointerMaterial == null)
            {
                var shader = Shader.Find("Unlit/Color");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                pointerMaterial = new Material(shader)
                {
                    color = PointerIdleColor,
                };
            }

            if (pointerLine == null)
            {
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
            }

            if (reticle == null)
            {
                var reticleObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                reticleObject.name = "Quest UI Reticle";
                reticleObject.transform.SetParent(transform, false);
                reticle = reticleObject.transform;
                reticle.localScale = Vector3.one * 0.035f;
                var collider = reticleObject.GetComponent<Collider>();
                DestroyForCurrentMode(collider);

                var renderer = reticleObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = pointerMaterial;
                }

                reticleObject.SetActive(false);
            }
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
                    DestroyForCurrentMode(standalone);
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

        private void RefreshInteractionCanvases()
        {
            interactionCanvases.Clear();
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            var camera = Camera.main;
            for (var index = 0; index < canvases.Length; index += 1)
            {
                var canvas = canvases[index];
                if (canvas.name != "Prototype Canvas" &&
                    canvas.name != QuestTabletTiltController.SwitchCanvasName &&
                    canvas.name != QuestRoomThemeController.SwitchCanvasName)
                {
                    continue;
                }

                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    continue;
                }

                canvas.worldCamera = camera;
                interactionCanvases.Add(canvas);
            }
        }

        private bool NeedsCanvasRefresh()
        {
            // Expect the control panel, the tilt switch and the room theme switch.
            if (interactionCanvases.Count < 3)
            {
                return true;
            }

            for (var index = 0; index < interactionCanvases.Count; index += 1)
            {
                if (interactionCanvases[index] == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureSelectableFeedback(Transform root)
        {
            var colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color32(240, 230, 210, 255),
                pressedColor = new Color32(188, 167, 126, 255),
                selectedColor = new Color32(226, 207, 169, 255),
                disabledColor = new Color32(112, 112, 113, 115),
                colorMultiplier = 1f,
                fadeDuration = 0.05f,
            };

            var selectables = root.GetComponentsInChildren<Selectable>(true);
            var consumerRoot = root.Find("Panel/Consumer UI");
            for (var i = 0; i < selectables.Length; i += 1)
            {
                if (consumerRoot != null && selectables[i].transform.IsChildOf(consumerRoot))
                {
                    continue;
                }

                selectables[i].colors = colors;
            }
        }
    }
}
