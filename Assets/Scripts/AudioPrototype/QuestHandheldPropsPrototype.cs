using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

using InputSystemCommonUsages = UnityEngine.InputSystem.CommonUsages;
using InputSystemDevice = UnityEngine.InputSystem.InputDevice;

namespace TsukiVox.AudioPrototype
{
    /// <summary>
    /// V0.6 handheld props. The right controller shows a virtual microphone restyled
    /// as a realistic handheld dynamic mic (SM58-style) and the left controller shows
    /// a multi-colour glowstick, both rebuilt as native Unity meshes. Glowstick layout,
    /// palette and both props' feedback curves are ported from the WebXR prototype
    /// (src/scene/microphone.ts, src/scene/glowstick.ts, src/xr/controllers.ts,
    /// src/feedback.ts); the implementation is native. Tracking space equals world
    /// space in this project, so props follow the controller grip pose read directly
    /// from the Input System each frame (matching QuestUiPointer).
    /// </summary>
    public sealed class QuestHandheldPropsPrototype : MonoBehaviour
    {
        public const string RootName = "V0.6 Handheld Props";
        public const float MicrophoneGrilleRadius = 0.0375f;
        public const float MinimumWarningClearance = 0.005f;
        public const float MaximumWarningClearance = 0.08f;
        public const float MinimumCriticalClearance = 0f;
        public const float MaximumCriticalClearance = 0.04f;
        public const float MinimumClearanceGap = 0.005f;
        public const float MinimumHapticStrength = 0.2f;
        public const float MaximumHapticStrength = 1f;
        public const float DefaultWarningClearance = 0.03f;
        public const float DefaultCriticalClearance = 0.008f;
        public const float DefaultHapticStrength = 1f;
        public const float DefaultMouthOffsetX = 0f;
        public const float DefaultMouthOffsetY = -0.11f;
        public const float DefaultMouthOffsetZ = 0.02f;
        public const float MinimumMouthOffsetX = -0.05f;
        public const float MaximumMouthOffsetX = 0.05f;
        public const float MinimumMouthOffsetY = -0.18f;
        public const float MaximumMouthOffsetY = -0.05f;
        public const float MinimumMouthOffsetZ = 0f;
        public const float MaximumMouthOffsetZ = 0.08f;

        // WebXR mounted props on the grip at (0, 0.02, +0.055) with geometry extending
        // toward grip -Z. Unity/OpenXR use the same grip pose but flip the Z axis, so
        // the mount Z is negated here and prop geometry is built extending toward +Z.
        private const float GripMountOffsetZ = -0.055f;
        private const float GripMountOffsetY = 0.02f;
        private const float WarningHapticDuration = 0.035f;
        private const float CriticalHapticDuration = 0.075f;
        private const string MicFaceEnabledPrefsKey = "TsukiVox.MicFaceHaptics.Enabled.v1";
        private const string MicFaceWarningClearancePrefsKey = "TsukiVox.MicFaceHaptics.WarningClearance.v2";
        private const string MicFaceCriticalClearancePrefsKey = "TsukiVox.MicFaceHaptics.CriticalClearance.v2";
        private const string MicFaceStrengthPrefsKey = "TsukiVox.MicFaceHaptics.Strength.v1";
        private const string MicFaceMouthOffsetXPrefsKey = "TsukiVox.MicFaceHaptics.MouthOffsetX.v1";
        private const string MicFaceMouthOffsetYPrefsKey = "TsukiVox.MicFaceHaptics.MouthOffsetY.v1";
        private const string MicFaceMouthOffsetZPrefsKey = "TsukiVox.MicFaceHaptics.MouthOffsetZ.v1";

        public readonly struct GlowstickColor
        {
            public readonly string Name;
            public readonly Color Value;

            public GlowstickColor(string name, uint hex)
            {
                Name = name;
                Value = HexColor(hex);
            }
        }

        // Ported 1:1 from GLOWSTICK_COLOR_OPTIONS in src/scene/glowstick.ts.
        public static readonly GlowstickColor[] GlowstickColorOptions =
        {
            new GlowstickColor("Red", 0xff2d35),
            new GlowstickColor("Orange", 0xff8a16),
            new GlowstickColor("Yellow", 0xffe850),
            new GlowstickColor("Green", 0x38f25b),
            new GlowstickColor("Lake Green", 0x19e6a2),
            new GlowstickColor("Cyan", 0x00dce8),
            new GlowstickColor("Ice Blue", 0x89f1ff),
            new GlowstickColor("Blue", 0x3478ff),
            new GlowstickColor("Light Purple", 0xc7a0ff),
            new GlowstickColor("Purple", 0x8b45ff),
            new GlowstickColor("Rose Red", 0xff2d9d),
            new GlowstickColor("Light Pink", 0xffb2d9),
            new GlowstickColor("Pink", 0xff62c7),
            new GlowstickColor("White", 0xf8ffff),
        };

        public const int DefaultGlowstickColorIndex = 5;

        [Header("Scene References")]
        [SerializeField] private QuestAudioPrototype audioPrototype;

        [Header("Grip Offsets (tune on device)")]
        [SerializeField] private Vector3 micLocalPosition = new Vector3(0f, GripMountOffsetY, GripMountOffsetZ);
        [SerializeField] private Vector3 micLocalEuler = Vector3.zero;
        [SerializeField] private Vector3 glowstickLocalPosition = new Vector3(0f, GripMountOffsetY, GripMountOffsetZ);
        // VRSing: glowstick.rotation.set(0.2, 0, 0) tilts forward by ~11.46° about grip X.
        // Unity's Z-flip preserves this forward tilt as a positive X rotation.
        [SerializeField] private Vector3 glowstickLocalEuler = new Vector3(11.46f, 0f, 0f);

        [Header("Microphone Face Proximity Haptics")]
        [SerializeField] private bool micFaceHapticsEnabled = true;
        [SerializeField] private Vector3 mouthLocalOffset = new Vector3(DefaultMouthOffsetX, DefaultMouthOffsetY, DefaultMouthOffsetZ);
        [SerializeField, Range(MicrophoneGrilleRadius + MinimumWarningClearance, MicrophoneGrilleRadius + MaximumWarningClearance)] private float micFaceWarningDistance = 0.0675f;
        [SerializeField, Range(MicrophoneGrilleRadius + MinimumCriticalClearance, MicrophoneGrilleRadius + MaximumCriticalClearance)] private float micFaceCriticalDistance = 0.0455f;
        [SerializeField, Range(0.005f, 0.05f)] private float micFaceReleaseHysteresis = 0.005f;
        [SerializeField, Range(1f, 40f)] private float micFaceDistanceSmoothing = 18f;
        [SerializeField, Range(0.05f, 1f)] private float micFaceWarningAmplitude = 0.16f;
        [SerializeField, Range(0.1f, 1f)] private float micFaceCriticalAmplitude = 0.95f;
        [SerializeField, Range(0.08f, 0.5f)] private float micFaceWarningPulseInterval = 0.32f;
        [SerializeField, Range(0.04f, 0.2f)] private float micFaceCriticalPulseInterval = 0.1f;
        [SerializeField, Range(MinimumHapticStrength, MaximumHapticStrength)] private float micFaceHapticStrength = DefaultHapticStrength;

