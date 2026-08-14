using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TsukiVox.AudioPrototype
{
    /// <summary>
    /// Room-wide color theme. Dark keeps the original V0.5 night-lounge palette,
    /// Bright swaps every palette color and light setting to a daylight lounge look
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
        public const int CurrentDesignRevision = 19;
        public const string ThemePrefsKey = "TsukiVox.RoomTheme";

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

        private const string GeometryRootName = "Geometry";
        private const string FeedbackRootName = "Feedback";
        private const string AnchorsRootName = "Anchors";

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
        private float smoothedLevel;
        private RoomTheme currentTheme = RoomTheme.Dark;

        public bool NeedsDesignRefresh => generatedDesignRevision < CurrentDesignRevision;

        public RoomTheme CurrentTheme => currentTheme;

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
            EnsureRoots();
            DetachControlCanvasFromGeneratedGeometry();
            RemoveLegacySceneProps();
            BuildRoom();
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

        private static RoomTheme LoadPersistedTheme()
        {
            if (!Application.isPlaying)
            {
                return RoomTheme.Dark;
            }

            var stored = PlayerPrefs.GetInt(ThemePrefsKey, (int)RoomTheme.Dark);
            return stored == (int)RoomTheme.Bright ? RoomTheme.Bright : RoomTheme.Dark;
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

                material.color = themedColor.Color;
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

            var palette = RoomPalette.Create(currentTheme);
            levelBarFillMaterial = palette.Accent;
            lightStripMaterial = palette.Warm;

            BuildRoomShell(palette);
            BuildWallDecoration(palette);
            BuildFurniture(palette);
            BuildScreenSurround(palette);
            BuildLevelBars(palette);
            BuildLightStrips(palette);
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
                        palette.Warm);
                    downlightIndex += 1;
                }
            }

            CreateBeveledBox(
                geometryRoot,
                "ceiling medallion",
                new Vector3(1.05f, 0.02f, 1.05f),
                new Vector3(0f, RoomHeight - 0.055f, 1.7f),
                palette.CeilingInset,
                0.04f);
            CreateTrimBox(
                geometryRoot,
                "ceiling medallion front",
                new Vector3(1.12f, 0.03f, 0.04f),
                new Vector3(0f, RoomHeight - 0.05f, 2.24f),
                palette.Trim,
                0.01f);
            CreateTrimBox(
                geometryRoot,
                "ceiling medallion back",
                new Vector3(1.12f, 0.03f, 0.04f),
                new Vector3(0f, RoomHeight - 0.05f, 1.16f),
                palette.Trim,
                0.01f);
            CreateTrimBox(
                geometryRoot,
                "ceiling medallion left",
                new Vector3(0.04f, 0.03f, 1.05f),
                new Vector3(-0.54f, RoomHeight - 0.05f, 1.7f),
                palette.Trim,
                0.01f);
            CreateTrimBox(
                geometryRoot,
                "ceiling medallion right",
                new Vector3(0.04f, 0.03f, 1.05f),
                new Vector3(0.54f, RoomHeight - 0.05f, 1.7f),
                palette.Trim,
                0.01f);
            CreateBeveledBox(
                geometryRoot,
                "ceiling medallion light",
                new Vector3(0.92f, 0.012f, 0.92f),
                new Vector3(0f, RoomHeight - 0.068f, 1.7f),
                palette.Warm,
                0.02f,
                false,
                false,
                false);
        }

        private void BuildWallDecoration(RoomPalette palette)
        {
            const float sidePanelStartZ = -0.55f;
            const float sidePanelSpacing = 0.65f;
            var sidePanelCount = Mathf.FloorToInt((FrontWallZ - 0.9f - sidePanelStartZ) / sidePanelSpacing) + 1;
            for (var index = 0; index < sidePanelCount; index += 1)
            {
                var z = sidePanelStartZ + index * sidePanelSpacing;
                var panelMaterial = index % 3 == 1 ? palette.WinePanel : palette.PaddedWall;
                CreateBeveledBox(geometryRoot, $"left padded panel {index}", new Vector3(0.05f, 1.6f, 0.58f), new Vector3(-RoomWidth * 0.5f + 0.03f, 1.32f, z), panelMaterial, 0.02f);
                CreateBeveledBox(geometryRoot, $"right padded panel {index}", new Vector3(0.05f, 1.6f, 0.58f), new Vector3(RoomWidth * 0.5f - 0.03f, 1.32f, z), panelMaterial, 0.02f);
                CreateTrimBox(geometryRoot, $"left brass divider {index}", new Vector3(0.07f, 1.74f, 0.035f), new Vector3(-RoomWidth * 0.5f + 0.035f, 1.32f, z + 0.325f), palette.Trim, 0.01f);
                CreateTrimBox(geometryRoot, $"right brass divider {index}", new Vector3(0.07f, 1.74f, 0.035f), new Vector3(RoomWidth * 0.5f - 0.035f, 1.32f, z + 0.325f), palette.Trim, 0.01f);
            }

            var sideRailStartZ = BackWallZ + 0.7f;
            var sideRailEndZ = FrontWallZ - 0.55f;
            var sideRailLength = sideRailEndZ - sideRailStartZ;
            var sideRailCenterZ = (sideRailStartZ + sideRailEndZ) * 0.5f;
            CreateTrimBox(geometryRoot, "left wall rail", new Vector3(0.06f, 0.075f, sideRailLength), new Vector3(-RoomWidth * 0.5f + 0.04f, 2.28f, sideRailCenterZ), palette.Trim, 0.018f);
            CreateTrimBox(geometryRoot, "right wall rail", new Vector3(0.06f, 0.075f, sideRailLength), new Vector3(RoomWidth * 0.5f - 0.04f, 2.28f, sideRailCenterZ), palette.Trim, 0.018f);
            CreateTrimBox(geometryRoot, "left wall lower rail", new Vector3(0.06f, 0.09f, sideRailLength), new Vector3(-RoomWidth * 0.5f + 0.04f, 0.38f, sideRailCenterZ), palette.Trim, 0.018f);
            CreateTrimBox(geometryRoot, "right wall lower rail", new Vector3(0.06f, 0.09f, sideRailLength), new Vector3(RoomWidth * 0.5f - 0.04f, 0.38f, sideRailCenterZ), palette.Trim, 0.018f);

            for (var index = 0; index < 5; index += 1)
            {
                var x = -2.4f + index * 1.2f;
                var panelMaterial = index == 2 ? palette.WinePanel : palette.PaddedWall;
                CreateBeveledBox(geometryRoot, $"back padded panel {index}", new Vector3(0.95f, 1.25f, 0.05f), new Vector3(x, 1.1f, BackWallZ + 0.03f), panelMaterial, 0.02f);
            }

            CreateTrimBox(geometryRoot, "back wall top rail", new Vector3(5.9f, 0.07f, 0.065f), new Vector3(0f, 1.82f, BackWallZ + 0.035f), palette.Trim, 0.018f);
            CreateTrimBox(geometryRoot, "back wall low rail", new Vector3(5.9f, 0.085f, 0.065f), new Vector3(0f, 0.43f, BackWallZ + 0.035f), palette.Trim, 0.018f);
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

            CreateBeveledBox(sofaRoot, "plinth", new Vector3(width, 0.16f, depth), new Vector3(0f, 0.08f, 0f), palette.SofaShadow, 0.035f);
            CreateBeveledBox(sofaRoot, "seat", new Vector3(width - 0.12f, 0.26f, depth - 0.1f), new Vector3(0f, 0.31f, 0.02f), palette.Sofa, 0.065f);
            CreateBeveledBox(sofaRoot, "front apron", new Vector3(width, 0.34f, 0.16f), new Vector3(0f, 0.3f, depth * 0.5f - 0.08f), palette.SofaShadow, 0.045f);
            CreateBeveledBox(sofaRoot, "back", new Vector3(width, 0.95f, 0.22f), new Vector3(0f, 0.62f, -depth * 0.5f + 0.11f), palette.Sofa, 0.055f);
            CreateBeveledBox(sofaRoot, "left arm", new Vector3(0.18f, 0.62f, depth - 0.06f), new Vector3(-width * 0.5f + 0.09f, 0.47f, 0f), palette.Sofa, 0.045f);
            CreateBeveledBox(sofaRoot, "right arm", new Vector3(0.18f, 0.62f, depth - 0.06f), new Vector3(width * 0.5f - 0.09f, 0.47f, 0f), palette.Sofa, 0.045f);
            CreateTrimBox(sofaRoot, "front trim", new Vector3(width - 0.3f, 0.035f, 0.03f), new Vector3(0f, 0.2f, depth * 0.5f + 0.015f), palette.Trim, 0.012f);
            CreateBeveledBox(sofaRoot, "underglow", new Vector3(width - 0.34f, 0.025f, 0.035f), new Vector3(0f, 0.12f, depth * 0.5f - 0.1f), palette.Warm, 0.01f);

            var cushionCount = Mathf.Max(2, Mathf.FloorToInt(width / 1.1f));
            var cushionWidth = (width - 0.42f) / cushionCount;
            for (var index = 0; index < cushionCount; index += 1)
            {
                var x = -width * 0.5f + 0.21f + cushionWidth * (index + 0.5f);
                CreateBeveledBox(
                    sofaRoot,
                    $"back cushion {index}",
                    new Vector3(cushionWidth - 0.05f, 0.52f, 0.07f),
                    new Vector3(x, 0.72f, -depth * 0.5f + 0.255f),
                    index % 2 == 0 ? palette.Sofa : palette.WinePanel,
                    0.032f);
            }
        }

        private void CreateCoffeeTable(RoomPalette palette)
        {
            var tableRoot = new GameObject("coffee table").transform;
            tableRoot.SetParent(geometryRoot, false);
            tableRoot.localPosition = new Vector3(0f, 0f, 1.1f);

            // Closed picture-frame rim: the smoked glass tucks into the rails so the
            // frame, glass and legs read as one piece instead of floating strips.
            const float frameOuterX = 2.16f;
            const float frameOuterZ = 1.08f;
            const float railWidth = 0.07f;
            const float railHeight = 0.09f;
            const float frameCenterY = 0.545f;
            var railCenterX = frameOuterX * 0.5f - railWidth * 0.5f;
            var railCenterZ = frameOuterZ * 0.5f - railWidth * 0.5f;

            CreateBeveledBox(tableRoot, "smoked glass top", new Vector3(2.06f, 0.05f, 0.98f), new Vector3(0f, 0.555f, 0f), palette.Glass, 0.02f);
            CreateTrimBox(tableRoot, "table rim front", new Vector3(frameOuterX, railHeight, railWidth), new Vector3(0f, frameCenterY, railCenterZ), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "table rim back", new Vector3(frameOuterX, railHeight, railWidth), new Vector3(0f, frameCenterY, -railCenterZ), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "table rim left", new Vector3(railWidth, railHeight, frameOuterZ - railWidth * 2f), new Vector3(-railCenterX, frameCenterY, 0f), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "table rim right", new Vector3(railWidth, railHeight, frameOuterZ - railWidth * 2f), new Vector3(railCenterX, frameCenterY, 0f), palette.Trim, 0.012f);

            // Lower shelf rests on stretcher rails that tie the four legs together.
            CreateTrimBox(tableRoot, "shelf stretcher front", new Vector3(2.0f, 0.035f, 0.05f), new Vector3(0f, 0.24f, 0.46f), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "shelf stretcher back", new Vector3(2.0f, 0.035f, 0.05f), new Vector3(0f, 0.24f, -0.46f), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "shelf stretcher left", new Vector3(0.05f, 0.035f, 0.87f), new Vector3(-1.0f, 0.24f, 0f), palette.Trim, 0.012f);
            CreateTrimBox(tableRoot, "shelf stretcher right", new Vector3(0.05f, 0.035f, 0.87f), new Vector3(1.0f, 0.24f, 0f), palette.Trim, 0.012f);
            CreateBeveledBox(tableRoot, "table shelf", new Vector3(1.98f, 0.04f, 0.9f), new Vector3(0f, 0.2775f, 0f), palette.Table, 0.015f);

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
                palette.FrontTrim,
                0.006f);
            CreateBeveledBox(geometryRoot, "screen left column", new Vector3(0.24f, 2.6f, 0.08f), new Vector3(-2.85f, 1.35f, FrontWallZ - 0.05f), palette.WinePanel, 0.025f);
            CreateBeveledBox(geometryRoot, "screen right column", new Vector3(0.24f, 2.6f, 0.08f), new Vector3(2.85f, 1.35f, FrontWallZ - 0.05f), palette.WinePanel, 0.025f);

            CreateSpeaker(palette, "left speaker", -2.57f);
            CreateSpeaker(palette, "right speaker", 2.57f);
        }

        private void CreateSpeaker(RoomPalette palette, string objectName, float x)
        {
            var speakerRoot = new GameObject(objectName).transform;
            speakerRoot.SetParent(geometryRoot, false);
            speakerRoot.localPosition = new Vector3(x, 1.45f, FrontWallZ - 0.075f);

            CreateBeveledBox(speakerRoot, "cabinet", new Vector3(0.34f, 1.55f, 0.12f), Vector3.zero, palette.Speaker, 0.025f);
            CreateTrimBox(speakerRoot, "brass header", new Vector3(0.28f, 0.05f, 0.035f), new Vector3(0f, 0.61f, -0.072f), palette.Trim, 0.01f);
            CreateSpeakerDriver(speakerRoot, "upper driver", new Vector3(0f, 0.3f, -0.09f), 0.1f, palette);
            CreateSpeakerDriver(speakerRoot, "lower driver", new Vector3(0f, -0.28f, -0.09f), 0.13f, palette);
        }

        private void CreateSpeakerDriver(Transform parent, string objectName, Vector3 position, float radius, RoomPalette palette)
        {
            var driver = CreateCylinder(parent, objectName, radius, 0.025f, position, palette.SpeakerCone);
            driver.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
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

        private void BuildPerformanceArea(RoomPalette palette)
        {
            const float stageWidth = 1.72f;
            const float stageDepth = 1.08f;
            const float stageHeight = 0.09f;
            const float stageZ = 3.42f;
            const float runnerWidth = 1.12f;
            const float runnerDepth = 1.14f;
            const float runnerZ = 2.48f;

            CreateBeveledBox(
                geometryRoot,
                "stage plinth",
                new Vector3(stageWidth + 0.06f, 0.03f, stageDepth + 0.06f),
                new Vector3(0f, 0.015f, stageZ),
                palette.SofaShadow,
                0.02f);
            CreateBeveledBox(
                geometryRoot,
                "stage deck",
                new Vector3(stageWidth, stageHeight - 0.02f, stageDepth),
                new Vector3(0f, 0.02f + (stageHeight - 0.02f) * 0.5f, stageZ),
                palette.Stage,
                0.025f);
            CreateTrimBox(
                geometryRoot,
                "stage front nosing",
                new Vector3(stageWidth + 0.02f, 0.03f, 0.04f),
                new Vector3(0f, stageHeight - 0.01f, stageZ + stageDepth * 0.5f + 0.005f),
                palette.Trim,
                0.008f);
            CreateTrimBox(
                geometryRoot,
                "stage back nosing",
                new Vector3(stageWidth + 0.02f, 0.03f, 0.04f),
                new Vector3(0f, stageHeight - 0.01f, stageZ - stageDepth * 0.5f - 0.005f),
                palette.Trim,
                0.008f);
            CreateTrimBox(
                geometryRoot,
                "stage left nosing",
                new Vector3(0.04f, 0.03f, stageDepth),
                new Vector3(-stageWidth * 0.5f - 0.005f, stageHeight - 0.01f, stageZ),
                palette.Trim,
                0.008f);
            CreateTrimBox(
                geometryRoot,
                "stage right nosing",
                new Vector3(0.04f, 0.03f, stageDepth),
                new Vector3(stageWidth * 0.5f + 0.005f, stageHeight - 0.01f, stageZ),
                palette.Trim,
                0.008f);

            var ledY = 0.022f;
            var ledX = stageWidth * 0.5f + 0.055f;
            CreateBeveledBox(
                feedbackRoot,
                "stage led front",
                new Vector3(stageWidth + 0.16f, 0.018f, 0.045f),
                new Vector3(0f, ledY, stageZ + stageDepth * 0.5f + 0.055f),
                palette.Warm,
                0.008f,
                false,
                false,
                false);
            CreateBeveledBox(
                feedbackRoot,
                "stage led back",
                new Vector3(stageWidth + 0.16f, 0.018f, 0.045f),
                new Vector3(0f, ledY, stageZ - stageDepth * 0.5f - 0.055f),
                palette.Warm,
                0.008f,
                false,
                false,
                false);
            CreateBeveledBox(
                feedbackRoot,
                "stage led left",
                new Vector3(0.045f, 0.018f, stageDepth + 0.07f),
                new Vector3(-ledX, ledY, stageZ),
                palette.Warm,
                0.008f,
                false,
                false,
                false);
            CreateBeveledBox(
                feedbackRoot,
                "stage led right",
                new Vector3(0.045f, 0.018f, stageDepth + 0.07f),
                new Vector3(ledX, ledY, stageZ),
                palette.Warm,
                0.008f,
                false,
                false,
                false);

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
            CreateCylinder(parent, "staff", 0.008f, 0.34f, new Vector3(0f, 0.57f, 0f), palette.Trim);
            CreateCylinder(parent, "cradle", 0.07f, 0.014f, new Vector3(0f, 0.75f, 0f), palette.Trim);
            CreateSphere(
                parent,
                "moon",
                0.1f,
                new Vector3(0f, 0.9f, 0f),
                palette.Moon,
                false,
                false,
                false);
            var aura = CreateCylinder(
                parent,
                "moon aura",
                0.13f,
                0.01f,
                new Vector3(0f, 0.9f, 0.03f),
                palette.Neon,
                false,
                false,
                false);
            aura.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

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
            RenderSettings.ambientSkyColor = bright ? new Color(0.68f, 0.72f, 0.74f, 1f) : new Color(0.22f, 0.29f, 0.29f, 1f);
            RenderSettings.ambientEquatorColor = bright ? new Color(0.56f, 0.53f, 0.48f, 1f) : new Color(0.09f, 0.075f, 0.065f, 1f);
            RenderSettings.ambientGroundColor = bright ? new Color(0.4f, 0.37f, 0.34f, 1f) : new Color(0.035f, 0.025f, 0.028f, 1f);
            RenderSettings.ambientIntensity = bright ? 1.02f : 0.88f;
            RenderSettings.reflectionIntensity = bright ? 0.85f : 0.7f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = bright ? new Color(0.8f, 0.82f, 0.84f, 1f) : new Color(0.018f, 0.022f, 0.024f, 1f);
            RenderSettings.fogDensity = bright ? 0.005f : 0.012f;

            var keySpot = FindOrCreateLight("V0.5 Key Spot", LightType.Spot);
            keySpot.transform.position = new Vector3(0f, RoomHeight - 0.18f, 1.3f);
            keySpot.transform.rotation = Quaternion.Euler(78f, 0f, 0f);
            keySpot.color = bright ? new Color(1f, 0.97f, 0.9f, 1f) : new Color(1f, 0.83f, 0.64f, 1f);
            keySpot.intensity = bright ? 2.4f : 3.15f;
            keySpot.range = 7.5f;
            keySpot.spotAngle = 82f;
            keySpot.innerSpotAngle = 48f;
            keySpot.shadows = LightShadows.Soft;
            keySpot.shadowResolution = LightShadowResolution.Medium;
            keySpot.shadowStrength = bright ? 0.42f : 0.62f;
            keySpot.shadowBias = 0.035f;
            keySpot.shadowNormalBias = 0.25f;
            keySpot.renderMode = LightRenderMode.ForcePixel;
#if UNITY_EDITOR
            keySpot.lightmapBakeType = LightmapBakeType.Mixed;
#endif

            screenGlow = FindOrCreateLight("V0.5 Screen Glow", LightType.Point);
            screenGlow.transform.position = new Vector3(0f, ScreenPosition.y, ScreenPosition.z - 0.9f);
            screenGlow.color = bright ? new Color(0.8f, 0.93f, 1f, 1f) : new Color(0.62f, 0.9f, 1f, 1f);
            screenGlow.range = 5f;
            screenGlow.shadows = LightShadows.None;
            screenGlow.renderMode = LightRenderMode.ForcePixel;

            loungeGlow = FindOrCreateLight("V0.5 Lounge Glow", LightType.Point);
            loungeGlow.transform.position = new Vector3(0f, 1.25f, 0.7f);
            loungeGlow.color = bright ? new Color(1f, 0.93f, 0.82f, 1f) : new Color(1f, 0.72f, 0.45f, 1f);
            loungeGlow.range = 4.5f;
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
            light.color = bright ? new Color(1f, 0.9f, 0.74f, 1f) : new Color(1f, 0.66f, 0.36f, 1f);
            light.intensity = 0.36f;
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

            SetEmission(levelBarFillMaterial, RoomPalette.AccentColor, 0.18f + level * 2.8f + pulse * level * 0.32f);
            SetEmission(
                lightStripMaterial,
                RoomPalette.WarmColor,
                WarmLightBaseEmission + level * WarmLightLevelEmission + pulse * WarmLightPulseEmission);

            if (screenGlow != null)
            {
                screenGlow.intensity = 1.15f + level * 0.6f;
            }

            if (loungeGlow != null)
            {
                loungeGlow.intensity = 0.95f + level * 0.5f + pulse * 0.1f;
            }

            var wallIntensity = 0.4f + level * 0.18f + pulse * 0.04f;
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

        private static void SetEmission(Material material, Color color, float intensity)
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

        /// <summary>
        /// Central palette for the room. Geometry code only references named materials,
        /// so a future room theme is a palette swap rather than a geometry rewrite.
        /// </summary>
        private sealed class RoomPalette
        {
            public static readonly Color AccentColor = new Color(0.2f, 0.9f, 0.76f, 1f);
            public static readonly Color WarmColor = new Color(1f, 0.68f, 0.4f, 1f);

            public Material Floor;
            public Material FloorGroove;
            public Material Ceiling;
            public Material CeilingInset;
            public Material Wall;
            public Material PaddedWall;
            public Material WinePanel;
            public Material Trim;
            public Material FrontTrim;
            public Material Rug;
            public Material Sofa;
            public Material SofaShadow;
            public Material Table;
            public Material Glass;
            public Material Speaker;
            public Material SpeakerCone;
            public Material Accent;
            public Material Warm;
            public Material Neon;
            public Material Moon;
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
                        ["V0.5 Floor"] = new ThemedColor(new Color(0.62f, 0.5f, 0.37f, 1f)),
                        ["V0.5 Floor Groove"] = new ThemedColor(new Color(0.48f, 0.38f, 0.28f, 1f)),
                        ["V0.5 Ceiling"] = new ThemedColor(new Color(0.88f, 0.87f, 0.84f, 1f)),
                        ["V0.5 Ceiling Inset"] = new ThemedColor(new Color(0.8f, 0.79f, 0.76f, 1f)),
                        ["V0.5 Wall"] = new ThemedColor(new Color(0.86f, 0.83f, 0.77f, 1f)),
                        ["V0.5 Padded Wall"] = new ThemedColor(new Color(0.72f, 0.8f, 0.77f, 1f)),
                        ["V0.5 Wine Panel"] = new ThemedColor(new Color(0.85f, 0.52f, 0.46f, 1f)),
                        ["V0.5 Brushed Brass"] = new ThemedColor(new Color(0.76f, 0.6f, 0.36f, 1f)),
                        ["V0.5 Front Brass"] = new ThemedColor(new Color(0.76f, 0.6f, 0.36f, 1f)),
                        ["V0.5 Rug"] = new ThemedColor(new Color(0.56f, 0.72f, 0.68f, 1f)),
                        ["V0.5 Velvet Sofa"] = new ThemedColor(new Color(0.86f, 0.62f, 0.6f, 1f)),
                        ["V0.5 Sofa Shadow"] = new ThemedColor(new Color(0.62f, 0.44f, 0.43f, 1f)),
                        ["V0.5 Table"] = new ThemedColor(new Color(0.7f, 0.58f, 0.45f, 1f)),
                        ["V0.5 Smoked Glass"] = new ThemedColor(new Color(0.74f, 0.85f, 0.86f, 0.45f)),
                        ["V0.5 Speaker Cloth"] = new ThemedColor(new Color(0.68f, 0.69f, 0.7f, 1f)),
                        ["V0.5 Speaker Cone"] = new ThemedColor(new Color(0.52f, 0.54f, 0.55f, 1f)),
                        ["V0.5 Accent"] = new ThemedColor(AccentColor, 0.8f),
                        ["V0.5 Warm Light"] = new ThemedColor(WarmColor, WarmLightBaseEmission),
                        ["V0.5 Neon"] = new ThemedColor(AccentColor, 0.7f),
                        ["V0.5 Moon"] = new ThemedColor(new Color(0.82f, 0.9f, 0.94f, 1f), 0.55f),
                        ["V0.5 Foliage"] = new ThemedColor(new Color(0.38f, 0.58f, 0.32f, 1f)),
                        ["V0.5 Ceramic"] = new ThemedColor(new Color(0.93f, 0.91f, 0.87f, 1f)),
                        ["V0.5 Citrus"] = new ThemedColor(new Color(0.92f, 0.62f, 0.28f, 1f)),
                        ["V0.5 Stage"] = new ThemedColor(new Color(0.58f, 0.46f, 0.33f, 1f)),
                        ["V0.5 Screen Frame"] = new ThemedColor(new Color(0.18f, 0.19f, 0.21f, 1f)),
                        ["V0.5 Screen Bezel"] = new ThemedColor(new Color(0.3f, 0.31f, 0.32f, 1f)),
                        ["V0.5 Level Track"] = new ThemedColor(new Color(0.62f, 0.75f, 0.73f, 1f), 0.35f),
                    };
                }

                return new Dictionary<string, ThemedColor>
                {
                    ["V0.5 Floor"] = new ThemedColor(new Color(0.19f, 0.115f, 0.075f, 1f)),
                    ["V0.5 Floor Groove"] = new ThemedColor(new Color(0.035f, 0.022f, 0.018f, 1f)),
                    ["V0.5 Ceiling"] = new ThemedColor(new Color(0.065f, 0.07f, 0.075f, 1f)),
                    ["V0.5 Ceiling Inset"] = new ThemedColor(new Color(0.026f, 0.03f, 0.034f, 1f)),
                    ["V0.5 Wall"] = new ThemedColor(new Color(0.065f, 0.09f, 0.092f, 1f)),
                    ["V0.5 Padded Wall"] = new ThemedColor(new Color(0.095f, 0.17f, 0.165f, 1f)),
                    ["V0.5 Wine Panel"] = new ThemedColor(new Color(0.25f, 0.085f, 0.12f, 1f)),
                    ["V0.5 Brushed Brass"] = new ThemedColor(new Color(0.5f, 0.36f, 0.21f, 1f)),
                    ["V0.5 Front Brass"] = new ThemedColor(new Color(0.5f, 0.36f, 0.21f, 1f)),
                    ["V0.5 Rug"] = new ThemedColor(new Color(0.055f, 0.25f, 0.235f, 1f)),
                    ["V0.5 Velvet Sofa"] = new ThemedColor(new Color(0.27f, 0.07f, 0.115f, 1f)),
                    ["V0.5 Sofa Shadow"] = new ThemedColor(new Color(0.075f, 0.02f, 0.035f, 1f)),
                    ["V0.5 Table"] = new ThemedColor(new Color(0.035f, 0.065f, 0.068f, 1f)),
                    ["V0.5 Smoked Glass"] = new ThemedColor(new Color(0.045f, 0.14f, 0.145f, 0.68f)),
                    ["V0.5 Speaker Cloth"] = new ThemedColor(new Color(0.018f, 0.024f, 0.026f, 1f)),
                    ["V0.5 Speaker Cone"] = new ThemedColor(new Color(0.045f, 0.05f, 0.052f, 1f)),
                    ["V0.5 Accent"] = new ThemedColor(AccentColor, 0.8f),
                    ["V0.5 Warm Light"] = new ThemedColor(WarmColor, WarmLightBaseEmission),
                    ["V0.5 Neon"] = new ThemedColor(AccentColor, 0.7f),
                    ["V0.5 Moon"] = new ThemedColor(new Color(0.72f, 0.84f, 0.9f, 1f), 0.7f),
                    ["V0.5 Foliage"] = new ThemedColor(new Color(0.12f, 0.28f, 0.14f, 1f), 0.12f),
                    ["V0.5 Ceramic"] = new ThemedColor(new Color(0.62f, 0.56f, 0.5f, 1f)),
                    ["V0.5 Citrus"] = new ThemedColor(new Color(0.72f, 0.42f, 0.12f, 1f)),
                    ["V0.5 Stage"] = new ThemedColor(new Color(0.16f, 0.1f, 0.07f, 1f)),
                    ["V0.5 Screen Frame"] = new ThemedColor(new Color(0.008f, 0.009f, 0.011f, 1f)),
                    ["V0.5 Screen Bezel"] = new ThemedColor(new Color(0.035f, 0.042f, 0.048f, 1f)),
                    ["V0.5 Level Track"] = new ThemedColor(new Color(0.025f, 0.075f, 0.072f, 1f), 0.35f),
                };
            }

            public static RoomPalette Create(RoomTheme theme)
            {
                var colors = GetThemeColors(theme);
                return new RoomPalette
                {
                    Floor = CreateMaterial("V0.5 Floor", colors, 0.28f, 0.02f),
                    FloorGroove = CreateMaterial("V0.5 Floor Groove", colors, 0.08f, 0f),
                    Ceiling = CreateMaterial("V0.5 Ceiling", colors, 0.16f, 0f),
                    CeilingInset = CreateMaterial("V0.5 Ceiling Inset", colors, 0.22f, 0f),
                    Wall = CreateMaterial("V0.5 Wall", colors, 0.12f, 0f),
                    PaddedWall = CreateMaterial("V0.5 Padded Wall", colors, 0.08f, 0f),
                    WinePanel = CreateMaterial("V0.5 Wine Panel", colors, 0.1f, 0f),
                    Trim = CreateMaterial("V0.5 Brushed Brass", colors, 0.42f, 0.52f),
                    FrontTrim = CreateMaterial("V0.5 Front Brass", colors, 0.18f, 0.28f),
                    Rug = CreateMaterial("V0.5 Rug", colors, 0.04f, 0f),
                    Sofa = CreateMaterial("V0.5 Velvet Sofa", colors, 0.18f, 0f),
                    SofaShadow = CreateMaterial("V0.5 Sofa Shadow", colors, 0.1f, 0f),
                    Table = CreateMaterial("V0.5 Table", colors, 0.58f, 0.22f),
                    Glass = CreateMaterial("V0.5 Smoked Glass", colors, 0.82f, 0.18f, true),
                    Speaker = CreateMaterial("V0.5 Speaker Cloth", colors, 0.06f, 0f),
                    SpeakerCone = CreateMaterial("V0.5 Speaker Cone", colors, 0.3f, 0.05f),
                    Accent = CreateMaterial("V0.5 Accent", colors, 0.72f, 0.1f),
                    Warm = CreateMaterial("V0.5 Warm Light", colors, 0.35f, 0f),
                    Neon = CreateMaterial("V0.5 Neon", colors, 0.72f, 0.1f),
                    Moon = CreateMaterial("V0.5 Moon", colors, 0.55f, 0.08f),
                    Foliage = CreateMaterial("V0.5 Foliage", colors, 0.12f, 0f),
                    Ceramic = CreateMaterial("V0.5 Ceramic", colors, 0.45f, 0.05f),
                    Citrus = CreateMaterial("V0.5 Citrus", colors, 0.38f, 0.04f),
                    Stage = CreateMaterial("V0.5 Stage", colors, 0.28f, 0.04f),
                    ScreenFrame = CreateMaterial("V0.5 Screen Frame", colors, 0.7f, 0.22f),
                    ScreenBezel = CreateMaterial("V0.5 Screen Bezel", colors, 0.18f, 0.02f),
                    LevelTrack = CreateMaterial("V0.5 Level Track", colors, 0.16f, 0f),
                };
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
                var shader = Shader.Find("Standard");
                var material = new Material(shader)
                {
                    name = materialName,
                    color = color,
                };

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", metallic);
                }

                if (material.HasProperty("_Glossiness"))
                {
                    material.SetFloat("_Glossiness", smoothness);
                }

                if (emissionIntensity > 0f)
                {
                    SetEmission(material, color, emissionIntensity);
                }

                if (transparent)
                {
                    material.SetFloat("_Mode", 3f);
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = (int)RenderQueue.Transparent;
                }

                return material;
            }
        }
    }
}
