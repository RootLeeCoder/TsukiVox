using UnityEngine;
using UnityEngine.SpatialTracking;

#if UNITY_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

namespace TsukiVox.AudioPrototype
{
    public sealed class QuestXrBootstrap : MonoBehaviour
    {
        public static QuestXrBootstrap EnsureSceneBootstrap()
        {
            var existing = FindAnyObjectByType<QuestXrBootstrap>();
            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(existing.gameObject);
                }

                EnsureMainCameraTracking();
                return existing;
            }

            var bootstrapObject = new GameObject("Quest XR Bootstrap");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(bootstrapObject);
            }

            EnsureMainCameraTracking();
            return bootstrapObject.AddComponent<QuestXrBootstrap>();
        }

        public static void EnsureMainCameraTracking()
        {
            var mainCamera = Camera.main;
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

        private void Start()
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

        private void OnDestroy()
        {
        }
    }
}