        [Header("Runtime")]
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private bool driveFeedbackFromMic = true;
        [SerializeField, Range(0.01f, 1f)] private float feedbackSmoothing = 0.22f;
        [SerializeField, Range(0, 13)] private int glowstickColorIndex = DefaultGlowstickColorIndex;

        private MicProp micProp;
        private GlowstickProp glowstickProp;
        private float smoothedLevel;
        private bool wasColorPreviousPressed;
        private bool wasColorNextPressed;
        private bool isRightControllerTracked;
        private bool isMicFaceWarningActive;
        private bool wasMicFaceCritical;
        private bool hasSmoothedMicFaceDistance;
        private float smoothedMicFaceDistance;
        private float nextMicFaceHapticTime;
        private Transform headTransform;
        private bool suppressMicFaceHaptics;
        private Transform mouthPointMarker;
        private bool mouthPointMarkerRequested;

        public float MicrophoneFaceDistance => hasSmoothedMicFaceDistance
            ? smoothedMicFaceDistance
            : float.PositiveInfinity;

        public float MicrophoneFaceSurfaceClearance => hasSmoothedMicFaceDistance
            ? Mathf.Max(0f, smoothedMicFaceDistance - MicrophoneGrilleRadius)
            : float.PositiveInfinity;

        public float MicrophoneFaceProximity { get; private set; }
        public bool IsMicrophoneFaceWarningActive => isMicFaceWarningActive;
        public bool IsMicrophoneFaceCritical => wasMicFaceCritical;
        public bool IsMicrophoneTracked => isRightControllerTracked;
        public bool MicFaceHapticsEnabled => micFaceHapticsEnabled;
        public float MicFaceWarningClearance => Mathf.Max(0f, micFaceWarningDistance - MicrophoneGrilleRadius);
        public float MicFaceCriticalClearance => Mathf.Max(0f, micFaceCriticalDistance - MicrophoneGrilleRadius);
        public float MicFaceHapticStrength => micFaceHapticStrength;
        public Vector3 MicFaceMouthLocalOffset => mouthLocalOffset;
        public bool IsMicFaceMouthMarkerVisible => mouthPointMarker != null && mouthPointMarker.gameObject.activeSelf;

        public static QuestHandheldPropsPrototype EnsureSceneProps()
        {
            var existing = FindAnyObjectByType<QuestHandheldPropsPrototype>();
            if (existing != null)
            {
                if (!Application.isPlaying)
                {
                    existing.ConfigureSceneReferences();
                }

                return existing;
            }

            var propsObject = new GameObject(RootName);
            var props = propsObject.AddComponent<QuestHandheldPropsPrototype>();
            if (!Application.isPlaying)
            {
                props.ConfigureSceneReferences();
            }

            return props;
        }

        private void Awake()
        {
            LoadMicFacePreferences();
            if (buildOnAwake)
            {
                ConfigureSceneReferences();
            }
        }

        private void Update()
        {
            AssignRolesFromControllers();
            UpdateGlowstickColorControls();
            UpdateMicFaceMouthMarker();
            UpdateMicrophoneFaceProximityHaptics();

            if (!driveFeedbackFromMic)
            {
                return;
            }

            if (audioPrototype == null)
            {
                audioPrototype = FindAnyObjectByType<QuestAudioPrototype>();
            }

            var targetLevel = audioPrototype != null
                ? Mathf.Max(audioPrototype.InputLevel, audioPrototype.OutputLevel * 0.72f)
                : 0f;
            smoothedLevel = Mathf.Lerp(smoothedLevel, Mathf.Clamp01(targetLevel), feedbackSmoothing);

            var pulse = Mathf.Sin(Time.unscaledTime * 2.2f) * 0.5f + 0.5f;
            micProp?.ApplyFeedback(smoothedLevel, pulse);
            glowstickProp?.ApplyFeedback(smoothedLevel, pulse);
        }

        public void ConfigureSceneReferences()
        {
            name = RootName;
            audioPrototype = audioPrototype != null ? audioPrototype : FindAnyObjectByType<QuestAudioPrototype>();

            glowstickColorIndex = ((glowstickColorIndex % GlowstickColorOptions.Length) + GlowstickColorOptions.Length) % GlowstickColorOptions.Length;

            BuildProps();
            ApplyGlowstickColor();
        }

        public void SetMicFaceHapticsEnabled(bool enabled)
        {
            micFaceHapticsEnabled = enabled;
            if (!enabled)
            {
                ClearMicrophoneFaceWarningState();
            }

            SaveMicFacePreferences();
        }

        public void SetMicFaceWarningClearance(float clearance)
        {
            var warning = Mathf.Clamp(clearance, MinimumWarningClearance, MaximumWarningClearance);
            var critical = Mathf.Min(MicFaceCriticalClearance, warning - MinimumClearanceGap);
            micFaceWarningDistance = warning + MicrophoneGrilleRadius;
            micFaceCriticalDistance = Mathf.Max(MinimumCriticalClearance, critical) + MicrophoneGrilleRadius;
            SaveMicFacePreferences();
        }

        public void SetMicFaceCriticalClearance(float clearance)
        {
            var critical = Mathf.Clamp(clearance, MinimumCriticalClearance, MaximumCriticalClearance);
            var warning = Mathf.Max(MicFaceWarningClearance, critical + MinimumClearanceGap);
            warning = Mathf.Min(warning, MaximumWarningClearance);
            critical = Mathf.Min(critical, warning - MinimumClearanceGap);
            micFaceWarningDistance = warning + MicrophoneGrilleRadius;
            micFaceCriticalDistance = Mathf.Max(MinimumCriticalClearance, critical) + MicrophoneGrilleRadius;
            SaveMicFacePreferences();
        }

        public void SetMicFaceHapticStrength(float strength)
        {
            micFaceHapticStrength = Mathf.Clamp(strength, MinimumHapticStrength, MaximumHapticStrength);
            SaveMicFacePreferences();
        }

        public void SetMicFaceMouthLocalOffset(Vector3 localOffset)
        {
            mouthLocalOffset = ClampMouthLocalOffset(localOffset);
            SaveMicFacePreferences();
        }

        public void SetMicFaceMouthMarkerVisible(bool visible)
        {
            mouthPointMarkerRequested = visible;
            if (!visible && mouthPointMarker != null)
            {
                mouthPointMarker.gameObject.SetActive(false);
            }
        }

