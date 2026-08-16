using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TsukiVox.AudioPrototype
{
    /// <summary>
    /// Room-wide color theme. Dark uses a charcoal champagne-moonlight palette,
    /// while Bright swaps every material and light to a restrained daylight look
    /// without touching geometry sizes or positions.
    /// </summary>
    public enum RoomTheme
    {
        Dark = 0,
        Bright = 1,
    }

    /// <summary>
    /// V0.5 premium VR KTV room. Tracking space equals world space in this project,
    /// so the room is built around the origin: the sofa seat sits at (0, 0, 0) and the
    /// video screen hangs on the front wall along +Z. Geometry, feedback targets and
    /// anchors are procedurally rebuilt under this component from a central palette.
    /// </summary>
    public sealed class QuestKtvRoomPrototype : MonoBehaviour
    {
        public const string RoomRootName = "V0.5 KTV Room";
        public const int CurrentDesignRevision = 27;
        public const string ThemePrefsKey = "TsukiVox.RoomTheme";
        public const string StarsPrefsKey = "TsukiVox.CeilingStarsEnabled";
        public const string AuroraPrefsKey = "TsukiVox.CeilingAuroraEnabled";

        // Player start is the world/tracking origin; recentering returns the user to the sofa.
        public static readonly Vector3 PlayerStartPosition = Vector3.zero;
        public static readonly Vector3 ScreenPosition = new Vector3(0f, 1.72f, FrontWallZ - ScreenWallOffset);
        public static readonly Quaternion ScreenRotation = Quaternion.identity;
        // Keep a slim optical bezel around 16:9 content. Source aspect ratios are
        // handled separately with contain semantics, so this margin is not needed
        // to prevent video cropping.
        private const float ScreenSafeScale = 0.96f;
        public static readonly Vector2 ScreenMatteSize = new Vector2(3.8f, 2.1375f);
        public static readonly Vector2 ScreenSafeSize = ScreenMatteSize * ScreenSafeScale;
        public static readonly Vector3 LeftSpeakerPosition = new Vector3(-2.78f, 2.38f, 4.7f);
        public static readonly Vector3 RightSpeakerPosition = new Vector3(2.78f, 2.38f, 4.7f);
        public static readonly Vector3 SpeakerAudioPosition =
            (LeftSpeakerPosition + RightSpeakerPosition) * 0.5f;
        public const float SpeakerSpatialBlend = 0.82f;
        public const float SpeakerStereoSpreadDegrees = 62f;
        public const float SpeakerMinDistance = 4.5f;
        public const float SpeakerMaxDistance = 12f;
        public const float SpeakerReverbZoneMix = 0.2f;

        private const string GeometryRootName = "Geometry";
        private const string FeedbackRootName = "Feedback";
        private const string AnchorsRootName = "Anchors";
        private const string CeilingStarsName = "ceiling starfield";
        private const string CeilingAuroraName = "ceiling aurora";

        private const float RoomWidth = 7.2f;
        private const float RoomHeight = 2.9f;
        private const float BackWallZ = -1.6f;
        private const float FrontWallZ = 5f;
        private const float ScreenWallOffset = 0.11f;
        private const float LevelBarHeight = 1.9f;
        private const float LevelBarMinHeight = 0.08f;
        private const float LevelBarCenterY = 1.55f;
        private const float LevelBarX = 2.35f;
        private const float MaximumTrimBevelRatio = 0.2f;
        private const float WarmLightBaseEmission = 0.72f;
        private const float WarmLightLevelEmission = 0.55f;
        private const float WarmLightPulseEmission = 0.08f;

        private static readonly string[] LegacyPreviewNames =
        {
            "Input Meter Preview",
            "Output Meter Preview",
        };

        [Header("Scene References")]
        [SerializeField] private QuestAudioPrototype audioPrototype;
        [SerializeField] private QuestVideoScreenPrototype videoScreenPrototype;

        [Header("Runtime")]
        [SerializeField] private bool configureOnAwake = true;
        [SerializeField] private bool driveFeedbackFromMic = true;
        [SerializeField, Range(0.01f, 1f)] private float feedbackSmoothing = 0.22f;
        [SerializeField, HideInInspector] private int generatedDesignRevision;

        private readonly List<MeshRenderer> levelBarFills = new List<MeshRenderer>();

        private Transform geometryRoot;
        private Transform feedbackRoot;
        private Transform anchorsRoot;
        private Material levelBarFillMaterial;
        private Material lightStripMaterial;
        private Light screenGlow;
        private Light loungeGlow;
        private Light leftWallGlow;
        private Light rightWallGlow;
        private MeshRenderer ceilingStarsRenderer;
        private MeshRenderer ceilingAuroraRenderer;
        private float smoothedLevel;
        private RoomTheme currentTheme = RoomTheme.Dark;
        private bool starsEnabled = true;
        private bool auroraEnabled = true;

        public bool NeedsDesignRefresh => generatedDesignRevision < CurrentDesignRevision;

        public RoomTheme CurrentTheme => currentTheme;

        public bool StarsEnabled => starsEnabled;

        public bool AuroraEnabled => auroraEnabled;

        public event Action<RoomTheme> ThemeChanged;

        public static QuestKtvRoomPrototype EnsureSceneRoom()
        {
            var existing = FindAnyObjectByType<QuestKtvRoomPrototype>();
            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    existing.ConfigureRuntimeReferences();
                }
                else
                {
                    existing.ConfigureSceneReferences();
                }

                return existing;
            }

            var roomObject = new GameObject(RoomRootName);
            var room = roomObject.AddComponent<QuestKtvRoomPrototype>();
            if (!Application.isPlaying)
            {
                room.ConfigureSceneReferences();
            }

            return room;
        }

        private void Awake()
        {
            if (configureOnAwake)
            {
                ConfigureRuntimeReferences();
            }
        }

        private void Update()
        {
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
            ApplyFeedback(smoothedLevel);
        }

        public void ConfigureSceneReferences()
        {
            name = RoomRootName;
            audioPrototype = audioPrototype != null ? audioPrototype : FindAnyObjectByType<QuestAudioPrototype>();
            videoScreenPrototype = videoScreenPrototype != null ? videoScreenPrototype : FindAnyObjectByType<QuestVideoScreenPrototype>();

            // Generated scenes are always saved with the dark baseline palette;
            // the bright theme is a runtime-only retint driven by PlayerPrefs.
            currentTheme = RoomTheme.Dark;
            starsEnabled = true;
            auroraEnabled = true;
            EnsureRoots();
            DetachControlCanvasFromGeneratedGeometry();
            RemoveLegacySceneProps();
            BuildRoom();
            ApplyCelestialVisibility();
            ConfigureLighting();
            ApplyVideoScreenLayout();
            CreateAnchors();
            ApplyFeedback(smoothedLevel);
            generatedDesignRevision = CurrentDesignRevision;
        }

        private void ConfigureRuntimeReferences()
        {
            name = RoomRootName;
            audioPrototype = audioPrototype != null ? audioPrototype : FindAnyObjectByType<QuestAudioPrototype>();
            videoScreenPrototype = videoScreenPrototype != null ? videoScreenPrototype : FindAnyObjectByType<QuestVideoScreenPrototype>();

            currentTheme = LoadPersistedTheme();
            starsEnabled = LoadPersistedToggle(StarsPrefsKey);
            auroraEnabled = LoadPersistedToggle(AuroraPrefsKey);
            EnsureRoots();
            if (geometryRoot.childCount == 0 || NeedsDesignRefresh)
            {
                // Runtime generation also upgrades scenes that have not yet been
                // resaved by the editor design-revision refresh.
                DetachControlCanvasFromGeneratedGeometry();
                BuildRoom();
                ConfigureLighting();
                generatedDesignRevision = CurrentDesignRevision;
            }
            else
            {
                BindGeneratedReferences();
                if (currentTheme != RoomTheme.Dark)
                {
                    // Saved scenes carry the dark baseline colors, so a persisted
                    // bright preference needs an in-place retint on startup.
                    RetintGeneratedMaterials(currentTheme);
                    ConfigureLighting();
                }
            }

            ApplyCelestialVisibility();
            ApplyVideoScreenLayout();
            CreateAnchors();
            ApplyFeedback(smoothedLevel);
        }

        /// <summary>
        /// Switches the room between the dark and bright palettes by retinting the
        /// generated shared materials in place and reapplying themed lighting.
        /// Geometry is never rebuilt here, so sizes, positions and the control
        /// canvas parented under the tablet anchor are untouched.
        /// </summary>
        public void ApplyTheme(RoomTheme theme)
        {
            var normalizedTheme = theme == RoomTheme.Bright ? RoomTheme.Bright : RoomTheme.Dark;
            var changed = normalizedTheme != currentTheme;
            currentTheme = normalizedTheme;
            EnsureRoots();
            RetintGeneratedMaterials(normalizedTheme);
            ConfigureLighting();
            ApplyFeedback(smoothedLevel);

            if (changed)
            {
                ThemeChanged?.Invoke(normalizedTheme);
            }
        }

        public void SetStarsEnabled(bool enabled)
        {
            starsEnabled = enabled;
            ApplyCelestialVisibility();
            PersistToggle(StarsPrefsKey, enabled);
        }

        public void SetAuroraEnabled(bool enabled)
        {
            auroraEnabled = enabled;
            ApplyCelestialVisibility();
            PersistToggle(AuroraPrefsKey, enabled);
        }

        public void RestoreCelestialDefaults()
        {
            starsEnabled = true;
            auroraEnabled = true;
            ApplyCelestialVisibility();
            PersistToggle(StarsPrefsKey, true);
            PersistToggle(AuroraPrefsKey, true);
        }

        private static RoomTheme LoadPersistedTheme()
        {
            if (!Application.isPlaying)
            {
                return RoomTheme.Dark;
            }

            var stored = PlayerPrefs.GetInt(ThemePrefsKey, (int)RoomTheme.Dark);
            return stored == (int)RoomTheme.Bright ? RoomTheme.Bright : RoomTheme.Dark;
        }

        private static bool LoadPersistedToggle(string key)
        {
            return !Application.isPlaying || PlayerPrefs.GetInt(key, 1) != 0;
        }

        private static void PersistToggle(string key, bool enabled)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerPrefs.SetInt(key, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void RetintGeneratedMaterials(RoomTheme theme)
        {
            var colors = RoomPalette.GetThemeColors(theme);
            RetintRendererMaterials(geometryRoot, colors);
            RetintRendererMaterials(feedbackRoot, colors);
        }

        private static void RetintRendererMaterials(Transform root, Dictionary<string, RoomPalette.ThemedColor> colors)
        {
            if (root == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (var index = 0; index < renderers.Length; index += 1)
            {
                var material = renderers[index].sharedMaterial;
                if (material == null || !colors.TryGetValue(material.name, out var themedColor))
                {
                    continue;
                }

                QuestStylizedMaterial.SetBaseColor(material, themedColor.Color);
                if (themedColor.EmissionIntensity > 0f)
                {
                    SetEmission(material, themedColor.Color, themedColor.EmissionIntensity);
                }
                else if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        private void BindGeneratedReferences()
        {
            levelBarFills.Clear();
            BindLevelBar("left mic level fill");
            BindLevelBar("right mic level fill");

            var strip = feedbackRoot.Find("ceiling light strip");
            if (strip != null && strip.TryGetComponent<MeshRenderer>(out var stripRenderer))
            {
                lightStripMaterial = stripRenderer.sharedMaterial;
            }

            screenGlow = FindLight("V0.5 Screen Glow");
            loungeGlow = FindLight("V0.5 Lounge Glow");
            leftWallGlow = FindLight("V0.5 Left Wall Glow");
            rightWallGlow = FindLight("V0.5 Right Wall Glow");
            var starsObject = feedbackRoot.Find(CeilingStarsName);
            ceilingStarsRenderer = starsObject != null ? starsObject.GetComponent<MeshRenderer>() : null;
            var auroraObject = feedbackRoot.Find(CeilingAuroraName);
            ceilingAuroraRenderer = auroraObject != null ? auroraObject.GetComponent<MeshRenderer>() : null;
        }

        private void ApplyCelestialVisibility()
        {
            if (ceilingStarsRenderer != null)
            {
                ceilingStarsRenderer.gameObject.SetActive(starsEnabled);
            }

            if (ceilingAuroraRenderer != null)
            {
                ceilingAuroraRenderer.gameObject.SetActive(auroraEnabled);
            }
        }

        private void BindLevelBar(string objectName)
        {
            var fill = feedbackRoot.Find(objectName);
            if (fill == null || !fill.TryGetComponent<MeshRenderer>(out var renderer))
            {
                return;
            }

            levelBarFills.Add(renderer);
            levelBarFillMaterial = renderer.sharedMaterial;
        }

        private void EnsureRoots()
        {
            geometryRoot = FindOrCreateChild(transform, GeometryRootName);
            feedbackRoot = FindOrCreateChild(transform, FeedbackRootName);
            anchorsRoot = FindOrCreateChild(transform, AnchorsRootName);
        }

        private void BuildRoom()
        {
            ClearChildren(geometryRoot);
            ClearChildren(feedbackRoot);
            levelBarFills.Clear();
            ceilingStarsRenderer = null;
            ceilingAuroraRenderer = null;

            var palette = RoomPalette.Create(currentTheme);
            levelBarFillMaterial = palette.Accent;
            lightStripMaterial = palette.Warm;

            BuildRoomShell(palette);
            BuildCelestialCeiling(palette);
            BuildWallDecoration(palette);
            BuildFurniture(palette);
            BuildScreenSurround(palette);
            BuildLevelBars(palette);
            BuildLightStrips(palette);
            BuildAudioEnvironment();
            BuildPerformanceArea(palette);
            BuildCornerDressing(palette);
        }

        private void BuildRoomShell(RoomPalette palette)
        {
            var roomCenterZ = (BackWallZ + FrontWallZ) * 0.5f;
            var roomDepth = FrontWallZ - BackWallZ;

            CreateBox(geometryRoot, "floor", new Vector3(RoomWidth, 0.1f, roomDepth), new Vector3(0f, -0.05f, roomCenterZ), palette.Floor);
            CreateBox(geometryRoot, "ceiling", new Vector3(RoomWidth, 0.1f, roomDepth), new Vector3(0f, RoomHeight + 0.05f, roomCenterZ), palette.Ceiling);
            CreateBox(geometryRoot, "front wall", new Vector3(RoomWidth, RoomHeight + 0.2f, 0.15f), new Vector3(0f, RoomHeight * 0.5f, FrontWallZ + 0.075f), palette.Wall);
            CreateBox(geometryRoot, "back wall", new Vector3(RoomWidth, RoomHeight + 0.2f, 0.15f), new Vector3(0f, RoomHeight * 0.5f, BackWallZ - 0.075f), palette.Wall);
            CreateBox(geometryRoot, "left wall", new Vector3(0.15f, RoomHeight + 0.2f, roomDepth), new Vector3(-RoomWidth * 0.5f - 0.075f, RoomHeight * 0.5f, roomCenterZ), palette.Wall);
            CreateBox(geometryRoot, "right wall", new Vector3(0.15f, RoomHeight + 0.2f, roomDepth), new Vector3(RoomWidth * 0.5f + 0.075f, RoomHeight * 0.5f, roomCenterZ), palette.Wall);

            for (var index = 0; index < 11; index += 1)
            {
                var x = -3f + index * 0.6f;
                CreateBox(geometryRoot, $"floor plank groove {index}", new Vector3(0.014f, 0.008f, roomDepth - 0.12f), new Vector3(x, 0.006f, roomCenterZ), palette.FloorGroove);
            }

            CreateTrimBox(geometryRoot, "floor inlay left", new Vector3(0.026f, 0.012f, roomDepth - 0.54f), new Vector3(-2.72f, 0.012f, roomCenterZ), palette.Trim, 0.005f, false, false);
            CreateTrimBox(geometryRoot, "floor inlay right", new Vector3(0.026f, 0.012f, roomDepth - 0.54f), new Vector3(2.72f, 0.012f, roomCenterZ), palette.Trim, 0.005f, false, false);

            CreateBeveledBox(geometryRoot, "lounge rug", new Vector3(3.2f, 0.022f, 1.9f), new Vector3(0f, 0.012f, 1.4f), palette.Rug, 0.01f);
            CreateTrimBox(geometryRoot, "rug front trim", new Vector3(3.26f, 0.03f, 0.04f), new Vector3(0f, 0.02f, 2.35f), palette.Trim, 0.012f);
            CreateTrimBox(geometryRoot, "rug back trim", new Vector3(3.26f, 0.03f, 0.04f), new Vector3(0f, 0.02f, 0.45f), palette.Trim, 0.012f);
            CreateTrimBox(geometryRoot, "rug left trim", new Vector3(0.04f, 0.03f, 1.9f), new Vector3(-1.62f, 0.02f, 1.4f), palette.Trim, 0.012f);
            CreateTrimBox(geometryRoot, "rug right trim", new Vector3(0.04f, 0.03f, 1.9f), new Vector3(1.62f, 0.02f, 1.4f), palette.Trim, 0.012f);

            // Stop the inset short of the front soffit so seated views can still
            // see the screen-top valance instead of a ceiling slab covering it.
            var insetDepth = roomDepth - 1.28f;
            var insetCenterZ = roomCenterZ - 0.28f;
            CreateBeveledBox(geometryRoot, "ceiling inset", new Vector3(5.7f, 0.08f, insetDepth), new Vector3(0f, RoomHeight - 0.07f, insetCenterZ), palette.CeilingInset, 0.02f);
            // Keep the front drop attached to the ceiling so it cannot hide the
            // screen's upper matte edge from seated headset viewpoints.
            CreateBeveledBox(geometryRoot, "ceiling front drop", new Vector3(6.3f, 0.18f, 0.18f), new Vector3(0f, RoomHeight - 0.09f, FrontWallZ - 0.44f), palette.Ceiling, 0.03f);
            CreateBeveledBox(geometryRoot, "ceiling back drop", new Vector3(6.3f, 0.18f, 0.18f), new Vector3(0f, RoomHeight - 0.16f, BackWallZ + 0.44f), palette.Ceiling, 0.03f);
            CreateBeveledBox(geometryRoot, "ceiling left drop", new Vector3(0.18f, 0.18f, roomDepth - 0.7f), new Vector3(-3.15f, RoomHeight - 0.16f, roomCenterZ), palette.Ceiling, 0.03f);
            CreateBeveledBox(geometryRoot, "ceiling right drop", new Vector3(0.18f, 0.18f, roomDepth - 0.7f), new Vector3(3.15f, RoomHeight - 0.16f, roomCenterZ), palette.Ceiling, 0.03f);

            var downlightIndex = 0;
            for (var ix = -1; ix <= 1; ix += 1)
            {
                for (var iz = -1; iz <= 1; iz += 1)
                {
                    var x = ix * 2.3f;
                    var z = 1.7f + iz * 1.55f;
                    var radius = ix == 0 && iz == 0 ? 0.12f : 0.085f;
                    CreateCylinder(
                        geometryRoot,
                        $"ceiling downlight {downlightIndex}",
                        radius,
                        0.015f,
                        new Vector3(x, RoomHeight - 0.02f, z),
                        palette.CeilingMoon);
                    downlightIndex += 1;
                }
            }

            var moonBacking = CreateCrescent(
                geometryRoot,
                "ceiling crescent backing",
                0.72f,
                0.69f,
                0.3f,
                0.045f,
                new Vector3(0f, RoomHeight - 0.095f, 1.7f),
                1f,
                palette.FocalTrim,
                false,
                false);
            moonBacking.transform.localRotation = Quaternion.Euler(90f, 0f, -18f);

            var moonLight = CreateCrescent(
                geometryRoot,
                "ceiling crescent light",
                0.62f,
                0.59f,
                0.255f,
                0.026f,
                new Vector3(0f, RoomHeight - 0.126f, 1.7f),
                1f,
                palette.CeilingMoon,
                false,
                false);
            moonLight.transform.localRotation = Quaternion.Euler(90f, 0f, -18f);
        }

        private void BuildCelestialCeiling(RoomPalette palette)
        {
            var effectSize = new Vector2(5.34f, 5.0f);
            var effectCenter = new Vector3(0f, RoomHeight - 0.115f, 1.42f);
            var aurora = CreateCeilingEffectPlane(
                feedbackRoot,
                CeilingAuroraName,
                effectSize,
                effectCenter,
                palette.Aurora);
            ceilingAuroraRenderer = aurora.GetComponent<MeshRenderer>();

            effectCenter.y -= 0.006f;
            var stars = CreateCeilingEffectPlane(
                feedbackRoot,
                CeilingStarsName,
                effectSize,
                effectCenter,
                palette.Stars);
            ceilingStarsRenderer = stars.GetComponent<MeshRenderer>();
        }

        private void BuildWallDecoration(RoomPalette palette)
        {
            const float sidePanelStartZ = -0.45f;
            const float sidePanelSpacing = 0.82f;
            const int sidePanelCount = 6;
            for (var index = 0; index < sidePanelCount; index += 1)
            {
                var z = sidePanelStartZ + index * sidePanelSpacing;
                var panelMaterial = index % 3 == 1 ? palette.WinePanel : palette.PaddedWall;
                CreateArchedPanel(geometryRoot, $"left panel shadow {index}", 0.74f, 1.92f, 0.18f, 0.055f, new Vector3(-RoomWidth * 0.5f + 0.04f, 1.34f, z), Quaternion.Euler(0f, -90f, 0f), palette.WallReveal);
                CreateArchedPanel(geometryRoot, $"left upholstered panel {index}", 0.64f, 1.78f, 0.16f, 0.04f, new Vector3(-RoomWidth * 0.5f + 0.075f, 1.33f, z), Quaternion.Euler(0f, -90f, 0f), panelMaterial);
                CreateArchedPanel(geometryRoot, $"right panel shadow {index}", 0.74f, 1.92f, 0.18f, 0.055f, new Vector3(RoomWidth * 0.5f - 0.04f, 1.34f, z), Quaternion.Euler(0f, 90f, 0f), palette.WallReveal);
                CreateArchedPanel(geometryRoot, $"right upholstered panel {index}", 0.64f, 1.78f, 0.16f, 0.04f, new Vector3(RoomWidth * 0.5f - 0.075f, 1.33f, z), Quaternion.Euler(0f, 90f, 0f), panelMaterial);

                if (index < sidePanelCount - 1)
                {
                    var dividerZ = z + sidePanelSpacing * 0.5f;
                    CreateTrimBox(geometryRoot, $"left brass divider {index}", new Vector3(0.045f, 1.66f, 0.025f), new Vector3(-RoomWidth * 0.5f + 0.095f, 1.26f, dividerZ), palette.Trim, 0.006f);
                    CreateTrimBox(geometryRoot, $"right brass divider {index}", new Vector3(0.045f, 1.66f, 0.025f), new Vector3(RoomWidth * 0.5f - 0.095f, 1.26f, dividerZ), palette.Trim, 0.006f);
                }
            }

            var sideRailStartZ = BackWallZ + 0.7f;
            var sideRailEndZ = FrontWallZ - 0.55f;
            var sideRailLength = sideRailEndZ - sideRailStartZ;
            var sideRailCenterZ = (sideRailStartZ + sideRailEndZ) * 0.5f;
            CreateTrimBox(geometryRoot, "left wall rail", new Vector3(0.06f, 0.075f, sideRailLength), new Vector3(-RoomWidth * 0.5f + 0.04f, 2.28f, sideRailCenterZ), palette.Trim, 0.018f);
            CreateTrimBox(geometryRoot, "right wall rail", new Vector3(0.06f, 0.075f, sideRailLength), new Vector3(RoomWidth * 0.5f - 0.04f, 2.28f, sideRailCenterZ), palette.Trim, 0.018f);
            CreateTrimBox(geometryRoot, "left wall lower rail", new Vector3(0.06f, 0.09f, sideRailLength), new Vector3(-RoomWidth * 0.5f + 0.04f, 0.38f, sideRailCenterZ), palette.Trim, 0.018f);
            CreateTrimBox(geometryRoot, "right wall lower rail", new Vector3(0.06f, 0.09f, sideRailLength), new Vector3(RoomWidth * 0.5f - 0.04f, 0.38f, sideRailCenterZ), palette.Trim, 0.018f);
            CreateBeveledBox(geometryRoot, "left timber wainscot", new Vector3(0.045f, 0.32f, sideRailLength), new Vector3(-RoomWidth * 0.5f + 0.045f, 0.19f, sideRailCenterZ), palette.Wood, 0.014f);
            CreateBeveledBox(geometryRoot, "right timber wainscot", new Vector3(0.045f, 0.32f, sideRailLength), new Vector3(RoomWidth * 0.5f - 0.045f, 0.19f, sideRailCenterZ), palette.Wood, 0.014f);

            for (var index = 0; index < 3; index += 1)
            {
                var x = -1.72f + index * 1.72f;
                var panelMaterial = index == 1 ? palette.WinePanel : palette.PaddedWall;
                CreateArchedPanel(geometryRoot, $"back panel shadow {index}", 1.46f, 1.52f, 0.28f, 0.055f, new Vector3(x, 1.17f, BackWallZ + 0.045f), Quaternion.Euler(0f, 180f, 0f), palette.WallReveal);
                CreateArchedPanel(geometryRoot, $"back upholstered panel {index}", 1.32f, 1.38f, 0.24f, 0.04f, new Vector3(x, 1.16f, BackWallZ + 0.08f), Quaternion.Euler(0f, 180f, 0f), panelMaterial);
            }

            CreateTrimBox(geometryRoot, "back wall top rail", new Vector3(5.7f, 0.07f, 0.065f), new Vector3(0f, 1.98f, BackWallZ + 0.055f), palette.Trim, 0.018f);
            CreateTrimBox(geometryRoot, "back wall low rail", new Vector3(5.9f, 0.085f, 0.065f), new Vector3(0f, 0.43f, BackWallZ + 0.035f), palette.Trim, 0.018f);
            CreateBeveledBox(geometryRoot, "back timber wainscot", new Vector3(5.65f, 0.32f, 0.045f), new Vector3(0f, 0.19f, BackWallZ + 0.045f), palette.Wood, 0.014f);
        }

        private void BuildFurniture(RoomPalette palette)
        {
            CreateSofa(palette, "back sofa", 4.6f, 1.0f, new Vector3(0f, 0f, -0.35f), Quaternion.identity);
            CreateSofa(palette, "left chaise", 2.3f, 0.95f, new Vector3(-2.95f, 0f, 1.5f), Quaternion.Euler(0f, 90f, 0f));
            CreateSofa(palette, "right chaise", 2.3f, 0.95f, new Vector3(2.95f, 0f, 1.5f), Quaternion.Euler(0f, -90f, 0f));
            CreateCoffeeTable(palette);
        }

        private void CreateSofa(RoomPalette palette, string sofaName, float width, float depth, Vector3 position, Quaternion rotation)
        {
            var sofaRoot = new GameObject(sofaName).transform;
            sofaRoot.SetParent(geometryRoot, false);
            sofaRoot.localPosition = position;
            sofaRoot.localRotation = rotation;

            CreateBeveledBox(sofaRoot, "plinth", new Vector3(width, 0.15f, depth), new Vector3(0f, 0.075f, 0f), palette.Wood, 0.045f);
            CreateBeveledBox(sofaRoot, "upholstered deck", new Vector3(width - 0.08f, 0.25f, depth - 0.08f), new Vector3(0f, 0.27f, 0.02f), palette.SofaShadow, 0.065f);
            CreateBeveledBox(sofaRoot, "front apron", new Vector3(width, 0.31f, 0.18f), new Vector3(0f, 0.29f, depth * 0.5f - 0.09f), palette.SofaShadow, 0.055f);
            CreateBeveledBox(sofaRoot, "back shell", new Vector3(width, 0.96f, 0.2f), new Vector3(0f, 0.63f, -depth * 0.5f + 0.1f), palette.SofaShadow, 0.07f);
            CreateBeveledBox(sofaRoot, "left arm", new Vector3(0.22f, 0.62f, depth - 0.04f), new Vector3(-width * 0.5f + 0.11f, 0.47f, 0f), palette.Sofa, 0.07f);
            CreateBeveledBox(sofaRoot, "right arm", new Vector3(0.22f, 0.62f, depth - 0.04f), new Vector3(width * 0.5f - 0.11f, 0.47f, 0f), palette.Sofa, 0.07f);
            CreateTrimBox(sofaRoot, "front trim", new Vector3(width - 0.3f, 0.035f, 0.03f), new Vector3(0f, 0.2f, depth * 0.5f + 0.015f), palette.Trim, 0.012f);
            CreateBeveledBox(sofaRoot, "underglow", new Vector3(width - 0.34f, 0.025f, 0.035f), new Vector3(0f, 0.12f, depth * 0.5f - 0.1f), palette.Warm, 0.01f);

            var cushionCount = Mathf.Max(2, Mathf.FloorToInt(width / 0.92f));
            var cushionWidth = (width - 0.42f) / cushionCount;
            for (var index = 0; index < cushionCount; index += 1)
            {
                var x = -width * 0.5f + 0.21f + cushionWidth * (index + 0.5f);
                var seatCushion = CreateBeveledBox(
                    sofaRoot,
                    $"seat cushion {index}",
                    new Vector3(cushionWidth - 0.035f, 0.2f, depth - 0.2f),
                    new Vector3(x, 0.405f, 0.065f),
                    index % 3 == 1 ? palette.SofaHighlight : palette.Sofa,
                    0.075f);
                seatCushion.transform.localRotation = Quaternion.Euler(-1.5f, 0f, 0f);

                var backCushion = CreateBeveledBox(
                    sofaRoot,
                    $"back cushion {index}",
                    new Vector3(cushionWidth - 0.045f, 0.52f, 0.12f),
                    new Vector3(x, 0.73f, -depth * 0.5f + 0.245f),
                    index % 3 == 1 ? palette.SofaHighlight : palette.Sofa,
                    0.065f);
                backCushion.transform.localRotation = Quaternion.Euler(-5f, 0f, 0f);
            }

            var pillowCount = width > 3f ? 2 : 1;
            for (var index = 0; index < pillowCount; index += 1)
            {
                var side = pillowCount == 1 ? -1f : index == 0 ? -1f : 1f;
                var pillow = CreateBeveledBox(
                    sofaRoot,
                    $"accent pillow {index}",
                    new Vector3(0.48f, 0.44f, 0.15f),
                    new Vector3(side * (width * 0.5f - 0.5f), 0.69f, -depth * 0.5f + 0.34f),
                    index % 2 == 0 ? palette.WinePanel : palette.Pillow,
                    0.1f);
                pillow.transform.localRotation = Quaternion.Euler(-7f, side * 6f, -side * 11f);
            }
        }

        private void CreateCoffeeTable(RoomPalette palette)
        {
            var tableRoot = new GameObject("coffee table").transform;
            tableRoot.SetParent(geometryRoot, false);
            tableRoot.localPosition = new Vector3(0f, 0f, 1.1f);

            // A veined stone slab and recessed brass frame give the table enough
            // visual weight to support the physical control panel.
            const float frameOuterX = 2.16f;
            const float frameOuterZ = 1.08f;
            const float railWidth = 0.07f;
            const float railHeight = 0.09f;
            const float frameCenterY = 0.545f;
            var railCenterX = frameOuterX * 0.5f - railWidth * 0.5f;
            var railCenterZ = frameOuterZ * 0.5f - railWidth * 0.5f;

            CreateBeveledBox(tableRoot, "moonstone top", new Vector3(2.08f, 0.075f, 1f), new Vector3(0f, 0.5575f, 0f), palette.Stone, 0.035f);
            CreateBeveledBox(tableRoot, "moonstone reveal", new Vector3(1.88f, 0.014f, 0.8f), new Vector3(0f, 0.602f, 0f), palette.Table, 0.006f, false, false);
            CreateTrimBox(tableRoot, "table rim front", new Vector3(frameOuterX, railHeight, railWidth), new Vector3(0f, frameCenterY, railCenterZ), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "table rim back", new Vector3(frameOuterX, railHeight, railWidth), new Vector3(0f, frameCenterY, -railCenterZ), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "table rim left", new Vector3(railWidth, railHeight, frameOuterZ - railWidth * 2f), new Vector3(-railCenterX, frameCenterY, 0f), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "table rim right", new Vector3(railWidth, railHeight, frameOuterZ - railWidth * 2f), new Vector3(railCenterX, frameCenterY, 0f), palette.Trim, 0.012f);

            // Lower shelf rests on stretcher rails that tie the four legs together.
            CreateTrimBox(tableRoot, "shelf stretcher front", new Vector3(2.0f, 0.035f, 0.05f), new Vector3(0f, 0.24f, 0.46f), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "shelf stretcher back", new Vector3(2.0f, 0.035f, 0.05f), new Vector3(0f, 0.24f, -0.46f), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "shelf stretcher left", new Vector3(0.05f, 0.035f, 0.87f), new Vector3(-1.0f, 0.24f, 0f), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "shelf stretcher right", new Vector3(0.05f, 0.035f, 0.87f), new Vector3(1.0f, 0.24f, 0f), palette.Trim, 0.012f);
            CreateBeveledBox(tableRoot, "table shelf", new Vector3(1.98f, 0.04f, 0.9f), new Vector3(0f, 0.2775f, 0f), palette.Wood, 0.015f);

            var tabletPivot = new GameObject(QuestTabletTiltController.TabletPivotName).transform;
            tabletPivot.SetParent(tableRoot, false);
            tabletPivot.localPosition = new Vector3(0f, 0.62f, -0.32f);
            tabletPivot.localRotation = Quaternion.Euler(30f, 0f, 0f);

            var tabletAnchor = new GameObject("Tablet Anchor").transform;
            tabletAnchor.SetParent(tabletPivot, false);
            tabletAnchor.localPosition = new Vector3(0f, 0.39f, 0f);
            tabletAnchor.localRotation = Quaternion.identity;

            // The tablet uses the same XY plane as the world-space control Canvas.
            // Its dark screen sits just behind that plane to avoid depth fighting.
            CreateBeveledBox(tabletAnchor, "tablet body", new Vector3(1.42f, 0.78f, 0.045f), new Vector3(0f, 0f, 0.0315f), palette.Table, 0.025f, true, true, false);
            CreateBeveledBox(tabletAnchor, "tablet screen", new Vector3(1.30f, 0.66f, 0.008f), new Vector3(0f, 0f, 0.005f), palette.ScreenFrame, 0.018f, false, false, false);
            var leftHinge = CreateCylinder(tabletPivot, "tablet hinge left", 0.03f, 0.14f, new Vector3(-0.60f, 0f, 0.025f), palette.Trim, true, true, false);
            leftHinge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var rightHinge = CreateCylinder(tabletPivot, "tablet hinge right", 0.03f, 0.14f, new Vector3(0.60f, 0f, 0.025f), palette.Trim, true, true, false);
            rightHinge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var tiltSwitchHousing = CreateBeveledBox(
                tableRoot,
                "tablet tilt switch housing",
                new Vector3(0.44f, 0.035f, 0.13f),
                new Vector3(0.79f, 0.598f, -0.436f),
                palette.Table,
                0.012f);
            tiltSwitchHousing.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            // Mirrors the tilt switch housing on the left-front corner and carries
            // the two-button room theme switch canvas.
            var themeSwitchHousing = CreateBeveledBox(
                tableRoot,
                "room theme switch housing",
                new Vector3(0.24f, 0.035f, 0.13f),
                new Vector3(-0.79f, 0.598f, -0.436f),
                palette.Table,
                0.012f);
            themeSwitchHousing.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            // Legs sit directly under the frame corners and run up into the rails,
            // so the rim never reads as unsupported from seated viewpoints.
            for (var index = 0; index < 4; index += 1)
            {
                var x = index % 2 == 0 ? -1.0f : 1.0f;
                var z = index < 2 ? -0.46f : 0.46f;
                CreateCylinder(tableRoot, $"table leg {index}", 0.04f, 0.56f, new Vector3(x, 0.28f, z), palette.Trim);
                CreateCylinder(tableRoot, $"table leg foot {index}", 0.05f, 0.02f, new Vector3(x, 0.01f, z), palette.Table);
            }
        }

        private void BuildScreenSurround(RoomPalette palette)
        {
            const float sideBorder = 0.19f;
            const float topBorder = 0.12f;
            const float frameDepth = 0.08f;
            const float frameBevel = 0.006f;
            const float trimHeight = 0.1f;
            const float trimDepth = 0.09f;
            const float trimGap = 0.055f;
            // Keep the existing outer silhouette below the ceiling. The wider bezel
            // grows into the matte's safe margin without covering the video surface.
            var frameOuter = ScreenMatteSize + new Vector2(0.32f, 0.2f);
            var frameZ = ScreenPosition.z + frameDepth * 0.5f;

            CreateArchedPanel(
                geometryRoot,
                "screen feature wall shadow",
                5.54f,
                2.78f,
                0.4f,
                0.06f,
                new Vector3(0f, 1.45f, FrontWallZ - 0.005f),
                Quaternion.identity,
                palette.FocalTrim);
            CreateArchedPanel(
                geometryRoot,
                "screen feature wall upholstery",
                5.32f,
                2.62f,
                0.35f,
                0.038f,
                new Vector3(0f, 1.43f, FrontWallZ - 0.035f),
                Quaternion.identity,
                palette.WinePanel);
            CreateTrimBox(geometryRoot, "screen halo left", new Vector3(0.035f, 1.78f, 0.025f), new Vector3(-2.31f, 1.42f, FrontWallZ - 0.075f), palette.Warm, 0.006f, false, false);
            CreateTrimBox(geometryRoot, "screen halo right", new Vector3(0.035f, 1.78f, 0.025f), new Vector3(2.31f, 1.42f, FrontWallZ - 0.075f), palette.Warm, 0.006f, false, false);

            CreateBeveledBox(geometryRoot, "screen frame top", new Vector3(frameOuter.x, topBorder, frameDepth), new Vector3(0f, ScreenPosition.y + frameOuter.y * 0.5f - topBorder * 0.5f, frameZ), palette.ScreenBezel, frameBevel);
            CreateBeveledBox(geometryRoot, "screen frame bottom", new Vector3(frameOuter.x, topBorder, frameDepth), new Vector3(0f, ScreenPosition.y - frameOuter.y * 0.5f + topBorder * 0.5f, frameZ), palette.ScreenBezel, frameBevel);
            CreateBeveledBox(geometryRoot, "screen frame left", new Vector3(sideBorder, frameOuter.y - topBorder * 2f, frameDepth), new Vector3(-frameOuter.x * 0.5f + sideBorder * 0.5f, ScreenPosition.y, frameZ), palette.ScreenBezel, frameBevel);
            CreateBeveledBox(geometryRoot, "screen frame right", new Vector3(sideBorder, frameOuter.y - topBorder * 2f, frameDepth), new Vector3(frameOuter.x * 0.5f - sideBorder * 0.5f, ScreenPosition.y, frameZ), palette.ScreenBezel, frameBevel);

            var trimCenterY = ScreenPosition.y - frameOuter.y * 0.5f - trimGap - trimHeight * 0.5f;
            var trimZ = ScreenPosition.z + trimDepth * 0.5f;
            CreateBeveledBox(
                geometryRoot,
                "screen bottom trim",
                new Vector3(frameOuter.x + 0.4f, trimHeight, trimDepth),
                new Vector3(0f, trimCenterY, trimZ),
                palette.FocalTrim,
                0.006f);
            CreateSpeakerAcousticPanel(palette, "left speaker acoustic panel", -2.68f);
            CreateSpeakerAcousticPanel(palette, "right speaker acoustic panel", 2.68f);

            CreateSpeaker(palette, "left speaker", LeftSpeakerPosition.x);
            CreateSpeaker(palette, "right speaker", RightSpeakerPosition.x);
        }

        private void CreateSpeakerAcousticPanel(RoomPalette palette, string objectName, float x)
        {
            var panelRoot = new GameObject(objectName).transform;
            panelRoot.SetParent(geometryRoot, false);
            panelRoot.localPosition = new Vector3(x, 1.43f, 0f);

            CreateArchedPanel(
                panelRoot,
                "shadow reveal",
                0.7f,
                2.28f,
                0.2f,
                0.025f,
                new Vector3(0f, 0f, FrontWallZ - 0.035f),
                Quaternion.identity,
                palette.SofaShadow,
                false,
                true);
            CreateArchedPanel(
                panelRoot,
                "fabric inset",
                0.58f,
                2.14f,
                0.18f,
                0.045f,
                new Vector3(0f, 0f, FrontWallZ - 0.065f),
                Quaternion.identity,
                palette.PaddedWall,
                false,
                true);

            for (var channel = -1; channel <= 1; channel += 1)
            {
                CreateTrimBox(
                    panelRoot,
                    $"acoustic channel {channel + 1}",
                    new Vector3(0.014f, 1.58f, 0.012f),
                    new Vector3(channel * 0.16f, -0.16f, FrontWallZ - 0.091f),
                    palette.WallReveal,
                    0.003f,
                    false,
                    false);
            }
        }

        private void CreateSpeaker(RoomPalette palette, string objectName, float x)
        {
            var speakerY = SpeakerAudioPosition.y;
            var speakerZ = SpeakerAudioPosition.z;
            var side = Mathf.Sign(x);

            CreateSpeakerMount(palette, objectName, x, speakerY);

            var speakerRoot = new GameObject(objectName).transform;
            speakerRoot.SetParent(geometryRoot, false);
            speakerRoot.localPosition = new Vector3(x, speakerY, speakerZ);
            speakerRoot.localRotation = Quaternion.Euler(-7f, side * 11f, 0f);

            CreateTaperedSpeakerCabinet(
                speakerRoot,
                "tapered cabinet",
                new Vector2(0.58f, 0.4f),
                new Vector2(0.48f, 0.34f),
                0.36f,
                0.035f,
                Vector3.zero,
                palette.SpeakerCabinet);
            CreateBeveledBox(
                speakerRoot,
                "recessed grille",
                new Vector3(0.515f, 0.325f, 0.025f),
                new Vector3(0f, 0f, -0.185f),
                palette.SpeakerGrille,
                0.018f);

            for (var index = -3; index <= 3; index += 1)
            {
                CreateBox(
                    speakerRoot,
                    $"grille relief {index + 3}",
                    new Vector3(0.455f, 0.006f, 0.004f),
                    new Vector3(0f, index * 0.038f, -0.199f),
                    palette.SpeakerDetail,
                    false,
                    false);
            }

            CreateTrimBox(
                speakerRoot,
                "brand badge",
                new Vector3(0.105f, 0.027f, 0.012f),
                new Vector3(0f, -0.127f, -0.204f),
                palette.FocalTrim,
                0.006f,
                false,
                false);

            for (var hingeSide = -1; hingeSide <= 1; hingeSide += 2)
            {
                var hinge = CreateCylinder(
                    speakerRoot,
                    hingeSide < 0 ? "left yoke cap" : "right yoke cap",
                    0.042f,
                    0.018f,
                    new Vector3(hingeSide * 0.252f, 0f, 0.085f),
                    palette.SpeakerHardware);
                hinge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private void CreateSpeakerMount(RoomPalette palette, string speakerName, float x, float y)
        {
            var mountRoot = new GameObject($"{speakerName} mount").transform;
            mountRoot.SetParent(geometryRoot, false);
            mountRoot.localPosition = new Vector3(x, y, 0f);

            CreateBeveledBox(
                mountRoot,
                "wall plate",
                new Vector3(0.17f, 0.21f, 0.028f),
                new Vector3(0f, 0f, FrontWallZ - 0.115f),
                palette.SpeakerHardware,
                0.012f);
            CreateBeveledBox(
                mountRoot,
                "support arm",
                new Vector3(0.065f, 0.065f, 0.15f),
                new Vector3(0f, 0f, FrontWallZ - 0.205f),
                palette.SpeakerHardware,
                0.012f);

            var pivot = CreateCylinder(
                mountRoot,
                "aiming pivot",
                0.045f,
                0.28f,
                new Vector3(0f, 0f, FrontWallZ - 0.28f),
                palette.SpeakerHardware);
            pivot.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            for (var boltIndex = -1; boltIndex <= 1; boltIndex += 2)
            {
                var bolt = CreateCylinder(
                    mountRoot,
                    boltIndex < 0 ? "upper wall bolt" : "lower wall bolt",
                    0.012f,
                    0.007f,
                    new Vector3(0f, boltIndex * 0.065f, FrontWallZ - 0.098f),
                    palette.FocalTrim,
                    false,
                    false);
                bolt.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private void BuildLevelBars(RoomPalette palette)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var sideName = side < 0 ? "left" : "right";
                CreateBeveledBox(
                    feedbackRoot,
                    $"{sideName} mic level track",
                    new Vector3(0.12f, LevelBarHeight, 0.03f),
                    new Vector3(LevelBarX * side, LevelBarCenterY, ScreenPosition.z + 0.02f),
                    palette.LevelTrack,
                    0.012f,
                    false,
                    false,
                    false);

                // The fill stays a plain box: ApplyFeedback rescales it on Y every
                // frame, which would visibly stretch a beveled mesh's corner radius.
                var fill = CreateBox(
                    feedbackRoot,
                    $"{sideName} mic level fill",
                    new Vector3(0.09f, LevelBarMinHeight, 0.04f),
                    new Vector3(LevelBarX * side, LevelBarCenterY - (LevelBarHeight - LevelBarMinHeight) * 0.5f, ScreenPosition.z + 0.005f),
                    palette.Accent,
                    false,
                    false,
                    false);
                if (fill.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    levelBarFills.Add(renderer);
                }
            }
        }

        private void BuildLightStrips(RoomPalette palette)
        {
            var sideStripStartZ = BackWallZ + 0.75f;
            var sideStripEndZ = FrontWallZ - 0.3f;
            var sideStripLength = sideStripEndZ - sideStripStartZ;
            var sideStripCenterZ = (sideStripStartZ + sideStripEndZ) * 0.5f;
            CreateBeveledBox(feedbackRoot, "ceiling light strip", new Vector3(6.2f, 0.07f, 0.07f), new Vector3(0f, RoomHeight - 0.08f, FrontWallZ - 0.3f), palette.Warm, 0.01f, false, false, false);
            CreateBeveledBox(feedbackRoot, "left light strip", new Vector3(0.07f, 0.07f, sideStripLength), new Vector3(-RoomWidth * 0.5f + 0.06f, RoomHeight - 0.12f, sideStripCenterZ), palette.Warm, 0.01f, false, false, false);
            CreateBeveledBox(feedbackRoot, "right light strip", new Vector3(0.07f, 0.07f, sideStripLength), new Vector3(RoomWidth * 0.5f - 0.06f, RoomHeight - 0.12f, sideStripCenterZ), palette.Warm, 0.01f, false, false, false);
        }

        private void BuildAudioEnvironment()
        {
            var reverbObject = new GameObject("small room reflections");
            reverbObject.transform.SetParent(feedbackRoot, false);
            reverbObject.transform.localPosition = new Vector3(0f, RoomHeight * 0.5f, (BackWallZ + FrontWallZ) * 0.5f);

            var reverbZone = reverbObject.AddComponent<AudioReverbZone>();
            reverbZone.reverbPreset = AudioReverbPreset.Livingroom;
            reverbZone.minDistance = 4.2f;
            reverbZone.maxDistance = 5.6f;
        }

        private void BuildPerformanceArea(RoomPalette palette)
        {
            const float stageWidth = 1.72f;
            const float stageDepth = 1.08f;
            const float stageHeight = 0.09f;
            const float stageZ = 3.42f;
            const float runnerWidth = 1.12f;
            const float runnerDepth = 1.14f;
            const float runnerZ = 2.48f;

            CreateEllipticalCylinder(
                geometryRoot,
                "stage plinth",
                new Vector2(stageWidth + 0.16f, stageDepth + 0.16f),
                0.035f,
                new Vector3(0f, 0.0175f, stageZ),
                palette.SofaShadow);
            CreateEllipticalCylinder(
                feedbackRoot,
                "stage halo",
                new Vector2(stageWidth + 0.12f, stageDepth + 0.12f),
                0.026f,
                new Vector3(0f, 0.041f, stageZ),
                palette.Warm,
                false,
                false,
                false);
            CreateEllipticalCylinder(
                geometryRoot,
                "stage brass edge",
                new Vector2(stageWidth + 0.04f, stageDepth + 0.04f),
                0.05f,
                new Vector3(0f, 0.06f, stageZ),
                palette.Trim);
            CreateEllipticalCylinder(
                geometryRoot,
                "stage deck",
                new Vector2(stageWidth - 0.08f, stageDepth - 0.08f),
                stageHeight - 0.035f,
                new Vector3(0f, 0.0775f, stageZ),
                palette.Stage);

            CreateBeveledBox(
                geometryRoot,
                "runner rug",
                new Vector3(runnerWidth, 0.018f, runnerDepth),
                new Vector3(0f, 0.011f, runnerZ),
                palette.Rug,
                0.01f);
            CreateTrimBox(
                geometryRoot,
                "runner front trim",
                new Vector3(runnerWidth + 0.04f, 0.024f, 0.03f),
                new Vector3(0f, 0.018f, runnerZ + runnerDepth * 0.5f),
                palette.Trim,
                0.008f);
            CreateTrimBox(
                geometryRoot,
                "runner back trim",
                new Vector3(runnerWidth + 0.04f, 0.024f, 0.03f),
                new Vector3(0f, 0.018f, runnerZ - runnerDepth * 0.5f),
                palette.Trim,
                0.008f);
            CreateTrimBox(
                geometryRoot,
                "runner left trim",
                new Vector3(0.03f, 0.024f, runnerDepth),
                new Vector3(-runnerWidth * 0.5f - 0.01f, 0.018f, runnerZ),
                palette.Trim,
                0.008f);
            CreateTrimBox(
                geometryRoot,
                "runner right trim",
                new Vector3(0.03f, 0.024f, runnerDepth),
                new Vector3(runnerWidth * 0.5f + 0.01f, 0.018f, runnerZ),
                palette.Trim,
                0.008f);
        }

        private void BuildCornerDressing(RoomPalette palette)
        {
            CreateCornerAccent(-1f, palette);
            CreateCornerAccent(1f, palette);
        }

        private void CreateCornerAccent(float side, RoomPalette palette)
        {
            var sideName = side < 0f ? "left" : "right";
            var wallX = side * (RoomWidth * 0.5f - 0.07f);

            CreateBeveledBox(
                geometryRoot,
                $"{sideName} corner pilaster",
                new Vector3(0.11f, 2.42f, 0.46f),
                new Vector3(wallX, 1.21f, 3.98f),
                palette.WinePanel,
                0.02f);
            CreateTrimBox(
                geometryRoot,
                $"{sideName} corner pilaster cap",
                new Vector3(0.14f, 0.04f, 0.5f),
                new Vector3(wallX, 2.44f, 3.98f),
                palette.Trim,
                0.01f);
            CreateTrimBox(
                geometryRoot,
                $"{sideName} corner pilaster base",
                new Vector3(0.15f, 0.05f, 0.52f),
                new Vector3(wallX, 0.025f, 3.98f),
                palette.Trim,
                0.012f);
            CreateTrimBox(
                geometryRoot,
                $"{sideName} corner pilaster inlay",
                new Vector3(0.02f, 1.7f, 0.03f),
                new Vector3(wallX - side * 0.06f, 1.28f, 3.98f),
                palette.Trim,
                0.006f);
            CreateBeveledBox(
                geometryRoot,
                $"{sideName} corner sconce",
                new Vector3(0.04f, 0.03f, 0.22f),
                new Vector3(wallX - side * 0.08f, 2.18f, 3.98f),
                palette.Warm,
                0.008f,
                false,
                false,
                false);

            var pedestalRoot = new GameObject($"{sideName} corner pedestal").transform;
            pedestalRoot.SetParent(geometryRoot, false);
            pedestalRoot.localPosition = new Vector3(side * 3.18f, 0f, 3.42f);
            CreateBeveledBox(pedestalRoot, "block", new Vector3(0.34f, 0.38f, 0.34f), new Vector3(0f, 0.19f, 0f), palette.Table, 0.02f);
            CreateTrimBox(pedestalRoot, "plate", new Vector3(0.38f, 0.02f, 0.38f), new Vector3(0f, 0.39f, 0f), palette.Trim, 0.006f);
            CreateBeveledBox(
                pedestalRoot,
                "underglow",
                new Vector3(0.28f, 0.016f, 0.28f),
                new Vector3(0f, 0.02f, 0f),
                palette.Warm,
                0.006f,
                false,
                false,
                false);

            CreateMoonRelic(pedestalRoot, side, palette);
            CreateDiscLamp(geometryRoot, $"{sideName} corner lamp", new Vector3(side * 3.2f, 0f, 4.46f), palette);
        }

        private static void CreateMoonRelic(Transform parent, float side, RoomPalette palette)
        {
            CreateCylinder(parent, "staff", 0.008f, 0.34f, new Vector3(0f, 0.57f, 0f), palette.FocalTrim);
            CreateCylinder(parent, "cradle", 0.07f, 0.014f, new Vector3(0f, 0.75f, 0f), palette.FocalTrim);
            CreateCrescent(
                parent,
                "crescent moon",
                0.115f,
                0.115f,
                0.055f,
                0.026f,
                new Vector3(0f, 0.9f, 0f),
                -side,
                palette.Moon,
                false,
                false,
                false);
            CreateCrescent(
                parent,
                "crescent aura",
                0.14f,
                0.14f,
                0.067f,
                0.008f,
                new Vector3(0f, 0.9f, 0.028f),
                -side,
                palette.Neon,
                false,
                false,
                false);

            for (var index = 0; index < 6; index += 1)
            {
                var angle = index * Mathf.PI / 3f + 0.35f;
                CreateSphere(
                    parent,
                    $"spark {index}",
                    index % 2 == 0 ? 0.012f : 0.008f,
                    new Vector3(Mathf.Cos(angle) * 0.16f, 0.9f + Mathf.Sin(angle) * 0.16f, -0.02f),
                    index % 2 == 0 ? palette.Neon : palette.Warm,
                    false,
                    false,
                    false);
            }

            CreateSphere(parent, "star a", 0.014f, new Vector3(side * 0.12f, 1.12f, -0.03f), palette.Neon, false, false, false);
            CreateSphere(parent, "star b", 0.009f, new Vector3(-side * 0.1f, 0.72f, -0.04f), palette.Warm, false, false, false);
        }

        private static void CreateDiscLamp(Transform parent, string objectName, Vector3 position, RoomPalette palette)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(parent, false);
            root.localPosition = position;

            CreateCylinder(root, "base", 0.1f, 0.02f, new Vector3(0f, 0.01f, 0f), palette.Table);
            CreateCylinder(root, "base ring", 0.108f, 0.008f, new Vector3(0f, 0.018f, 0f), palette.Trim);
            CreateCylinder(root, "pole", 0.012f, 1.28f, new Vector3(0f, 0.66f, 0f), palette.Trim);
            CreateCylinder(root, "shade", 0.18f, 0.032f, new Vector3(0f, 1.32f, 0f), palette.Warm);
            CreateCylinder(root, "shade ring", 0.16f, 0.01f, new Vector3(0f, 1.338f, 0f), palette.Trim);
            CreateSphere(root, "bulb", 0.028f, new Vector3(0f, 1.3f, 0f), palette.Warm);
        }

        private void ConfigureLighting()
        {
            var bright = currentTheme == RoomTheme.Bright;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = bright ? Rgb(0xC8CBD0) : Rgb(0x3A4354);
            RenderSettings.ambientEquatorColor = bright ? Rgb(0xB8B2A8) : Rgb(0x2A2428);
            RenderSettings.ambientGroundColor = bright ? Rgb(0x8C857B) : Rgb(0x151218);
            RenderSettings.ambientIntensity = bright ? 1.02f : 1.05f;
            RenderSettings.reflectionIntensity = bright ? 0.85f : 0.82f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = bright ? Rgb(0xCDD0D5) : Rgb(0x171B24);
            RenderSettings.fogDensity = bright ? 0.005f : 0.0065f;

            var keySpot = FindOrCreateLight("V0.5 Key Spot", LightType.Spot);
            keySpot.transform.position = new Vector3(0f, RoomHeight - 0.18f, 1.3f);
            keySpot.transform.rotation = Quaternion.Euler(78f, 0f, 0f);
            keySpot.color = bright ? Rgb(0xFFF2DA) : Rgb(0xE9D2AA);
            keySpot.intensity = bright ? 2.7f : 4.1f;
            keySpot.range = 7.5f;
            keySpot.spotAngle = 82f;
            keySpot.innerSpotAngle = 48f;
            keySpot.shadows = LightShadows.Soft;
            keySpot.shadowResolution = LightShadowResolution.Medium;
            keySpot.shadowStrength = bright ? 0.42f : 0.52f;
            keySpot.shadowBias = 0.035f;
            keySpot.shadowNormalBias = 0.25f;
            keySpot.renderMode = LightRenderMode.ForcePixel;
#if UNITY_EDITOR
            keySpot.lightmapBakeType = LightmapBakeType.Mixed;
#endif

            screenGlow = FindOrCreateLight("V0.5 Screen Glow", LightType.Point);
            screenGlow.transform.position = new Vector3(0f, ScreenPosition.y, ScreenPosition.z - 0.9f);
            screenGlow.color = bright ? Rgb(0xD2DAE8) : Rgb(0xA9C1DF);
            screenGlow.range = 5f;
            screenGlow.shadows = LightShadows.None;
            screenGlow.renderMode = LightRenderMode.ForcePixel;

            loungeGlow = FindOrCreateLight("V0.5 Lounge Glow", LightType.Point);
            loungeGlow.transform.position = new Vector3(0f, 1.25f, 0.7f);
            loungeGlow.color = bright ? Rgb(0xEBD9B9) : Rgb(0xF0CCA0);
            loungeGlow.range = 5f;
            loungeGlow.shadows = LightShadows.None;
            loungeGlow.renderMode = LightRenderMode.ForceVertex;

            leftWallGlow = ConfigureWallGlow("V0.5 Left Wall Glow", -2.95f, bright);
            rightWallGlow = ConfigureWallGlow("V0.5 Right Wall Glow", 2.95f, bright);
            DynamicGI.UpdateEnvironment();
        }

        private static Light ConfigureWallGlow(string objectName, float x, bool bright)
        {
            var light = FindOrCreateLight(objectName, LightType.Point);
            light.transform.position = new Vector3(x, 1.55f, 1.35f);
            light.color = bright ? Rgb(0xE5CFA7) : Rgb(0xD8B982);
            light.intensity = bright ? 0.38f : 0.48f;
            light.range = 3.6f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForceVertex;
            return light;
        }

        private void ApplyVideoScreenLayout()
        {
            if (videoScreenPrototype == null)
            {
                return;
            }

            videoScreenPrototype.ApplyScreenLayout(ScreenPosition, ScreenRotation, ScreenSafeSize, ScreenMatteSize);
        }

        private void CreateAnchors()
        {
            CreateAnchor("Player Start", PlayerStartPosition, Quaternion.identity);
            CreateAnchor("Screen Anchor", ScreenPosition, ScreenRotation);
            CreateAnchor("Control Panel Anchor", QuestAppShellPrototype.ControlPanelWorldPosition, QuestAppShellPrototype.ControlPanelWorldRotation);
        }

        private void ApplyFeedback(float level)
        {
            var pulse = Mathf.Sin(Time.unscaledTime * 2.2f) * 0.5f + 0.5f;

            var fillHeight = LevelBarMinHeight + level * (LevelBarHeight - LevelBarMinHeight);
            var fillCenterY = LevelBarCenterY - LevelBarHeight * 0.5f + fillHeight * 0.5f;
            for (var index = 0; index < levelBarFills.Count; index += 1)
            {
                var fill = levelBarFills[index];
                if (fill == null)
                {
                    continue;
                }

                var fillTransform = fill.transform;
                var scale = fillTransform.localScale;
                scale.y = fillHeight;
                fillTransform.localScale = scale;

                var position = fillTransform.localPosition;
                position.y = fillCenterY;
                fillTransform.localPosition = position;
            }

            SetEmission(
                levelBarFillMaterial,
                RoomPalette.GetAccentColor(currentTheme),
                0.18f + level * 2.8f + pulse * level * 0.32f);
            SetEmission(
                lightStripMaterial,
                RoomPalette.GetWarmColor(currentTheme),
                WarmLightBaseEmission + level * WarmLightLevelEmission + pulse * WarmLightPulseEmission);

            if (screenGlow != null)
            {
                screenGlow.intensity = 1.35f + level * 0.65f;
            }

            if (loungeGlow != null)
            {
                loungeGlow.intensity = 1.2f + level * 0.55f + pulse * 0.1f;
            }

            var wallIntensity = 0.52f + level * 0.2f + pulse * 0.04f;
            if (leftWallGlow != null)
            {
                leftWallGlow.intensity = wallIntensity;
            }

            if (rightWallGlow != null)
            {
                rightWallGlow.intensity = wallIntensity;
            }
        }

        private void RemoveLegacySceneProps()
        {
            for (var index = 0; index < LegacyPreviewNames.Length; index += 1)
            {
                DestroySceneObject(GameObject.Find(LegacyPreviewNames[index]));
            }

            // Older generated scenes used a bare directional light that washes out the room.
            var legacyKeyLight = GameObject.Find("Key Light");
            if (legacyKeyLight != null &&
                legacyKeyLight.TryGetComponent<Light>(out var legacyLight) &&
                legacyLight.type == LightType.Directional)
            {
                DestroySceneObject(legacyKeyLight);
            }
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

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }

        private void DetachControlCanvasFromGeneratedGeometry()
        {
            if (geometryRoot == null)
            {
                return;
            }

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (var index = 0; index < canvases.Length; index += 1)
            {
                if (canvases[index].name == "Prototype Canvas" && canvases[index].transform.IsChildOf(geometryRoot))
                {
                    canvases[index].transform.SetParent(null, true);
                    return;
                }
            }
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var index = parent.childCount - 1; index >= 0; index -= 1)
            {
                DestroySceneObject(parent.GetChild(index).gameObject);
            }
        }

        private static GameObject CreateCeilingEffectPlane(
            Transform parent,
            string objectName,
            Vector2 size,
            Vector3 localPosition,
            Material material)
        {
            var halfWidth = size.x * 0.5f;
            var halfDepth = size.y * 0.5f;
            var vertices = new List<Vector3>(4);
            var normals = new List<Vector3>(4);
            var uvs = new List<Vector2>(4);
            var triangles = new List<int>(6);
            AddMeshQuad(
                vertices,
                normals,
                uvs,
                triangles,
                new Vector3(-halfWidth, 0f, -halfDepth),
                new Vector3(halfWidth, 0f, -halfDepth),
                new Vector3(halfWidth, 0f, halfDepth),
                new Vector3(-halfWidth, 0f, halfDepth),
                Vector3.down);

            var mesh = new Mesh
            {
                name = $"{objectName} mesh",
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                uv = uvs.ToArray(),
                triangles = triangles.ToArray(),
            };
            mesh.RecalculateBounds();

            var plane = new GameObject(objectName);
            plane.transform.SetParent(parent, false);
            plane.transform.localPosition = localPosition;
            plane.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = plane.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material, false, false);
            plane.isStatic = true;
            return plane;
        }

        private static GameObject CreateBox(
            Transform parent,
            string objectName,
            Vector3 size,
            Vector3 localPosition,
            Material material,
            bool castShadows = true,
            bool receiveShadows = true,
            bool staticGeometry = true)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            return ConfigurePrimitive(cube, parent, objectName, size, localPosition, material, castShadows, receiveShadows, staticGeometry);
        }

        private static GameObject CreateCylinder(
            Transform parent,
            string objectName,
            float radius,
            float height,
            Vector3 localPosition,
            Material material,
            bool castShadows = true,
            bool receiveShadows = true,
            bool staticGeometry = true)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            // Unity's cylinder primitive is 2 units tall, so halve the requested height for scale.
            return ConfigurePrimitive(
                cylinder,
                parent,
                objectName,
                new Vector3(radius * 2f, height * 0.5f, radius * 2f),
                localPosition,
                material,
                castShadows,
                receiveShadows,
                staticGeometry);
        }

        private static GameObject CreateEllipticalCylinder(
            Transform parent,
            string objectName,
            Vector2 diameter,
            float height,
            Vector3 localPosition,
            Material material,
            bool castShadows = true,
            bool receiveShadows = true,
            bool staticGeometry = true)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            return ConfigurePrimitive(
                cylinder,
                parent,
                objectName,
                new Vector3(diameter.x, height * 0.5f, diameter.y),
                localPosition,
                material,
                castShadows,
                receiveShadows,
                staticGeometry);
        }

        private static GameObject CreateSphere(
            Transform parent,
            string objectName,
            float radius,
            Vector3 localPosition,
            Material material,
            bool castShadows = true,
            bool receiveShadows = true,
            bool staticGeometry = true)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            return ConfigurePrimitive(
                sphere,
                parent,
                objectName,
                Vector3.one * (radius * 2f),
                localPosition,
                material,
                castShadows,
                receiveShadows,
                staticGeometry);
        }

        private static GameObject CreateArchedPanel(
            Transform parent,
            string objectName,
            float width,
            float height,
            float archRise,
            float depth,
            Vector3 localPosition,
            Quaternion localRotation,
            Material material,
            bool castShadows = true,
            bool receiveShadows = true,
            bool staticGeometry = true)
        {
            var panel = new GameObject(objectName);
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localPosition;
            panel.transform.localRotation = localRotation;
            panel.AddComponent<MeshFilter>().sharedMesh = CreateArchedPanelMesh(objectName, width, height, archRise, depth);
            var renderer = panel.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material, castShadows, receiveShadows);
            panel.isStatic = staticGeometry;
            return panel;
        }

        private static Mesh CreateArchedPanelMesh(
            string meshName,
            float width,
            float height,
            float archRise,
            float depth)
        {
            const int archSegments = 12;
            var safeWidth = Mathf.Max(0.02f, width);
            var safeHeight = Mathf.Max(0.02f, height);
            var safeArchRise = Mathf.Clamp(archRise, 0.01f, safeHeight * 0.48f);
            var halfWidth = safeWidth * 0.5f;
            var halfHeight = safeHeight * 0.5f;
            var halfDepth = Mathf.Max(0.005f, depth * 0.5f);
            var archBaseY = halfHeight - safeArchRise;
            var outline = new List<Vector2>(archSegments + 4)
            {
                new Vector2(-halfWidth, -halfHeight),
                new Vector2(halfWidth, -halfHeight),
            };

            for (var index = 0; index <= archSegments; index += 1)
            {
                var angle = index / (float)archSegments * Mathf.PI;
                outline.Add(new Vector2(
                    Mathf.Cos(angle) * halfWidth,
                    archBaseY + Mathf.Sin(angle) * safeArchRise));
            }

            var vertices = new List<Vector3>(outline.Count * 16);
            var normals = new List<Vector3>(outline.Count * 16);
            var uvs = new List<Vector2>(outline.Count * 16);
            var triangles = new List<int>(outline.Count * 24);
            var frontCenter = new Vector3(0f, (archBaseY - halfHeight) * 0.25f, -halfDepth);
            var backCenter = new Vector3(0f, frontCenter.y, halfDepth);
            for (var index = 0; index < outline.Count; index += 1)
            {
                var next = (index + 1) % outline.Count;
                var currentPoint = outline[index];
                var nextPoint = outline[next];
                var frontCurrent = new Vector3(currentPoint.x, currentPoint.y, -halfDepth);
                var frontNext = new Vector3(nextPoint.x, nextPoint.y, -halfDepth);
                var backCurrent = new Vector3(currentPoint.x, currentPoint.y, halfDepth);
                var backNext = new Vector3(nextPoint.x, nextPoint.y, halfDepth);

                AddMeshTriangle(vertices, normals, uvs, triangles, frontCenter, frontCurrent, frontNext, Vector3.back);
                AddMeshTriangle(vertices, normals, uvs, triangles, backCenter, backNext, backCurrent, Vector3.forward);

                var edge = nextPoint - currentPoint;
                var sideNormal = new Vector3(edge.y, -edge.x, 0f).normalized;
                AddMeshQuad(vertices, normals, uvs, triangles, frontCurrent, backCurrent, backNext, frontNext, sideNormal);
            }

            var mesh = new Mesh
            {
                name = $"{meshName} arched mesh",
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                uv = uvs.ToArray(),
                triangles = triangles.ToArray(),
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject CreateTaperedSpeakerCabinet(
            Transform parent,
            string objectName,
            Vector2 frontSize,
            Vector2 rearSize,
            float depth,
            float cornerChamfer,
            Vector3 localPosition,
            Material material,
            bool castShadows = true,
            bool receiveShadows = true,
            bool staticGeometry = true)
        {
            var cabinet = new GameObject(objectName);
            cabinet.transform.SetParent(parent, false);
            cabinet.transform.localPosition = localPosition;
            cabinet.AddComponent<MeshFilter>().sharedMesh = CreateTaperedSpeakerCabinetMesh(
                objectName,
                frontSize,
                rearSize,
                depth,
                cornerChamfer);
            var renderer = cabinet.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material, castShadows, receiveShadows);
            cabinet.isStatic = staticGeometry;
            return cabinet;
        }

        private static Mesh CreateTaperedSpeakerCabinetMesh(
            string meshName,
            Vector2 frontSize,
            Vector2 rearSize,
            float depth,
            float cornerChamfer)
        {
            var safeFrontSize = new Vector2(Mathf.Max(0.01f, frontSize.x), Mathf.Max(0.01f, frontSize.y));
            var safeRearSize = new Vector2(Mathf.Max(0.01f, rearSize.x), Mathf.Max(0.01f, rearSize.y));
            var safeDepth = Mathf.Max(0.01f, depth);
            var frontRing = CreateChamferedRectangleRing(safeFrontSize, cornerChamfer, -safeDepth * 0.5f);
            var rearRing = CreateChamferedRectangleRing(safeRearSize, cornerChamfer * 0.72f, safeDepth * 0.5f);

            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var uvs = new List<Vector2>(96);
            var triangles = new List<int>(48);
            for (var index = 0; index < frontRing.Length; index += 1)
            {
                var next = (index + 1) % frontRing.Length;
                AddMeshTriangle(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    new Vector3(0f, 0f, frontRing[index].z),
                    frontRing[index],
                    frontRing[next],
                    Vector3.back);
                AddMeshTriangle(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    new Vector3(0f, 0f, rearRing[index].z),
                    rearRing[next],
                    rearRing[index],
                    Vector3.forward);

                var sideNormal = Vector3.Cross(
                    rearRing[index] - frontRing[index],
                    rearRing[next] - frontRing[index]).normalized;
                AddMeshQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    frontRing[index],
                    rearRing[index],
                    rearRing[next],
                    frontRing[next],
                    sideNormal);
            }

            var mesh = new Mesh
            {
                name = $"{meshName} mesh",
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                uv = uvs.ToArray(),
                triangles = triangles.ToArray(),
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3[] CreateChamferedRectangleRing(Vector2 size, float requestedChamfer, float z)
        {
            var halfWidth = size.x * 0.5f;
            var halfHeight = size.y * 0.5f;
            var chamfer = Mathf.Clamp(requestedChamfer, 0f, Mathf.Min(halfWidth, halfHeight) * 0.8f);
            return new[]
            {
                new Vector3(-halfWidth + chamfer, halfHeight, z),
                new Vector3(halfWidth - chamfer, halfHeight, z),
                new Vector3(halfWidth, halfHeight - chamfer, z),
                new Vector3(halfWidth, -halfHeight + chamfer, z),
                new Vector3(halfWidth - chamfer, -halfHeight, z),
                new Vector3(-halfWidth + chamfer, -halfHeight, z),
                new Vector3(-halfWidth, -halfHeight + chamfer, z),
                new Vector3(-halfWidth, halfHeight - chamfer, z),
            };
        }

        private static GameObject CreateCrescent(
            Transform parent,
            string objectName,
            float outerRadius,
            float cutoutRadius,
            float cutoutOffset,
            float depth,
            Vector3 localPosition,
            float openingDirection,
            Material material,
            bool castShadows = true,
            bool receiveShadows = true,
            bool staticGeometry = true)
        {
            var crescent = new GameObject(objectName);
            crescent.transform.SetParent(parent, false);
            crescent.transform.localPosition = localPosition;
            crescent.AddComponent<MeshFilter>().sharedMesh = CreateCrescentMesh(
                objectName,
                outerRadius,
                cutoutRadius,
                cutoutOffset,
                depth,
                openingDirection);
            var renderer = crescent.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material, castShadows, receiveShadows);
            crescent.isStatic = staticGeometry;
            return crescent;
        }

        private static Mesh CreateCrescentMesh(
            string meshName,
            float outerRadius,
            float cutoutRadius,
            float cutoutOffset,
            float depth,
            float openingDirection)
        {
            const int segmentCount = 24;
            var safeOuterRadius = Mathf.Max(outerRadius, 0.001f);
            var safeCutoutRadius = Mathf.Max(cutoutRadius, 0.001f);
            var minimumOffset = Mathf.Abs(safeOuterRadius - safeCutoutRadius) + 0.0001f;
            var maximumOffset = safeOuterRadius + safeCutoutRadius - 0.0001f;
            var safeOffset = Mathf.Clamp(Mathf.Abs(cutoutOffset), minimumOffset, maximumOffset);
            var safeDepth = Mathf.Max(depth, 0.001f);
            var direction = openingDirection < 0f ? -1f : 1f;
            var intersectionX =
                (safeOuterRadius * safeOuterRadius - safeCutoutRadius * safeCutoutRadius + safeOffset * safeOffset) /
                (2f * safeOffset);
            var intersectionY = Mathf.Sqrt(Mathf.Max(
                0f,
                safeOuterRadius * safeOuterRadius - intersectionX * intersectionX));
            var halfDepth = safeDepth * 0.5f;

            var outerPoints = new Vector2[segmentCount + 1];
            var innerPoints = new Vector2[segmentCount + 1];
            for (var index = 0; index <= segmentCount; index += 1)
            {
                var y = Mathf.Lerp(-intersectionY, intersectionY, index / (float)segmentCount);
                var outerX = -Mathf.Sqrt(Mathf.Max(0f, safeOuterRadius * safeOuterRadius - y * y));
                var innerX = safeOffset - Mathf.Sqrt(Mathf.Max(0f, safeCutoutRadius * safeCutoutRadius - y * y));
                outerPoints[index] = new Vector2(outerX * direction, y);
                innerPoints[index] = new Vector2(innerX * direction, y);
            }

            var vertices = new List<Vector3>(segmentCount * 16);
            var normals = new List<Vector3>(segmentCount * 16);
            var uvs = new List<Vector2>(segmentCount * 16);
            var triangles = new List<int>(segmentCount * 24);
            var cutoutCenter = new Vector2(safeOffset * direction, 0f);

            for (var index = 0; index < segmentCount; index += 1)
            {
                var outerBottom = outerPoints[index];
                var outerTop = outerPoints[index + 1];
                var innerBottom = innerPoints[index];
                var innerTop = innerPoints[index + 1];

                AddMeshQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    new Vector3(outerBottom.x, outerBottom.y, -halfDepth),
                    new Vector3(innerBottom.x, innerBottom.y, -halfDepth),
                    new Vector3(innerTop.x, innerTop.y, -halfDepth),
                    new Vector3(outerTop.x, outerTop.y, -halfDepth),
                    Vector3.back);
                AddMeshQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    new Vector3(outerBottom.x, outerBottom.y, halfDepth),
                    new Vector3(outerTop.x, outerTop.y, halfDepth),
                    new Vector3(innerTop.x, innerTop.y, halfDepth),
                    new Vector3(innerBottom.x, innerBottom.y, halfDepth),
                    Vector3.forward);

                var outerNormal2D = ((outerBottom + outerTop) * 0.5f).normalized;
                AddMeshQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    new Vector3(outerBottom.x, outerBottom.y, -halfDepth),
                    new Vector3(outerTop.x, outerTop.y, -halfDepth),
                    new Vector3(outerTop.x, outerTop.y, halfDepth),
                    new Vector3(outerBottom.x, outerBottom.y, halfDepth),
                    new Vector3(outerNormal2D.x, outerNormal2D.y, 0f));

                var innerMidpoint = (innerBottom + innerTop) * 0.5f;
                var innerNormal2D = (cutoutCenter - innerMidpoint).normalized;
                AddMeshQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    new Vector3(innerBottom.x, innerBottom.y, -halfDepth),
                    new Vector3(innerBottom.x, innerBottom.y, halfDepth),
                    new Vector3(innerTop.x, innerTop.y, halfDepth),
                    new Vector3(innerTop.x, innerTop.y, -halfDepth),
                    new Vector3(innerNormal2D.x, innerNormal2D.y, 0f));
            }

            var mesh = new Mesh
            {
                name = $"{meshName} mesh",
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                uv = uvs.ToArray(),
                triangles = triangles.ToArray(),
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddMeshTriangle(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 normal)
        {
            var vertexOffset = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            for (var index = 0; index < 3; index += 1)
            {
                normals.Add(normal);
            }

            uvs.Add(new Vector2(0.5f, 0.5f));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));

            var windingNormal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(windingNormal, normal) >= 0f)
            {
                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 1);
                triangles.Add(vertexOffset + 2);
            }
            else
            {
                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 2);
                triangles.Add(vertexOffset + 1);
            }
        }

        private static void AddMeshQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 normal)
        {
            var vertexOffset = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            for (var index = 0; index < 4; index += 1)
            {
                normals.Add(normal);
            }

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            var windingNormal = Vector3.Cross(b - a, c - a);
            if (windingNormal.sqrMagnitude <= 0.0000001f)
            {
                windingNormal = Vector3.Cross(c - a, d - a);
            }

            if (Vector3.Dot(windingNormal, normal) >= 0f)
            {
                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 1);
                triangles.Add(vertexOffset + 2);
                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 2);
                triangles.Add(vertexOffset + 3);
            }
            else
            {
                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 2);
                triangles.Add(vertexOffset + 1);
                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 3);
                triangles.Add(vertexOffset + 2);
            }
        }

        private static GameObject CreateBeveledBox(
            Transform parent,
            string objectName,
            Vector3 size,
            Vector3 localPosition,
            Material material,
            float bevel,
            bool castShadows = true,
            bool receiveShadows = true,
            bool staticGeometry = true)
        {
            var box = new GameObject(objectName);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;

            var mesh = CreateBeveledBoxMesh(objectName, size, bevel);
            box.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = box.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material, castShadows, receiveShadows);
            box.isStatic = staticGeometry;
            return box;
        }

        private static GameObject CreateTrimBox(
            Transform parent,
            string objectName,
            Vector3 size,
            Vector3 localPosition,
            Material material,
            float requestedBevel,
            bool castShadows = true,
            bool receiveShadows = true,
            bool staticGeometry = true)
        {
            var minimumSize = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            var stableBevel = Mathf.Min(requestedBevel, minimumSize * MaximumTrimBevelRatio);
            return CreateBeveledBox(
                parent,
                objectName,
                size,
                localPosition,
                material,
                stableBevel,
                castShadows,
                receiveShadows,
                staticGeometry);
        }

        private static Mesh CreateBeveledBoxMesh(string meshName, Vector3 size, float bevel)
        {
            var half = size * 0.5f;
            var radius = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.92f);
            var xCoordinates = CreateBevelCoordinates(half.x, radius);
            var yCoordinates = CreateBevelCoordinates(half.y, radius);
            var zCoordinates = CreateBevelCoordinates(half.z, radius);

            var vertices = new List<Vector3>(192);
            var normals = new List<Vector3>(192);
            var uvs = new List<Vector2>(192);
            var triangles = new List<int>(324);

            AddBeveledFace(vertices, normals, uvs, triangles, half, radius, 0, true, yCoordinates, zCoordinates, false);
            AddBeveledFace(vertices, normals, uvs, triangles, half, radius, 0, false, yCoordinates, zCoordinates, true);
            AddBeveledFace(vertices, normals, uvs, triangles, half, radius, 1, true, xCoordinates, zCoordinates, true);
            AddBeveledFace(vertices, normals, uvs, triangles, half, radius, 1, false, xCoordinates, zCoordinates, false);
            AddBeveledFace(vertices, normals, uvs, triangles, half, radius, 2, true, xCoordinates, yCoordinates, false);
            AddBeveledFace(vertices, normals, uvs, triangles, half, radius, 2, false, xCoordinates, yCoordinates, true);

            var mesh = new Mesh
            {
                name = $"{meshName} beveled mesh",
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                uv = uvs.ToArray(),
                triangles = triangles.ToArray(),
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float[] CreateBevelCoordinates(float halfExtent, float radius)
        {
            if (radius <= 0.0001f || halfExtent <= radius)
            {
                return new[] { -halfExtent, halfExtent };
            }

            return new[] { -halfExtent, -halfExtent + radius, halfExtent - radius, halfExtent };
        }

        private static void AddBeveledFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 half,
            float radius,
            int fixedAxis,
            bool positive,
            float[] uCoordinates,
            float[] vCoordinates,
            bool reverseWinding)
        {
            var vertexOffset = vertices.Count;
            for (var u = 0; u < uCoordinates.Length; u += 1)
            {
                for (var v = 0; v < vCoordinates.Length; v += 1)
                {
                    var point = Vector3.zero;
                    point[fixedAxis] = positive ? half[fixedAxis] : -half[fixedAxis];
                    var uAxis = fixedAxis == 0 ? 1 : 0;
                    var vAxis = fixedAxis == 2 ? 1 : 2;
                    point[uAxis] = uCoordinates[u];
                    point[vAxis] = vCoordinates[v];

                    var inner = new Vector3(
                        Mathf.Clamp(point.x, -half.x + radius, half.x - radius),
                        Mathf.Clamp(point.y, -half.y + radius, half.y - radius),
                        Mathf.Clamp(point.z, -half.z + radius, half.z - radius));
                    var offset = point - inner;
                    var normal = offset.sqrMagnitude > 0.000001f
                        ? offset.normalized
                        : AxisVector(fixedAxis, positive);

                    vertices.Add(inner + normal * radius);
                    normals.Add(normal);
                    uvs.Add(new Vector2(
                        uCoordinates.Length > 1 ? u / (float)(uCoordinates.Length - 1) : 0f,
                        vCoordinates.Length > 1 ? v / (float)(vCoordinates.Length - 1) : 0f));
                }
            }

            for (var u = 0; u < uCoordinates.Length - 1; u += 1)
            {
                for (var v = 0; v < vCoordinates.Length - 1; v += 1)
                {
                    var a = vertexOffset + u * vCoordinates.Length + v;
                    var b = vertexOffset + (u + 1) * vCoordinates.Length + v;
                    var c = vertexOffset + (u + 1) * vCoordinates.Length + v + 1;
                    var d = vertexOffset + u * vCoordinates.Length + v + 1;

                    if (reverseWinding)
                    {
                        triangles.Add(a);
                        triangles.Add(c);
                        triangles.Add(b);
                        triangles.Add(a);
                        triangles.Add(d);
                        triangles.Add(c);
                    }
                    else
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                        triangles.Add(a);
                        triangles.Add(c);
                        triangles.Add(d);
                    }
                }
            }
        }

        private static Vector3 AxisVector(int axis, bool positive)
        {
            var vector = Vector3.zero;
            vector[axis] = positive ? 1f : -1f;
            return vector;
        }

        private static GameObject ConfigurePrimitive(
            GameObject primitive,
            Transform parent,
            string objectName,
            Vector3 size,
            Vector3 localPosition,
            Material material,
            bool castShadows,
            bool receiveShadows,
            bool staticGeometry)
        {
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = size;
            primitive.isStatic = staticGeometry;

            var collider = primitive.GetComponent<Collider>();
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

            if (primitive.TryGetComponent<MeshRenderer>(out var renderer))
            {
                ConfigureRenderer(renderer, material, castShadows, receiveShadows);
            }

            return primitive;
        }

        private static void ConfigureRenderer(MeshRenderer renderer, Material material, bool castShadows, bool receiveShadows)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
        }

        private void CreateAnchor(string anchorName, Vector3 position, Quaternion rotation)
        {
            var anchor = anchorsRoot.Find(anchorName);
            if (anchor == null)
            {
                anchor = new GameObject(anchorName).transform;
                anchor.SetParent(anchorsRoot, false);
            }

            anchor.position = position;
            anchor.rotation = rotation;
        }

        private static Light FindOrCreateLight(string objectName, LightType type)
        {
            var existing = GameObject.Find(objectName);
            var lightObject = existing != null ? existing : new GameObject(objectName);
            var light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = lightObject.AddComponent<Light>();
            }

            light.type = type;
            return light;
        }

        private static Light FindLight(string objectName)
        {
            var lightObject = GameObject.Find(objectName);
            return lightObject != null ? lightObject.GetComponent<Light>() : null;
        }

        private static Color Rgb(uint hex, float alpha = 1f)
        {
            return new Color(
                ((hex >> 16) & 0xff) / 255f,
                ((hex >> 8) & 0xff) / 255f,
                (hex & 0xff) / 255f,
                alpha);
        }

        private static void SetEmission(Material material, Color color, float intensity)
        {
            QuestStylizedMaterial.SetEmission(material, color, intensity);
        }

        /// <summary>
        /// Central palette for the room. Geometry code only references named materials,
        /// so a future room theme is a palette swap rather than a geometry rewrite.
        /// </summary>
        private sealed class RoomPalette
        {
            public static Color GetAccentColor(RoomTheme theme)
            {
                return theme == RoomTheme.Bright ? Rgb(0xB89E6D) : Rgb(0xD8C49D);
            }

            public static Color GetWarmColor(RoomTheme theme)
            {
                return theme == RoomTheme.Bright ? Rgb(0xDCC8A3) : Rgb(0xE3CFA8);
            }

            public Material Floor;
            public Material FloorGroove;
            public Material Ceiling;
            public Material CeilingInset;
            public Material Wall;
            public Material WallReveal;
            public Material PaddedWall;
            public Material WinePanel;
            public Material Wood;
            public Material Trim;
            public Material FocalTrim;
            public Material Rug;
            public Material Sofa;
            public Material SofaHighlight;
            public Material SofaShadow;
            public Material Pillow;
            public Material Table;
            public Material Stone;
            public Material Glass;
            public Material SpeakerCabinet;
            public Material SpeakerGrille;
            public Material SpeakerDetail;
            public Material SpeakerHardware;
            public Material Accent;
            public Material Warm;
            public Material Neon;
            public Material Moon;
            public Material CeilingMoon;
            public Material Stars;
            public Material Aurora;
            public Material Foliage;
            public Material Ceramic;
            public Material Citrus;
            public Material Stage;
            public Material ScreenFrame;
            public Material ScreenBezel;
            public Material LevelTrack;

            public readonly struct ThemedColor
            {
                public ThemedColor(Color color, float emissionIntensity = 0f)
                {
                    Color = color;
                    EmissionIntensity = emissionIntensity;
                }

                public Color Color { get; }
                public float EmissionIntensity { get; }
            }

            /// <summary>
            /// Theme colors keyed by material name. Both room construction and the
            /// runtime theme retint read from this single source of truth.
            /// </summary>
            public static Dictionary<string, ThemedColor> GetThemeColors(RoomTheme theme)
            {
                if (theme == RoomTheme.Bright)
                {
                    return new Dictionary<string, ThemedColor>
                    {
                        ["V0.5 Floor"] = new ThemedColor(Rgb(0x81725E)),
                        ["V0.5 Floor Groove"] = new ThemedColor(Rgb(0x5C5144)),
                        ["V0.5 Ceiling"] = new ThemedColor(Rgb(0xE4E4E2)),
                        ["V0.5 Ceiling Inset"] = new ThemedColor(Rgb(0xD1D2D4)),
                        ["V0.5 Wall"] = new ThemedColor(Rgb(0xDCDBD7)),
                        ["V0.5 Wall Reveal"] = new ThemedColor(Rgb(0x77767A)),
                        ["V0.5 Padded Wall"] = new ThemedColor(Rgb(0xC8C9CC)),
                        ["V0.5 Wine Panel"] = new ThemedColor(Rgb(0xA98482)),
                        ["V0.5 Walnut"] = new ThemedColor(Rgb(0x725A49)),
                        ["V0.5 Brushed Brass"] = new ThemedColor(Rgb(0x827666)),
                        ["V0.5 Focal Brass"] = new ThemedColor(Rgb(0x9F8357)),
                        ["V0.5 Rug"] = new ThemedColor(Rgb(0xB9B7B0)),
                        ["V0.5 Velvet Sofa"] = new ThemedColor(Rgb(0xC8B4B1)),
                        ["V0.5 Velvet Highlight"] = new ThemedColor(Rgb(0xD9C9C7)),
                        ["V0.5 Sofa Shadow"] = new ThemedColor(Rgb(0x8A7775)),
                        ["V0.5 Accent Pillow"] = new ThemedColor(Rgb(0xB89D78)),
                        ["V0.5 Table"] = new ThemedColor(Rgb(0xA99B87)),
                        ["V0.5 Moonstone"] = new ThemedColor(Rgb(0xC4C1C3)),
                        ["V0.5 Smoked Glass"] = new ThemedColor(Rgb(0xD8DCE2, 0.45f)),
                        ["V0.5 Speaker Cabinet"] = new ThemedColor(Rgb(0x25282D)),
                        ["V0.5 Speaker Grille"] = new ThemedColor(Rgb(0x0D0F12)),
                        ["V0.5 Speaker Detail"] = new ThemedColor(Rgb(0x292D33)),
                        ["V0.5 Speaker Hardware"] = new ThemedColor(Rgb(0x3A3D42)),
                        ["V0.5 Accent"] = new ThemedColor(GetAccentColor(theme), 0.55f),
                        ["V0.5 Warm Light"] = new ThemedColor(GetWarmColor(theme), WarmLightBaseEmission),
                        ["V0.5 Neon"] = new ThemedColor(GetAccentColor(theme), 0.5f),
                        ["V0.5 Moon"] = new ThemedColor(Rgb(0xEEE2CC), 0.45f),
                        ["V0.5 Ceiling Moon"] = new ThemedColor(Rgb(0xF1E6CF), 0.2f),
                        ["V0.5 Ceiling Stars"] = new ThemedColor(Rgb(0xB9DCFF)),
                        ["V0.5 Ceiling Aurora"] = new ThemedColor(Rgb(0x55D8C9)),
                        ["V0.5 Foliage"] = new ThemedColor(Rgb(0x75816D)),
                        ["V0.5 Ceramic"] = new ThemedColor(Rgb(0xEEECE7)),
                        ["V0.5 Citrus"] = new ThemedColor(Rgb(0xB89455)),
                        ["V0.5 Stage"] = new ThemedColor(Rgb(0x80715E)),
                        ["V0.5 Screen Frame"] = new ThemedColor(Rgb(0x2B2D31)),
                        ["V0.5 Screen Bezel"] = new ThemedColor(Rgb(0x4A4C50)),
                        ["V0.5 Level Track"] = new ThemedColor(Rgb(0xB9B5AD), 0.18f),
                    };
                }

                return new Dictionary<string, ThemedColor>
                {
                    ["V0.5 Floor"] = new ThemedColor(Rgb(0x2A241F)),
                    ["V0.5 Floor Groove"] = new ThemedColor(Rgb(0x151312)),
                    ["V0.5 Ceiling"] = new ThemedColor(Rgb(0x1B1E26)),
                    ["V0.5 Ceiling Inset"] = new ThemedColor(Rgb(0x10141C)),
                    ["V0.5 Wall"] = new ThemedColor(Rgb(0x242B38)),
                    ["V0.5 Wall Reveal"] = new ThemedColor(Rgb(0x0C0E14)),
                    ["V0.5 Padded Wall"] = new ThemedColor(Rgb(0x343B4B)),
                    ["V0.5 Wine Panel"] = new ThemedColor(Rgb(0x4A2F40)),
                    ["V0.5 Walnut"] = new ThemedColor(Rgb(0x352820)),
                    ["V0.5 Brushed Brass"] = new ThemedColor(Rgb(0x927E68)),
                    ["V0.5 Focal Brass"] = new ThemedColor(Rgb(0xC3A46B)),
                    ["V0.5 Rug"] = new ThemedColor(Rgb(0x373740)),
                    ["V0.5 Velvet Sofa"] = new ThemedColor(Rgb(0x4A3741)),
                    ["V0.5 Velvet Highlight"] = new ThemedColor(Rgb(0x5A444E)),
                    ["V0.5 Sofa Shadow"] = new ThemedColor(Rgb(0x251C22)),
                    ["V0.5 Accent Pillow"] = new ThemedColor(Rgb(0x967853)),
                    ["V0.5 Table"] = new ThemedColor(Rgb(0x252932)),
                    ["V0.5 Moonstone"] = new ThemedColor(Rgb(0x68646D)),
                    ["V0.5 Smoked Glass"] = new ThemedColor(Rgb(0x202834, 0.68f)),
                    ["V0.5 Speaker Cabinet"] = new ThemedColor(Rgb(0x111318)),
                    ["V0.5 Speaker Grille"] = new ThemedColor(Rgb(0x050608)),
                    ["V0.5 Speaker Detail"] = new ThemedColor(Rgb(0x1C2026)),
                    ["V0.5 Speaker Hardware"] = new ThemedColor(Rgb(0x30343B)),
                    ["V0.5 Accent"] = new ThemedColor(GetAccentColor(theme), 0.55f),
                    ["V0.5 Warm Light"] = new ThemedColor(GetWarmColor(theme), WarmLightBaseEmission),
                    ["V0.5 Neon"] = new ThemedColor(GetAccentColor(theme), 0.5f),
                    ["V0.5 Moon"] = new ThemedColor(Rgb(0xEDE2CC), 0.62f),
                    ["V0.5 Ceiling Moon"] = new ThemedColor(Rgb(0xE9D8B9), 0.22f),
                    ["V0.5 Ceiling Stars"] = new ThemedColor(Rgb(0x91C9FF)),
                    ["V0.5 Ceiling Aurora"] = new ThemedColor(Rgb(0x4FD5C2)),
                    ["V0.5 Foliage"] = new ThemedColor(Rgb(0x1D2A22), 0.08f),
                    ["V0.5 Ceramic"] = new ThemedColor(Rgb(0xB8B2A5)),
                    ["V0.5 Citrus"] = new ThemedColor(Rgb(0x9B7B45)),
                    ["V0.5 Stage"] = new ThemedColor(Rgb(0x3A2E27)),
                    ["V0.5 Screen Frame"] = new ThemedColor(Rgb(0x020305)),
                    ["V0.5 Screen Bezel"] = new ThemedColor(Rgb(0x12151A)),
                    ["V0.5 Level Track"] = new ThemedColor(Rgb(0x26282D), 0.18f),
                };
            }

            public static RoomPalette Create(RoomTheme theme)
            {
                var colors = GetThemeColors(theme);
                var palette = new RoomPalette
                {
                    Floor = CreateMaterial("V0.5 Floor", colors, 0.28f, 0.02f),
                    FloorGroove = CreateMaterial("V0.5 Floor Groove", colors, 0.08f, 0f),
                    Ceiling = CreateMaterial("V0.5 Ceiling", colors, 0.16f, 0f),
                    CeilingInset = CreateMaterial("V0.5 Ceiling Inset", colors, 0.22f, 0f),
                    Wall = CreateMaterial("V0.5 Wall", colors, 0.12f, 0f),
                    WallReveal = CreateMaterial("V0.5 Wall Reveal", colors, 0.08f, 0f),
                    PaddedWall = CreateMaterial("V0.5 Padded Wall", colors, 0.08f, 0f),
                    WinePanel = CreateMaterial("V0.5 Wine Panel", colors, 0.1f, 0f),
                    Wood = CreateMaterial("V0.5 Walnut", colors, 0.32f, 0.04f),
                    Trim = CreateMaterial("V0.5 Brushed Brass", colors, 0.42f, 0.58f),
                    FocalTrim = CreateMaterial("V0.5 Focal Brass", colors, 0.52f, 0.65f),
                    Rug = CreateMaterial("V0.5 Rug", colors, 0.04f, 0f),
                    Sofa = CreateMaterial("V0.5 Velvet Sofa", colors, 0.18f, 0f),
                    SofaHighlight = CreateMaterial("V0.5 Velvet Highlight", colors, 0.2f, 0f),
                    SofaShadow = CreateMaterial("V0.5 Sofa Shadow", colors, 0.1f, 0f),
                    Pillow = CreateMaterial("V0.5 Accent Pillow", colors, 0.2f, 0f),
                    Table = CreateMaterial("V0.5 Table", colors, 0.58f, 0.22f),
                    Stone = CreateMaterial("V0.5 Moonstone", colors, 0.7f, 0.08f),
                    Glass = CreateMaterial("V0.5 Smoked Glass", colors, 0.82f, 0.18f, true),
                    SpeakerCabinet = CreateMaterial("V0.5 Speaker Cabinet", colors, 0.24f, 0.08f),
                    SpeakerGrille = CreateMaterial("V0.5 Speaker Grille", colors, 0.06f, 0f),
                    SpeakerDetail = CreateMaterial("V0.5 Speaker Detail", colors, 0.12f, 0.02f),
                    SpeakerHardware = CreateMaterial("V0.5 Speaker Hardware", colors, 0.46f, 0.62f),
                    Accent = CreateMaterial("V0.5 Accent", colors, 0.72f, 0.1f),
                    Warm = CreateMaterial("V0.5 Warm Light", colors, 0.35f, 0f),
                    Neon = CreateMaterial("V0.5 Neon", colors, 0.72f, 0.1f),
                    Moon = CreateMaterial("V0.5 Moon", colors, 0.55f, 0.08f),
                    CeilingMoon = CreateMaterial("V0.5 Ceiling Moon", colors, 0.48f, 0.04f),
                    Stars = CreateCelestialMaterial(
                        "V0.5 Ceiling Stars",
                        colors,
                        Rgb(0xFFE6B7),
                        0f,
                        0.82f,
                        30f,
                        0.92f,
                        0.12f,
                        11.7f,
                        3001),
                    Aurora = CreateCelestialMaterial(
                        "V0.5 Ceiling Aurora",
                        colors,
                        Rgb(0xA88CF5),
                        1f,
                        0.34f,
                        4.6f,
                        0.58f,
                        0.12f,
                        4.2f,
                        3000),
                    Foliage = CreateMaterial("V0.5 Foliage", colors, 0.12f, 0f),
                    Ceramic = CreateMaterial("V0.5 Ceramic", colors, 0.45f, 0.05f),
                    Citrus = CreateMaterial("V0.5 Citrus", colors, 0.38f, 0.04f),
                    Stage = CreateMaterial("V0.5 Stage", colors, 0.28f, 0.04f),
                    ScreenFrame = CreateMaterial("V0.5 Screen Frame", colors, 0.7f, 0.22f),
                    ScreenBezel = CreateMaterial("V0.5 Screen Bezel", colors, 0.18f, 0.02f),
                    LevelTrack = CreateMaterial("V0.5 Level Track", colors, 0.16f, 0f),
                };

                ConfigureMaterialDetails(palette);
                return palette;
            }

            private static void ConfigureMaterialDetails(RoomPalette palette)
            {
                QuestStylizedMaterial.ConfigureDetail(palette.Floor, QuestMaterialDetailMode.Wood, 2.8f, 0.14f, 0.08f);
                QuestStylizedMaterial.ConfigureDetail(palette.Ceiling, QuestMaterialDetailMode.Plaster, 5.5f, 0.035f, 0.05f);
                QuestStylizedMaterial.ConfigureDetail(palette.CeilingInset, QuestMaterialDetailMode.Plaster, 6.5f, 0.04f, 0.04f);
                QuestStylizedMaterial.ConfigureDetail(palette.Wall, QuestMaterialDetailMode.Plaster, 5f, 0.055f, 0.12f);
                QuestStylizedMaterial.ConfigureDetail(palette.PaddedWall, QuestMaterialDetailMode.Fabric, 38f, 0.045f, 0.1f);
                QuestStylizedMaterial.ConfigureDetail(palette.WinePanel, QuestMaterialDetailMode.Fabric, 42f, 0.05f, 0.12f);
                QuestStylizedMaterial.ConfigureDetail(palette.Wood, QuestMaterialDetailMode.Wood, 3.6f, 0.12f, 0.08f);
                QuestStylizedMaterial.ConfigureDetail(palette.Trim, QuestMaterialDetailMode.BrushedMetal, 42f, 0.09f, 0.08f);
                QuestStylizedMaterial.ConfigureDetail(palette.FocalTrim, QuestMaterialDetailMode.BrushedMetal, 48f, 0.08f, 0.1f);
                QuestStylizedMaterial.ConfigureDetail(palette.Rug, QuestMaterialDetailMode.Fabric, 52f, 0.07f);
                QuestStylizedMaterial.ConfigureDetail(palette.Sofa, QuestMaterialDetailMode.Fabric, 44f, 0.055f, 0.08f);
                QuestStylizedMaterial.ConfigureDetail(palette.SofaHighlight, QuestMaterialDetailMode.Fabric, 44f, 0.05f, 0.08f);
                QuestStylizedMaterial.ConfigureDetail(palette.Pillow, QuestMaterialDetailMode.Fabric, 48f, 0.06f, 0.1f);
                QuestStylizedMaterial.ConfigureDetail(palette.Stone, QuestMaterialDetailMode.Stone, 2.6f, 0.11f, 0.06f);
                QuestStylizedMaterial.ConfigureDetail(palette.SpeakerGrille, QuestMaterialDetailMode.Perforated, 28f, 0.24f);
                QuestStylizedMaterial.ConfigureDetail(palette.SpeakerHardware, QuestMaterialDetailMode.BrushedMetal, 52f, 0.08f);

                QuestStylizedMaterial.ConfigureLighting(palette.Wall, 0.42f, 1.02f, 0.018f);
                QuestStylizedMaterial.ConfigureLighting(palette.PaddedWall, 0.4f, 0.98f, 0.022f);
                QuestStylizedMaterial.ConfigureLighting(palette.WinePanel, 0.4f, 0.98f, 0.024f);
                QuestStylizedMaterial.ConfigureLighting(palette.Sofa, 0.42f, 0.98f, 0.028f);
                QuestStylizedMaterial.ConfigureLighting(palette.SofaHighlight, 0.42f, 0.98f, 0.03f);
                QuestStylizedMaterial.ConfigureLighting(palette.Stone, 0.42f, 1f, 0.04f);
            }

            private static Material CreateMaterial(
                string materialName,
                Dictionary<string, ThemedColor> colors,
                float smoothness,
                float metallic,
                bool transparent = false)
            {
                var themedColor = colors[materialName];
                return CreateMaterial(materialName, themedColor.Color, smoothness, metallic, themedColor.EmissionIntensity, transparent);
            }

            private static Material CreateMaterial(
                string materialName,
                Color color,
                float smoothness,
                float metallic,
                float emissionIntensity = 0f,
                bool transparent = false)
            {
                return QuestStylizedMaterial.CreateLit(
                    materialName,
                    color,
                    1f - smoothness,
                    metallic,
                    emissionIntensity,
                    transparent);
            }

            private static Material CreateCelestialMaterial(
                string materialName,
                Dictionary<string, ThemedColor> colors,
                Color secondaryColor,
                float effectMode,
                float intensity,
                float scale,
                float speed,
                float density,
                float seed,
                int renderQueue)
            {
                var shader = Shader.Find("TsukiVox/Quest Celestial Ceiling");
                if (shader == null)
                {
                    Debug.LogError("[TsukiVox Room] Quest Celestial Ceiling shader is unavailable.");
                    return CreateMaterial(materialName, colors, 0.45f, 0f, true);
                }

                var material = new Material(shader)
                {
                    name = materialName,
                    renderQueue = renderQueue,
                    enableInstancing = true,
                };
                material.SetColor("_BaseColor", colors[materialName].Color);
                material.SetColor("_SecondaryColor", secondaryColor);
                material.SetFloat("_EffectMode", effectMode);
                material.SetFloat("_Intensity", intensity);
                material.SetFloat("_Scale", scale);
                material.SetFloat("_Speed", speed);
                material.SetFloat("_Density", density);
                material.SetFloat("_Seed", seed);
                return material;
            }
        }
    }
}
