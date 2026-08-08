using System.Collections;
using UnityEngine;
using UnityEngine.SpatialTracking;
using UnityEngine.XR;

#if UNITY_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

namespace TsukiVox.AudioPrototype
{
    public sealed class QuestXrBootstrap : MonoBehaviour
    {
        private const float MinimumAlignmentDelaySeconds = 0.5f;
        private const float StablePoseDurationSeconds = 0.25f;
        private const float StableYawToleranceDegrees = 4f;
        private const float StablePositionToleranceMeters = 0.05f;
        private const float AlignmentTimeoutSeconds = 8f;

        private static Transform trackingOrigin;

        private Coroutine alignmentCoroutine;
        private int alignmentRequestVersion;
        private bool hasStarted;

        public static QuestXrBootstrap EnsureSceneBootstrap()
        {
            var existing = FindAnyObjectByType<QuestXrBootstrap>();
            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(existing.gameObject);
                }

                existing.ConfigureMainCameraTracking();
                return existing;
            }

            var bootstrapObject = new GameObject("Quest XR Bootstrap");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(bootstrapObject);
            }

            var bootstrap = bootstrapObject.AddComponent<QuestXrBootstrap>();
            bootstrap.ConfigureMainCameraTracking();
            return bootstrap;
        }

        public static void EnsureMainCameraTracking()
        {
            var bootstrap = FindAnyObjectByType<QuestXrBootstrap>();
            if (bootstrap != null)
            {
                bootstrap.ConfigureMainCameraTracking();
                return;
            }

            ConfigurePoseDriver(Camera.main);
        }

        /// <summary>
        /// Converts a raw OpenXR tracking-space controller pose into the same world
        /// space used by the HMD after startup alignment.
        /// </summary>
        public static void TransformTrackingPose(ref Vector3 position, ref Quaternion rotation)
        {
            var origin = trackingOrigin;
            if (origin == null)
            {
                return;
            }

            position = origin.TransformPoint(position);
            rotation = origin.rotation * rotation;
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            ConfigureMainCameraTracking();
        }

        private void Start()
        {
            LogLoaderState();
            hasStarted = true;
            RequestStartupAlignment();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasStarted)
            {
                return;
            }

            if (hasFocus)
            {
                RequestStartupAlignment();
            }
            else
            {
                CancelPendingAlignment();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (!hasStarted)
            {
                return;
            }

            if (isPaused)
            {
                CancelPendingAlignment();
            }
            else
            {
                RequestStartupAlignment();
            }
        }

        private void OnDestroy()
        {
            CancelPendingAlignment();
            if (trackingOrigin == transform)
            {
                trackingOrigin = null;
            }
        }

        private void ConfigureMainCameraTracking()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[TsukiVox XR] No MainCamera found for HMD tracking.");
                return;
            }

            ConfigurePoseDriver(mainCamera);
            if (!Application.isPlaying)
            {
                return;
            }

            if (trackingOrigin != transform)
            {
                transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                transform.localScale = Vector3.one;
                trackingOrigin = transform;
            }

            if (mainCamera.transform.parent != transform)
            {
                mainCamera.transform.SetParent(transform, true);
            }
        }

        private static void ConfigurePoseDriver(Camera mainCamera)
        {
            if (mainCamera == null)
            {
                Debug.LogWarning("[TsukiVox XR] No MainCamera found for HMD tracking.");
                return;
            }

            var poseDriver = mainCamera.GetComponent<TrackedPoseDriver>();
            if (poseDriver == null)
            {
                poseDriver = mainCamera.gameObject.AddComponent<TrackedPoseDriver>();
            }

            poseDriver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
            poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        }

        private void RequestStartupAlignment()
        {
            CancelPendingAlignment();
            ConfigureMainCameraTracking();
            alignmentCoroutine = StartCoroutine(AlignWhenTrackingIsStable(alignmentRequestVersion));
        }

        private void CancelPendingAlignment()
        {
            alignmentRequestVersion += 1;
            if (alignmentCoroutine == null)
            {
                return;
            }

            StopCoroutine(alignmentCoroutine);
            alignmentCoroutine = null;
        }

        private IEnumerator AlignWhenTrackingIsStable(int requestVersion)
        {
            var startedAt = Time.unscaledTime;
            var candidateStartedAt = 0f;
            var candidatePosition = Vector3.zero;
            var candidateYaw = 0f;
            var lastPosition = Vector3.zero;
            var lastRotation = Quaternion.identity;
            var hasCandidate = false;
            var hasLastPose = false;

            while (requestVersion == alignmentRequestVersion)
            {
                var now = Time.unscaledTime;
                if (TryGetHeadTrackingPose(out var position, out var rotation))
                {
                    lastPosition = position;
                    lastRotation = rotation;
                    hasLastPose = true;

                    if (now - startedAt >= MinimumAlignmentDelaySeconds &&
                        TryGetHorizontalYaw(rotation, out var yaw))
                    {
                        var horizontalPosition = new Vector2(position.x, position.z);
                        var candidateHorizontalPosition = new Vector2(candidatePosition.x, candidatePosition.z);
                        var candidateChanged = !hasCandidate ||
                                               Mathf.Abs(Mathf.DeltaAngle(candidateYaw, yaw)) > StableYawToleranceDegrees ||
                                               Vector2.Distance(candidateHorizontalPosition, horizontalPosition) > StablePositionToleranceMeters;
                        if (candidateChanged)
                        {
                            hasCandidate = true;
                            candidateStartedAt = now;
                            candidatePosition = position;
                            candidateYaw = yaw;
                        }
                        else if (now - candidateStartedAt >= StablePoseDurationSeconds)
                        {
                            ApplyTrackingOriginAlignment(position, rotation, "stable");
                            alignmentCoroutine = null;
                            yield break;
                        }
                    }
                }

                if (now - startedAt >= AlignmentTimeoutSeconds)
                {
                    if (hasLastPose && TryGetHorizontalYaw(lastRotation, out _))
                    {
                        ApplyTrackingOriginAlignment(lastPosition, lastRotation, "timeout fallback");
                    }
                    else
                    {
                        Debug.LogWarning("[TsukiVox XR] Startup alignment timed out before a valid tracked HMD pose was available.");
                    }

                    alignmentCoroutine = null;
                    yield break;
                }

                yield return null;
            }

            alignmentCoroutine = null;
        }

        private void ApplyTrackingOriginAlignment(Vector3 headPosition, Quaternion headRotation, string reason)
        {
            if (trackingOrigin != transform || !TryGetHorizontalYaw(headRotation, out var sourceYaw))
            {
                return;
            }

            var originRotation = Quaternion.Euler(0f, -sourceYaw, 0f);
            var horizontalHeadPosition = new Vector3(headPosition.x, 0f, headPosition.z);
            var originPosition = QuestKtvRoomPrototype.PlayerStartPosition - originRotation * horizontalHeadPosition;
            originPosition.y = QuestKtvRoomPrototype.PlayerStartPosition.y;
            transform.SetPositionAndRotation(originPosition, originRotation);

            Debug.Log(
                $"[TsukiVox XR] Startup alignment applied ({reason}); " +
                $"headYaw={sourceYaw:F1}, correctionYaw={-sourceYaw:F1}, " +
                $"headXZ=({headPosition.x:F2}, {headPosition.z:F2}).");
        }

        private static bool TryGetHeadTrackingPose(out Vector3 position, out Quaternion rotation)
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!TryReadTrackedDevicePose(device, out position, out rotation))
            {
                device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (!TryReadTrackedDevicePose(device, out position, out rotation))
                {
                    position = Vector3.zero;
                    rotation = Quaternion.identity;
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadTrackedDevicePose(InputDevice device, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (!device.isValid)
            {
                return false;
            }

            if (device.TryGetFeatureValue(CommonUsages.isTracked, out var isTracked) && !isTracked)
            {
                return false;
            }

            if (device.TryGetFeatureValue(CommonUsages.trackingState, out var trackingState))
            {
                var required = InputTrackingState.Position | InputTrackingState.Rotation;
                if ((trackingState & required) != required)
                {
                    return false;
                }
            }

            return device.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
                   device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation) &&
                   IsFinite(position) &&
                   IsUsableRotation(rotation);
        }

        private static bool TryGetHorizontalYaw(Quaternion rotation, out float yaw)
        {
            var forward = rotation * Vector3.forward;
            forward.y = 0f;
            if (!IsFinite(forward) || forward.sqrMagnitude < 0.01f)
            {
                yaw = 0f;
                return false;
            }

            forward.Normalize();
            yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            return IsFinite(yaw);
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

        private static void LogLoaderState()
        {
#if UNITY_XR_MANAGEMENT
            var settings = XRGeneralSettings.Instance;
            var manager = settings == null ? null : settings.Manager;
            if (manager == null)
            {
                Debug.LogWarning("[TsukiVox XR] XR General Settings are not configured.");
                return;
            }

            if (manager.activeLoader != null)
            {
                Debug.Log($"[TsukiVox XR] XR loader active: {manager.activeLoader.name}");
                return;
            }

            Debug.LogWarning("[TsukiVox XR] No active XR loader yet. XR Management automatic startup should initialize OpenXR before rendering.");
#else
            Debug.LogWarning("[TsukiVox XR] XR Management package is not available at compile time.");
#endif
        }
    }
}