        public void ResetMicFaceMouthLocalOffset()
        {
            PlayerPrefs.DeleteKey(MicFaceMouthOffsetXPrefsKey);
            PlayerPrefs.DeleteKey(MicFaceMouthOffsetYPrefsKey);
            PlayerPrefs.DeleteKey(MicFaceMouthOffsetZPrefsKey);
            mouthLocalOffset = DefaultMouthLocalOffset();
            SaveMicFacePreferences();
        }

        public void SetMicFaceHapticsSuppressed(bool suppressed)
        {
            suppressMicFaceHaptics = suppressed;
            if (suppressed)
            {
                ClearMicrophoneFaceWarningState();
            }
        }

        public void ResetMicFaceHapticPreferences()
        {
            PlayerPrefs.DeleteKey(MicFaceEnabledPrefsKey);
            PlayerPrefs.DeleteKey(MicFaceWarningClearancePrefsKey);
            PlayerPrefs.DeleteKey(MicFaceCriticalClearancePrefsKey);
            PlayerPrefs.DeleteKey(MicFaceStrengthPrefsKey);
            PlayerPrefs.DeleteKey(MicFaceMouthOffsetXPrefsKey);
            PlayerPrefs.DeleteKey(MicFaceMouthOffsetYPrefsKey);
            PlayerPrefs.DeleteKey(MicFaceMouthOffsetZPrefsKey);
            PlayerPrefs.Save();

            micFaceHapticsEnabled = true;
            micFaceWarningDistance = DefaultWarningClearance + MicrophoneGrilleRadius;
            micFaceCriticalDistance = DefaultCriticalClearance + MicrophoneGrilleRadius;
            micFaceHapticStrength = DefaultHapticStrength;
            mouthLocalOffset = DefaultMouthLocalOffset();
            micFaceReleaseHysteresis = 0.005f;
            ClearMicrophoneFaceWarningState();
        }

        private void LoadMicFacePreferences()
        {
            micFaceHapticsEnabled = PlayerPrefs.GetInt(MicFaceEnabledPrefsKey, micFaceHapticsEnabled ? 1 : 0) != 0;
            var warning = PlayerPrefs.GetFloat(MicFaceWarningClearancePrefsKey, DefaultWarningClearance);
            var critical = PlayerPrefs.GetFloat(MicFaceCriticalClearancePrefsKey, DefaultCriticalClearance);
            warning = Mathf.Clamp(warning, MinimumWarningClearance, MaximumWarningClearance);
            critical = Mathf.Clamp(critical, MinimumCriticalClearance, Mathf.Min(MaximumCriticalClearance, warning - MinimumClearanceGap));
            micFaceWarningDistance = warning + MicrophoneGrilleRadius;
            micFaceCriticalDistance = critical + MicrophoneGrilleRadius;
            micFaceHapticStrength = Mathf.Clamp(
                PlayerPrefs.GetFloat(MicFaceStrengthPrefsKey, micFaceHapticStrength),
                MinimumHapticStrength,
                MaximumHapticStrength);
            mouthLocalOffset = ClampMouthLocalOffset(new Vector3(
                PlayerPrefs.GetFloat(MicFaceMouthOffsetXPrefsKey, DefaultMouthOffsetX),
                PlayerPrefs.GetFloat(MicFaceMouthOffsetYPrefsKey, DefaultMouthOffsetY),
                PlayerPrefs.GetFloat(MicFaceMouthOffsetZPrefsKey, DefaultMouthOffsetZ)));
        }

        private void SaveMicFacePreferences()
        {
            PlayerPrefs.SetInt(MicFaceEnabledPrefsKey, micFaceHapticsEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(MicFaceWarningClearancePrefsKey, MicFaceWarningClearance);
            PlayerPrefs.SetFloat(MicFaceCriticalClearancePrefsKey, MicFaceCriticalClearance);
            PlayerPrefs.SetFloat(MicFaceStrengthPrefsKey, micFaceHapticStrength);
            PlayerPrefs.SetFloat(MicFaceMouthOffsetXPrefsKey, mouthLocalOffset.x);
            PlayerPrefs.SetFloat(MicFaceMouthOffsetYPrefsKey, mouthLocalOffset.y);
            PlayerPrefs.SetFloat(MicFaceMouthOffsetZPrefsKey, mouthLocalOffset.z);
            PlayerPrefs.Save();
        }

        private void BuildProps()
        {
            for (var index = transform.childCount - 1; index >= 0; index -= 1)
            {
                DestroySceneObject(transform.GetChild(index).gameObject);
            }

            micProp = MicProp.Build(transform, micLocalPosition, Quaternion.Euler(micLocalEuler));
            glowstickProp = GlowstickProp.Build(transform, glowstickLocalPosition, Quaternion.Euler(glowstickLocalEuler));
            mouthPointMarker = BuildMouthPointMarker(transform);

            micProp.SetVisible(false);
            glowstickProp.SetVisible(false);
            mouthPointMarker.gameObject.SetActive(false);
        }

        private static Transform BuildMouthPointMarker(Transform parent)
        {
            var markerRoot = new GameObject("mic mouth point marker").transform;
            markerRoot.SetParent(parent, false);

            var markerColor = new Color(0.15f, 1f, 0.72f, 1f);
            var coreMaterial = PropMaterials.Emissive("mic mouth marker core", markerColor, 3.5f, 0.05f);
            var guideMaterial = PropMaterials.AdditiveShell("mic mouth marker guide", markerColor, 0.38f);
            MeshFactory.CreateSphere(markerRoot, "marker core", 0.005f, coreMaterial);

            var frontRing = MeshFactory.CreateTorus(markerRoot, "marker front ring", 0.014f, 0.0014f, 8, 32, guideMaterial);
            frontRing.transform.localRotation = Quaternion.identity;
            var horizontalRing = MeshFactory.CreateTorus(markerRoot, "marker horizontal ring", 0.014f, 0.0012f, 8, 32, guideMaterial);
            horizontalRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var verticalRing = MeshFactory.CreateTorus(markerRoot, "marker vertical ring", 0.014f, 0.0012f, 8, 32, guideMaterial);
            verticalRing.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            var guide = MeshFactory.CreateTaperedCylinder(markerRoot, "marker forward guide", 0.0012f, 0.0012f, 0.07f, 10, guideMaterial);
            guide.transform.localPosition = new Vector3(0f, 0f, 0.035f);
            return markerRoot;
        }

        private void UpdateMicFaceMouthMarker()
        {
            if (mouthPointMarker == null)
            {
                return;
            }

            if (!mouthPointMarkerRequested)
            {
                mouthPointMarker.gameObject.SetActive(false);
                return;
            }

            if (headTransform == null)
            {
                var mainCamera = Camera.main;
                headTransform = mainCamera != null ? mainCamera.transform : null;
            }

            if (headTransform == null)
            {
                mouthPointMarker.gameObject.SetActive(false);
                return;
            }

            mouthPointMarker.gameObject.SetActive(true);
            mouthPointMarker.SetPositionAndRotation(headTransform.TransformPoint(mouthLocalOffset), headTransform.rotation);
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.06f;
            mouthPointMarker.localScale = Vector3.one * pulse;
        }

        private static Vector3 DefaultMouthLocalOffset()
        {
            return new Vector3(DefaultMouthOffsetX, DefaultMouthOffsetY, DefaultMouthOffsetZ);
        }

        private static Vector3 ClampMouthLocalOffset(Vector3 localOffset)
        {
            return new Vector3(
                Mathf.Clamp(localOffset.x, MinimumMouthOffsetX, MaximumMouthOffsetX),
                Mathf.Clamp(localOffset.y, MinimumMouthOffsetY, MaximumMouthOffsetY),
                Mathf.Clamp(localOffset.z, MinimumMouthOffsetZ, MaximumMouthOffsetZ));
        }

        private void AssignRolesFromControllers()
        {
            if (micProp == null || glowstickProp == null)
            {
                isRightControllerTracked = false;
                return;
            }

            // Right hand is the microphone, left hand is the glowstick (WebXR
            // refreshRoles). A missing hand simply hides its prop.
            if (TryGetGripPose(rightHand: true, out var rightPosition, out var rightRotation))
            {
                micProp.PlaceAtGrip(rightPosition, rightRotation);
                micProp.SetVisible(true);
                isRightControllerTracked = true;
            }
            else
            {
                micProp.SetVisible(false);
                isRightControllerTracked = false;
            }

            if (TryGetGripPose(rightHand: false, out var leftPosition, out var leftRotation))
            {
                glowstickProp.PlaceAtGrip(leftPosition, leftRotation);
                glowstickProp.SetVisible(true);
            }
            else
            {
                glowstickProp.SetVisible(false);
            }
        }

        private void UpdateMicrophoneFaceProximityHaptics()
        {
            if (!isRightControllerTracked || micProp == null)
            {
                ResetMicrophoneFaceProximity();
                return;
            }

            if (headTransform == null)
            {
                var mainCamera = Camera.main;
                headTransform = mainCamera != null ? mainCamera.transform : null;
            }

            if (headTransform == null)
            {
                ResetMicrophoneFaceProximity();
                return;
            }

            var grillePosition = micProp.GrilleWorldPosition;
            var grilleHeadLocalPosition = headTransform.InverseTransformPoint(grillePosition);

            // Do not warn for a microphone held behind the head. The mouth proxy is
            // intentionally local to the HMD so it follows both position and rotation.
            if (grilleHeadLocalPosition.z < -0.02f)
            {
                ResetMicrophoneFaceProximity();
                return;
            }

            var mouthPosition = headTransform.TransformPoint(mouthLocalOffset);
            var rawDistance = Vector3.Distance(grillePosition, mouthPosition);
            if (!hasSmoothedMicFaceDistance)
            {
                smoothedMicFaceDistance = rawDistance;
                hasSmoothedMicFaceDistance = true;
            }
            else
            {
                var smoothing = 1f - Mathf.Exp(-micFaceDistanceSmoothing * Time.unscaledDeltaTime);
                smoothedMicFaceDistance = Mathf.Lerp(smoothedMicFaceDistance, rawDistance, smoothing);
            }

            if (!micFaceHapticsEnabled || suppressMicFaceHaptics)
            {
                ClearMicrophoneFaceWarningState();
                return;
            }

            var warningDistance = Mathf.Max(micFaceWarningDistance, micFaceCriticalDistance + MinimumClearanceGap);
            var criticalDistance = Mathf.Min(micFaceCriticalDistance, warningDistance - MinimumClearanceGap);
            if (isMicFaceWarningActive)
            {
                if (smoothedMicFaceDistance > warningDistance + micFaceReleaseHysteresis)
                {
                    isMicFaceWarningActive = false;
                }
            }
            else if (smoothedMicFaceDistance <= warningDistance)
            {
                isMicFaceWarningActive = true;
                nextMicFaceHapticTime = Time.unscaledTime;
            }

            if (!isMicFaceWarningActive)
            {
                MicrophoneFaceProximity = 0f;
                wasMicFaceCritical = false;
                return;
            }

            var linearProximity = Mathf.InverseLerp(warningDistance, criticalDistance, smoothedMicFaceDistance);
            MicrophoneFaceProximity = linearProximity * linearProximity * (3f - 2f * linearProximity);
            var criticalReleaseDistance = criticalDistance + Mathf.Min(micFaceReleaseHysteresis * 0.5f, 0.01f);
            var isCritical = smoothedMicFaceDistance <= (wasMicFaceCritical ? criticalReleaseDistance : criticalDistance);

            if (isCritical && !wasMicFaceCritical)
            {
                SendRightControllerHaptic(micFaceCriticalAmplitude * micFaceHapticStrength, CriticalHapticDuration);
                nextMicFaceHapticTime = Time.unscaledTime + micFaceCriticalPulseInterval;
            }
            else if (Time.unscaledTime >= nextMicFaceHapticTime)
            {
                var amplitude = Mathf.Lerp(micFaceWarningAmplitude, micFaceCriticalAmplitude, MicrophoneFaceProximity);
                var duration = Mathf.Lerp(WarningHapticDuration, CriticalHapticDuration, MicrophoneFaceProximity);
                SendRightControllerHaptic(amplitude * micFaceHapticStrength, duration);

                var interval = Mathf.Lerp(micFaceWarningPulseInterval, micFaceCriticalPulseInterval, MicrophoneFaceProximity);
                nextMicFaceHapticTime = Time.unscaledTime + interval;
            }

            wasMicFaceCritical = isCritical;
        }

        private void ResetMicrophoneFaceProximity()
        {
            ClearMicrophoneFaceWarningState();
            hasSmoothedMicFaceDistance = false;
        }

        private void ClearMicrophoneFaceWarningState()
        {
            isMicFaceWarningActive = false;
            wasMicFaceCritical = false;
            MicrophoneFaceProximity = 0f;
            nextMicFaceHapticTime = 0f;
        }

        private static void SendRightControllerHaptic(float amplitude, float duration)
        {
            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            if (device.isValid)
            {
                device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Max(0f, duration));
            }
        }

        private void OnDisable()
        {
            SetMicFaceMouthMarkerVisible(false);
            ResetMicrophoneFaceProximity();
        }

        private void UpdateGlowstickColorControls()
        {
            // Left controller X (primaryButton) / Y (secondaryButton) cycle the colour,
            // matching the WebXR left-hand mapping in src/xr/controllers.ts.
            var device = GetControllerDevice(rightHand: false);
            if (device == null)
            {
                wasColorPreviousPressed = false;
                wasColorNextPressed = false;
                return;
            }

            var isPreviousPressed = IsButtonPressed(device, "primaryButton");
            var isNextPressed = IsButtonPressed(device, "secondaryButton");

            if (isPreviousPressed && !wasColorPreviousPressed)
            {
                CycleGlowstickColor(-1);
            }

            if (isNextPressed && !wasColorNextPressed)
            {
                CycleGlowstickColor(1);
            }

            wasColorPreviousPressed = isPreviousPressed;
            wasColorNextPressed = isNextPressed;
        }

        private void CycleGlowstickColor(int direction)
        {
            glowstickColorIndex = ((glowstickColorIndex + direction) % GlowstickColorOptions.Length + GlowstickColorOptions.Length) % GlowstickColorOptions.Length;
            ApplyGlowstickColor();
        }

        private void ApplyGlowstickColor()
        {
            glowstickProp?.SetColor(GlowstickColorOptions[glowstickColorIndex].Value);
        }

        // --- Controller pose / input plumbing (same approach as QuestUiPointer) ---

        private static bool TryGetGripPose(bool rightHand, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            var device = GetControllerDevice(rightHand);
            if (device == null || !IsControllerTracked(device))
            {
                return false;
            }

            // Grip pose lives on devicePosition/deviceRotation for XR controllers.
            var positionControl = device.TryGetChildControl<Vector3Control>("devicePosition");
            var rotationControl = device.TryGetChildControl<QuaternionControl>("deviceRotation");
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

        private static InputSystemDevice GetControllerDevice(bool rightHand)
        {
            var controller = rightHand ? XRController.rightHand : XRController.leftHand;
            if (controller != null && IsControllerTracked(controller))
            {
                return controller;
            }

            var devices = InputSystem.devices;
            for (var index = 0; index < devices.Count; index += 1)
            {
                var candidate = devices[index];
                if (candidate is XRController && HasHandUsage(candidate, rightHand) && IsControllerTracked(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool HasHandUsage(InputSystemDevice device, bool rightHand)
        {
            var targetUsage = rightHand ? InputSystemCommonUsages.RightHand : InputSystemCommonUsages.LeftHand;
            var usages = device.usages;
            for (var index = 0; index < usages.Count; index += 1)
            {
                if (usages[index] == targetUsage)
                {
                    return true;
                }
            }

            return false;
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

        private static bool IsButtonPressed(InputSystemDevice device, string controlName)
        {
            var button = device.TryGetChildControl<ButtonControl>(controlName);
            return button != null && button.isPressed;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsUsableRotation(Quaternion rotation)
        {
            return IsFinite(rotation.x) &&
                   IsFinite(rotation.y) &&
                   IsFinite(rotation.z) &&
                   IsFinite(rotation.w) &&
                   rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w > 0.0001f;
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

        private static Color HexColor(uint hex)
        {
            var r = ((hex >> 16) & 0xff) / 255f;
            var g = ((hex >> 8) & 0xff) / 255f;
            var b = (hex & 0xff) / 255f;
            return new Color(r, g, b, 1f);
        }

        private static void DestroySceneObject(GameObject target)
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

        /// <summary>
        /// Virtual microphone restyled as a realistic handheld dynamic mic (SM58-style):
        /// charcoal body with a steel base cap, grip rings, an on/off switch, a flared
        /// shoulder, a dark steel collar and a wire-mesh grille ball. The slim status
        /// ring under the grille keeps the V0.6 level feedback from src/feedback.ts
        /// (ring scale + emissive, mic glow light). The grille centre and radius keep
        /// the V0.6 reference values so face-proximity haptics stay calibrated.
        /// </summary>
        private sealed class MicProp
        {
            // Grille ball centre on the mic axis; haptics measure the mouth distance
            // against this point minus MicrophoneGrilleRadius, so do not move it.
            private const float GrilleCenterZ = 0.145f;

            private static readonly Color RingColor = new Color(0.2f, 0.9f, 0.76f, 1f);

            private readonly Transform anchor;
            private readonly Transform grille;
            private readonly Transform ring;
            private readonly Material ringMaterial;
            private readonly Light glow;
            private readonly GameObject visualRoot;
            // Grip-local mount offset kept separate: once PlaceAtGrip writes a world pose
            // the anchor's localPosition no longer equals the mount offset.
            private readonly Vector3 mountPosition;
            private readonly Quaternion mountRotation;

            private MicProp(Transform anchor, Transform grille, Transform ring, Material ringMaterial, Light glow, GameObject visualRoot, Vector3 mountPosition, Quaternion mountRotation)
            {
                this.anchor = anchor;
                this.grille = grille;
                this.ring = ring;
                this.ringMaterial = ringMaterial;
                this.glow = glow;
                this.visualRoot = visualRoot;
                this.mountPosition = mountPosition;
                this.mountRotation = mountRotation;
            }

            public static MicProp Build(Transform parent, Vector3 localPosition, Quaternion localRotation)
            {
                var anchor = new GameObject("virtual microphone").transform;
                anchor.SetParent(parent, false);
                anchor.localPosition = localPosition;
                anchor.localRotation = localRotation;

                // Painted-metal charcoal body, dark steel hardware, a two-tone steel
                // grille and the emissive status ring. Few shared materials keep the
                // prop cheap on Quest.
                var bodyMaterial = PropMaterials.Standard("V0.6 Mic Body", new Color(0.075f, 0.08f, 0.085f, 1f), 0.55f, 0.55f);
                var steelMaterial = PropMaterials.Standard("V0.6 Mic Dark Steel", new Color(0.26f, 0.28f, 0.3f, 1f), 0.38f, 0.85f);
                var grilleMaterial = PropMaterials.Standard("V0.6 Mic Grille", new Color(0.3f, 0.32f, 0.34f, 1f), 0.45f, 0.9f);
                var grilleWireMaterial = PropMaterials.Standard("V0.6 Mic Grille Wire", new Color(0.52f, 0.55f, 0.58f, 1f), 0.3f, 0.95f);
                var ringMaterial = PropMaterials.Emissive("V0.6 Mic Ring", RingColor, 0.8f, 0.1f);

                // Base cap at the pinky end, slightly wider than the grip and narrowing
                // toward the tail like the battery cap of a real handheld mic.
                var baseCap = MeshFactory.CreateTaperedCylinder(anchor, "mic base cap", 0.017f, 0.0185f, 0.016f, 24, steelMaterial);
                baseCap.transform.localPosition = new Vector3(0f, 0f, -0.04f);

                // Grip: gently tapered charcoal tube.
                var handle = MeshFactory.CreateTaperedCylinder(anchor, "mic handle", 0.0165f, 0.0195f, 0.104f, 24, bodyMaterial);
                handle.transform.localPosition = new Vector3(0f, 0f, 0.02f);

                // Thin grip rings pressed into the handle (each hugs the local taper).
                BuildDetailRing(anchor, "mic grip ring", 0.005f, 0.0179f, 0.0011f, 10, steelMaterial);
                BuildDetailRing(anchor, "mic grip ring", 0.022f, 0.0184f, 0.0011f, 10, steelMaterial);
                BuildDetailRing(anchor, "mic grip ring", 0.039f, 0.0188f, 0.0011f, 10, steelMaterial);

                // On/off switch plate and slider on the thumb side (+Y).
                var switchPlate = MeshFactory.CreateBox(anchor, "mic switch plate", new Vector3(0.01f, 0.003f, 0.02f), bodyMaterial);
                switchPlate.transform.localPosition = new Vector3(0f, 0.018f, 0.048f);
                var switchSlider = MeshFactory.CreateBox(anchor, "mic switch slider", new Vector3(0.0055f, 0.003f, 0.0085f), steelMaterial);
                switchSlider.transform.localPosition = new Vector3(0f, 0.0195f, 0.05f);

                // Shoulder flares out toward the grille like a real mic body.
                var shoulder = MeshFactory.CreateTaperedCylinder(anchor, "mic shoulder", 0.0195f, 0.0245f, 0.036f, 24, bodyMaterial);
                shoulder.transform.localPosition = new Vector3(0f, 0f, 0.09f);

                // Slim status ring between body and grille (real wireless mics have an
                // LED band here); still pulses with the mic level via ApplyFeedback.
                var ring = MeshFactory.CreateTorus(anchor, "mic ring", 0.0248f, 0.0026f, 12, 36, ringMaterial);
                ring.transform.localPosition = new Vector3(0f, 0f, 0.11f);

                // Dark steel collar the grille screws onto; the ball nests into it.
                var collar = MeshFactory.CreateTaperedCylinder(anchor, "mic grille collar", 0.0245f, 0.021f, 0.014f, 24, steelMaterial);
                collar.transform.localPosition = new Vector3(0f, 0f, 0.115f);

                // Wire-mesh grille ball. A full sphere now (no cartoon squash); radius
                // stays MicrophoneGrilleRadius so the haptic surface reference is exact
                // on every axis. Latitude rings stand ~0.5 mm proud to suggest the
                // woven mesh, plus a slightly thicker seam where the two halves meet.
                var grille = MeshFactory.CreateSphere(anchor, "mic grille", MicrophoneGrilleRadius, grilleMaterial);
                grille.transform.localPosition = new Vector3(0f, 0f, GrilleCenterZ);
                BuildDetailRing(anchor, "mic grille wire", GrilleCenterZ - 0.024f, 0.0289f, 0.0005f, 8, grilleWireMaterial);
                BuildDetailRing(anchor, "mic grille wire", GrilleCenterZ - 0.012f, 0.0356f, 0.0005f, 8, grilleWireMaterial);
                BuildDetailRing(anchor, "mic grille seam", GrilleCenterZ, MicrophoneGrilleRadius + 0.0001f, 0.0007f, 8, grilleWireMaterial);
                BuildDetailRing(anchor, "mic grille wire", GrilleCenterZ + 0.012f, 0.0356f, 0.0005f, 8, grilleWireMaterial);
                BuildDetailRing(anchor, "mic grille wire", GrilleCenterZ + 0.024f, 0.0289f, 0.0005f, 8, grilleWireMaterial);

                var glowObject = new GameObject("mic glow");
                glowObject.transform.SetParent(anchor, false);
                glowObject.transform.localPosition = new Vector3(0f, 0f, 0.11f);
                var glow = glowObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = RingColor;
                glow.range = 0.6f;
                glow.intensity = 0.58f;
                glow.shadows = LightShadows.None;

                return new MicProp(anchor, grille.transform, ring.transform, ringMaterial, glow, anchor.gameObject, localPosition, localRotation);
            }

            // Thin torus band encircling the mic axis at the given local Z. Used for
            // grip grooves and grille wires; the torus mesh already encircles local Z.
            private static void BuildDetailRing(Transform anchor, string objectName, float localZ, float ringRadius, float tubeRadius, int tubeSegments, Material material)
            {
                var detailRing = MeshFactory.CreateTorus(anchor, objectName, ringRadius, tubeRadius, tubeSegments, 36, material);
                detailRing.transform.localPosition = new Vector3(0f, 0f, localZ);
            }

            public Vector3 GrilleWorldPosition => grille != null ? grille.position : anchor.position;

            public void PlaceAtGrip(Vector3 gripPosition, Quaternion gripRotation)
            {
                anchor.SetPositionAndRotation(
                    gripPosition + gripRotation * mountPosition,
                    gripRotation * mountRotation);
            }

            public void SetVisible(bool visible)
            {
                if (visualRoot != null && visualRoot.activeSelf != visible)
                {
                    visualRoot.SetActive(visible);
                }
            }

            public void ApplyFeedback(float level, float pulse)
            {
                // src/feedback.ts: ring.scale = 1 + level*0.42 + pulse*0.03,
                // emissiveIntensity = 0.8 + level*3, micGlow = 0.58 + level*1.9.
                if (ring != null)
                {
                    ring.localScale = Vector3.one * (1f + level * 0.42f + pulse * 0.03f);
                    PropMaterials.SetEmission(ringMaterial, RingColor, 0.8f + level * 3f);
                }

                if (glow != null)
                {
                    glow.intensity = 0.58f + level * 1.9f;
                }
            }
        }

        /// <summary>
        /// Glowstick: black handle/pommel/collar, an additive transparent outer shell,
        /// an emissive inner core and a point light. Geometry and colour handling ported
        /// from src/scene/glowstick.ts; feedback from updateGlowstickFeedback.
        /// </summary>
        private sealed class GlowstickProp
        {
            private const float TotalLength = 0.25f;
            private const float HandleLength = 0.078f;
            private const float TubeLength = TotalLength - HandleLength;
            private const float Radius = 0.02f;
            private const float HandleRadius = 0.0215f;
            private const float CoreRadius = Radius * 0.58f;

            private readonly Transform anchor;
            private readonly GameObject visualRoot;
            private readonly Material outerMaterial;
            private readonly Material coreMaterial;
            private readonly Light glow;
            private readonly Vector3 mountPosition;
            private readonly Quaternion mountRotation;
            private float coreEmissionScale = 2.0f;

            private GlowstickProp(Transform anchor, GameObject visualRoot, Material outerMaterial, Material coreMaterial, Light glow, Vector3 mountPosition, Quaternion mountRotation)
            {
                this.anchor = anchor;
                this.visualRoot = visualRoot;
                this.outerMaterial = outerMaterial;
                this.coreMaterial = coreMaterial;
                this.glow = glow;
                this.mountPosition = mountPosition;
                this.mountRotation = mountRotation;
            }

            public static GlowstickProp Build(Transform parent, Vector3 localPosition, Quaternion localRotation)
            {
                var anchor = new GameObject("glowstick").transform;
                anchor.SetParent(parent, false);
                anchor.localPosition = localPosition;
                anchor.localRotation = localRotation;

                var startColor = GlowstickColorOptions[DefaultGlowstickColorIndex].Value;
                var handleMaterial = PropMaterials.Standard("V0.6 Glowstick Handle", new Color(0.012f, 0.016f, 0.02f, 1f), 0.44f, 0.16f);
                var outerMaterial = PropMaterials.AdditiveShell("V0.6 Glowstick Shell", startColor, 0.38f);
                var coreMaterial = PropMaterials.Emissive("V0.6 Glowstick Core", startColor, 2.0f, 0f);

                // Handle (tapered), pommel and collar sit near the grip; the glowing
                // tube extends toward +Z (WebXR -Z after the handedness flip). Handle is
                // 0.88r at the grip end (matching the pommel) and full r toward the tube.
                var handle = MeshFactory.CreateTaperedCylinder(anchor, "glowstick handle", HandleRadius * 0.88f, HandleRadius, HandleLength, 28, handleMaterial);
                handle.transform.localPosition = new Vector3(0f, 0f, HandleLength * 0.5f);

                var pommel = MeshFactory.CreateSphere(anchor, "glowstick pommel", HandleRadius * 0.88f, handleMaterial);
                pommel.transform.localScale = new Vector3(HandleRadius * 0.88f * 2f, HandleRadius * 0.88f * 2f * 0.72f, HandleRadius * 0.88f * 2f);
                pommel.transform.localPosition = Vector3.zero;

                // Collar flares from R*1.08 on the grip side down to HandleRadius*0.9 toward the tube.
                var collar = MeshFactory.CreateTaperedCylinder(anchor, "glowstick collar", Radius * 1.08f, HandleRadius * 0.9f, 0.02f, 28, handleMaterial);
                collar.transform.localPosition = new Vector3(0f, 0f, HandleLength + 0.01f);

                var tubeCenterZ = HandleLength + TubeLength * 0.5f;

                // The additive shell has ZWrite off, so it relies on draw order. The world-space
                // control panel Canvas uses sortingOrder 10 and would otherwise paint over the
                // glow when the stick is in front of the panel; keep the shell above it.
                var outerTube = MeshFactory.CreateTaperedCylinder(anchor, "glowstick outer tube", Radius, Radius, TubeLength, 36, outerMaterial);
                outerTube.transform.localPosition = new Vector3(0f, 0f, tubeCenterZ);
                SetShellSortingOrder(outerTube);

                var outerTip = MeshFactory.CreateSphere(anchor, "glowstick outer tip", Radius, outerMaterial);
                outerTip.transform.localScale = new Vector3(Radius * 2f, Radius * 2f, Radius * 2f * 0.92f);
                outerTip.transform.localPosition = new Vector3(0f, 0f, TotalLength);
                SetShellSortingOrder(outerTip);

                var coreTube = MeshFactory.CreateTaperedCylinder(anchor, "glowstick core tube", CoreRadius, CoreRadius, TubeLength, 28, coreMaterial);
                coreTube.transform.localPosition = new Vector3(0f, 0f, tubeCenterZ);

                var coreTip = MeshFactory.CreateSphere(anchor, "glowstick core tip", CoreRadius, coreMaterial);
                coreTip.transform.localScale = new Vector3(CoreRadius * 2f, CoreRadius * 2f, CoreRadius * 2f * 0.92f);
                coreTip.transform.localPosition = new Vector3(0f, 0f, TotalLength);

                var glowObject = new GameObject("glowstick light");
                glowObject.transform.SetParent(anchor, false);
                glowObject.transform.localPosition = new Vector3(0f, 0f, tubeCenterZ);
                var glow = glowObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = startColor;
                glow.range = 0.82f;
                glow.intensity = 1.8f;
                glow.shadows = LightShadows.None;

                return new GlowstickProp(anchor, anchor.gameObject, outerMaterial, coreMaterial, glow, localPosition, localRotation);
            }

            // Draw the additive shell after the world-space control panel Canvas (sortingOrder 10)
            // so the depthless glow is not overpainted when the stick is held in front of the panel.
            private const int ShellSortingOrder = 20;

            private static void SetShellSortingOrder(GameObject shell)
            {
                if (shell != null && shell.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    renderer.sortingOrder = ShellSortingOrder;
                }
            }

            public void PlaceAtGrip(Vector3 gripPosition, Quaternion gripRotation)
            {
                anchor.SetPositionAndRotation(
                    gripPosition + gripRotation * mountPosition,
                    gripRotation * mountRotation);
            }

            public void SetVisible(bool visible)
            {
                if (visualRoot != null && visualRoot.activeSelf != visible)
                {
                    visualRoot.SetActive(visible);
                }
            }

            public void SetColor(Color color)
            {
                outerMaterial.color = color;
                coreMaterial.color = color;
                PropMaterials.SetEmission(coreMaterial, color, coreEmissionScale);
                if (glow != null)
                {
                    glow.color = color;
                }
            }

            public void ApplyFeedback(float level, float pulse)
            {
                // updateGlowstickFeedback: core emissive 2.0 + level*2 + pulse*level*0.3
                // (tip +0.2), outer opacity 0.38 + level*0.22, light 1.8 + level*2.8.
                coreEmissionScale = 2.0f + level * 2.0f + pulse * level * 0.3f;
                PropMaterials.SetEmission(coreMaterial, coreMaterial.color, coreEmissionScale);

                PropMaterials.SetAlpha(outerMaterial, 0.38f + level * 0.22f);

                if (glow != null)
                {
                    glow.intensity = 1.8f + level * 2.8f + pulse * level * 0.4f;
                }
            }
        }

        /// <summary>Shared material factory for the handheld props (built-in Standard pipeline).</summary>
        private static class PropMaterials
        {
            public static Material Standard(string materialName, Color color, float roughness, float metallic)
            {
                var material = new Material(Shader.Find("Standard"))
                {
                    name = materialName,
                    color = color,
                };
                ApplySurface(material, roughness, metallic);
                return material;
            }

            public static Material Emissive(string materialName, Color color, float emissionIntensity, float metallic)
            {
                var material = Standard(materialName, color, 0.28f, metallic);
                material.name = materialName;
                SetEmission(material, color, emissionIntensity);
                return material;
            }

            public static Material AdditiveShell(string materialName, Color color, float alpha)
            {
                var material = new Material(Shader.Find("Standard"))
                {
                    name = materialName,
                    color = color,
                };

                // Transparent, additive-flavoured emissive shell that never writes depth.
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                ApplySurface(material, 0.35f, 0f);
                SetEmission(material, color, 1.0f);
                SetAlpha(material, alpha);
                return material;
            }

            public static void SetEmission(Material material, Color color, float intensity)
            {
                if (material == null)
                {
                    return;
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
                }

                material.EnableKeyword("_EMISSION");
            }

            public static void SetAlpha(Material material, float alpha)
            {
                if (material == null)
                {
                    return;
                }

                var color = material.color;
                color.a = Mathf.Clamp01(alpha);
                material.color = color;
            }

            private static void ApplySurface(Material material, float roughness, float metallic)
            {
                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", metallic);
                }

                if (material.HasProperty("_Glossiness"))
                {
                    material.SetFloat("_Glossiness", Mathf.Clamp01(1f - roughness));
                }
            }
        }

        /// <summary>
        /// Builds the small prop meshes. Cubes/spheres reuse Unity primitives; the
        /// tapered cylinder and torus are generated so the props keep their intended
        /// silhouette. All meshes drop colliders and shadows for Quest perf.
        /// </summary>
        private static class MeshFactory
        {
            public static GameObject CreateSphere(Transform parent, string objectName, float radius, Material material)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.localScale = Vector3.one * (radius * 2f);
                return Configure(sphere, parent, objectName, material);
            }

            public static GameObject CreateBox(Transform parent, string objectName, Vector3 size, Material material)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.transform.localScale = size;
                return Configure(box, parent, objectName, material);
            }

            public static GameObject CreateTaperedCylinder(
                Transform parent,
                string objectName,
                float bottomRadius,
                float topRadius,
                float length,
                int segments,
                Material material)
            {
                var meshObject = new GameObject(objectName);
                var mesh = BuildTaperedCylinderMesh(bottomRadius, topRadius, length, Mathf.Max(3, segments));
                var filter = meshObject.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                meshObject.AddComponent<MeshRenderer>();
                return Configure(meshObject, parent, objectName, material);
            }

            public static GameObject CreateTorus(
                Transform parent,
                string objectName,
                float ringRadius,
                float tubeRadius,
                int tubeSegments,
                int ringSegments,
                Material material)
            {
                var meshObject = new GameObject(objectName);
                var mesh = BuildTorusMesh(ringRadius, tubeRadius, Mathf.Max(3, tubeSegments), Mathf.Max(3, ringSegments));
                var filter = meshObject.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                meshObject.AddComponent<MeshRenderer>();
                return Configure(meshObject, parent, objectName, material);
            }

            private static GameObject Configure(GameObject target, Transform parent, string objectName, Material material)
            {
                target.name = objectName;
                target.transform.SetParent(parent, false);

                var collider = target.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(collider);
                    }
                    else
                    {
                        DestroyImmediate(collider);
                    }
                }

                if (target.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                return target;
            }

            // Cylinder along local Z, centred on the origin so it spans [-length/2, +length/2]
            // exactly like Three.js CylinderGeometry (bottomRadius at -Z, topRadius at +Z).
            // The ported prop positions are centre positions, so the mesh must be centred too.
            // End caps use their own rim vertices: sharing the side wall's rim vertices would
            // make RecalculateNormals average the axial cap normal with the radial side
            // normal, shading the flat caps like a flared bell (visible at the mic tail).
            private static Mesh BuildTaperedCylinderMesh(float bottomRadius, float topRadius, float length, int segments)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                var halfLength = length * 0.5f;

                for (var i = 0; i <= segments; i += 1)
                {
                    var angle = (float)i / segments * Mathf.PI * 2f;
                    var cos = Mathf.Cos(angle);
                    var sin = Mathf.Sin(angle);
                    vertices.Add(new Vector3(cos * bottomRadius, sin * bottomRadius, -halfLength));
                    vertices.Add(new Vector3(cos * topRadius, sin * topRadius, halfLength));
                }

                for (var i = 0; i < segments; i += 1)
                {
                    var b0 = i * 2;
                    var t0 = i * 2 + 1;
                    var b1 = (i + 1) * 2;
                    var t1 = (i + 1) * 2 + 1;
                    triangles.Add(b0);
                    triangles.Add(b1);
                    triangles.Add(t0);
                    triangles.Add(t0);
                    triangles.Add(b1);
                    triangles.Add(t1);
                }

                var bottomCenter = vertices.Count;
                vertices.Add(new Vector3(0f, 0f, -halfLength));
                var bottomRimStart = vertices.Count;
                for (var i = 0; i <= segments; i += 1)
                {
                    var angle = (float)i / segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * bottomRadius, Mathf.Sin(angle) * bottomRadius, -halfLength));
                }

                var topCenter = vertices.Count;
                vertices.Add(new Vector3(0f, 0f, halfLength));
                var topRimStart = vertices.Count;
                for (var i = 0; i <= segments; i += 1)
                {
                    var angle = (float)i / segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * topRadius, Mathf.Sin(angle) * topRadius, halfLength));
                }

                for (var i = 0; i < segments; i += 1)
                {
                    triangles.Add(bottomCenter);
                    triangles.Add(bottomRimStart + i + 1);
                    triangles.Add(bottomRimStart + i);

                    triangles.Add(topCenter);
                    triangles.Add(topRimStart + i);
                    triangles.Add(topRimStart + i + 1);
                }

                var mesh = new Mesh { name = "TsukiVox Tapered Cylinder" };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            // Torus encircling the local Z axis (a band around the mic neck).
            private static Mesh BuildTorusMesh(float ringRadius, float tubeRadius, int tubeSegments, int ringSegments)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();

                for (var ring = 0; ring <= ringSegments; ring += 1)
                {
                    var ringAngle = (float)ring / ringSegments * Mathf.PI * 2f;
                    var ringCos = Mathf.Cos(ringAngle);
                    var ringSin = Mathf.Sin(ringAngle);

                    for (var tube = 0; tube <= tubeSegments; tube += 1)
                    {
                        var tubeAngle = (float)tube / tubeSegments * Mathf.PI * 2f;
                        var r = ringRadius + tubeRadius * Mathf.Cos(tubeAngle);
                        vertices.Add(new Vector3(r * ringCos, r * ringSin, tubeRadius * Mathf.Sin(tubeAngle)));
                    }
                }

                var stride = tubeSegments + 1;
                for (var ring = 0; ring < ringSegments; ring += 1)
                {
                    for (var tube = 0; tube < tubeSegments; tube += 1)
                    {
                        var a = ring * stride + tube;
                        var b = (ring + 1) * stride + tube;
                        var c = a + 1;
                        var d = b + 1;
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                        triangles.Add(c);
                        triangles.Add(b);
                        triangles.Add(d);
                    }
                }

                var mesh = new Mesh { name = "TsukiVox Torus" };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
