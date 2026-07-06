using System.Collections.Generic;
using UnityEngine;

namespace TsukiVox.AudioPrototype
{
    /// <summary>
    /// V0.5 minimal VR KTV room. Tracking space equals world space in this project,
    /// so the room is built around the origin: the sofa seat sits at (0, 0, 0) and the
    /// video screen hangs on the front wall along +Z. Geometry, feedback targets and
    /// anchors are procedurally rebuilt under this component from a central palette.
    /// </summary>
    public sealed class QuestKtvRoomPrototype : MonoBehaviour
    {
        public const string RoomRootName = "V0.5 KTV Room";

        // Player start is the world/tracking origin; recentering returns the user to the sofa.
        public static readonly Vector3 PlayerStartPosition = Vector3.zero;
        public static readonly Vector3 ScreenPosition = new Vector3(0f, 1.72f, 4.14f);
        public static readonly Quaternion ScreenRotation = Quaternion.identity;
        // The matte is exactly 16:9 and the safe area equals it, so 16:9 videos fill
        // the screen with zero border. Other aspect ratios keep the full picture and
        // letterbox against the matte, which is unavoidable without cropping.
        public static readonly Vector2 ScreenMatteSize = new Vector2(3.8f, 2.1375f);
        public static readonly Vector2 ScreenSafeSize = ScreenMatteSize;

        private const string GeometryRootName = "Geometry";
        private const string FeedbackRootName = "Feedback";
        private const string AnchorsRootName = "Anchors";

        private const float RoomWidth = 7.2f;
        private const float RoomHeight = 2.9f;
        private const float BackWallZ = -1.6f;
        private const float FrontWallZ = 4.25f;
        private const float LevelBarHeight = 1.9f;
        private const float LevelBarMinHeight = 0.08f;
        private const float LevelBarCenterY = 1.55f;
        private const float LevelBarX = 2.35f;

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

        private readonly List<MeshRenderer> levelBarFills = new List<MeshRenderer>();

        private Transform geometryRoot;
        private Transform feedbackRoot;
        private Transform anchorsRoot;
        private Material levelBarFillMaterial;
        private Material lightStripMaterial;
        private Light screenGlow;
        private Light loungeGlow;
        private float smoothedLevel;

        public static QuestKtvRoomPrototype EnsureSceneRoom()
        {
            var existing = FindAnyObjectByType<QuestKtvRoomPrototype>();
            if (existing != null)
            {
                if (!Application.isPlaying)
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
                ConfigureSceneReferences();
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

            EnsureRoots();
            RemoveLegacySceneProps();
            BuildRoom();
            ConfigureLighting();
            ApplyVideoScreenLayout();
            CreateAnchors();
            ApplyFeedback(smoothedLevel);
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

            var palette = RoomPalette.Create();
            levelBarFillMaterial = palette.Accent;
            lightStripMaterial = palette.Warm;

            BuildRoomShell(palette);
            BuildWallDecoration(palette);
            BuildFurniture(palette);
            BuildScreenSurround(palette);
            BuildLevelBars(palette);
            BuildLightStrips(palette);
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

            CreateBox(geometryRoot, "lounge rug", new Vector3(3.2f, 0.022f, 1.9f), new Vector3(0f, 0.012f, 1.4f), palette.Rug);
            CreateBox(geometryRoot, "rug front trim", new Vector3(3.26f, 0.03f, 0.04f), new Vector3(0f, 0.02f, 2.35f), palette.Trim);
            CreateBox(geometryRoot, "rug back trim", new Vector3(3.26f, 0.03f, 0.04f), new Vector3(0f, 0.02f, 0.45f), palette.Trim);

            for (var index = 0; index < 4; index += 1)
            {
                var x = index % 2 == 0 ? -2.2f : 2.2f;
                var z = index < 2 ? 0.2f : 2.6f;
                CreateCylinder(geometryRoot, $"ceiling downlight {index}", 0.09f, 0.015f, new Vector3(x, RoomHeight - 0.02f, z), palette.Warm);
            }
        }

        private void BuildWallDecoration(RoomPalette palette)
        {
            for (var index = 0; index < 7; index += 1)
            {
                var z = -0.55f + index * 0.65f;
                var panelMaterial = index % 3 == 1 ? palette.WinePanel : palette.PaddedWall;
                CreateBox(geometryRoot, $"left padded panel {index}", new Vector3(0.05f, 1.6f, 0.58f), new Vector3(-RoomWidth * 0.5f + 0.03f, 1.32f, z), panelMaterial);
                CreateBox(geometryRoot, $"right padded panel {index}", new Vector3(0.05f, 1.6f, 0.58f), new Vector3(RoomWidth * 0.5f - 0.03f, 1.32f, z), panelMaterial);
                CreateBox(geometryRoot, $"left brass divider {index}", new Vector3(0.055f, 1.74f, 0.025f), new Vector3(-RoomWidth * 0.5f + 0.035f, 1.32f, z + 0.325f), palette.Trim);
                CreateBox(geometryRoot, $"right brass divider {index}", new Vector3(0.055f, 1.74f, 0.025f), new Vector3(RoomWidth * 0.5f - 0.035f, 1.32f, z + 0.325f), palette.Trim);
            }

            CreateBox(geometryRoot, "left wall rail", new Vector3(0.05f, 0.06f, 4.6f), new Vector3(-RoomWidth * 0.5f + 0.04f, 2.28f, 1.4f), palette.Trim);
            CreateBox(geometryRoot, "right wall rail", new Vector3(0.05f, 0.06f, 4.6f), new Vector3(RoomWidth * 0.5f - 0.04f, 2.28f, 1.4f), palette.Trim);

            for (var index = 0; index < 5; index += 1)
            {
                var x = -2.4f + index * 1.2f;
                var panelMaterial = index == 2 ? palette.WinePanel : palette.PaddedWall;
                CreateBox(geometryRoot, $"back padded panel {index}", new Vector3(0.95f, 1.25f, 0.05f), new Vector3(x, 1.1f, BackWallZ + 0.03f), panelMaterial);
            }

            CreateBox(geometryRoot, "back wall top rail", new Vector3(5.9f, 0.05f, 0.055f), new Vector3(0f, 1.82f, BackWallZ + 0.035f), palette.Trim);
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

            CreateBox(sofaRoot, "plinth", new Vector3(width, 0.16f, depth), new Vector3(0f, 0.08f, 0f), palette.SofaShadow);
            CreateBox(sofaRoot, "seat", new Vector3(width - 0.12f, 0.26f, depth - 0.1f), new Vector3(0f, 0.31f, 0.02f), palette.Sofa);
            CreateBox(sofaRoot, "back", new Vector3(width, 0.95f, 0.22f), new Vector3(0f, 0.62f, -depth * 0.5f + 0.11f), palette.Sofa);
            CreateBox(sofaRoot, "left arm", new Vector3(0.18f, 0.62f, depth - 0.06f), new Vector3(-width * 0.5f + 0.09f, 0.47f, 0f), palette.Sofa);
            CreateBox(sofaRoot, "right arm", new Vector3(0.18f, 0.62f, depth - 0.06f), new Vector3(width * 0.5f - 0.09f, 0.47f, 0f), palette.Sofa);
            CreateBox(sofaRoot, "front trim", new Vector3(width - 0.3f, 0.035f, 0.03f), new Vector3(0f, 0.2f, depth * 0.5f + 0.015f), palette.Trim);

            var cushionCount = Mathf.Max(2, Mathf.FloorToInt(width / 1.1f));
            var cushionWidth = (width - 0.42f) / cushionCount;
            for (var index = 0; index < cushionCount; index += 1)
            {
                var x = -width * 0.5f + 0.21f + cushionWidth * (index + 0.5f);
                CreateBox(
                    sofaRoot,
                    $"back cushion {index}",
                    new Vector3(cushionWidth - 0.05f, 0.52f, 0.07f),
                    new Vector3(x, 0.72f, -depth * 0.5f + 0.255f),
                    index % 2 == 0 ? palette.Sofa : palette.WinePanel);
            }
        }

        private void CreateCoffeeTable(RoomPalette palette)
        {
            var tableRoot = new GameObject("coffee table").transform;
            tableRoot.SetParent(geometryRoot, false);
            tableRoot.localPosition = new Vector3(0f, 0f, 1.1f);

            CreateBox(tableRoot, "table top", new Vector3(1.9f, 0.09f, 0.9f), new Vector3(0f, 0.5f, 0f), palette.Table);
            CreateBox(tableRoot, "table shelf", new Vector3(1.6f, 0.05f, 0.66f), new Vector3(0f, 0.26f, 0f), palette.Table);
            CreateBox(tableRoot, "table rim front", new Vector3(1.98f, 0.04f, 0.04f), new Vector3(0f, 0.56f, 0.45f), palette.Trim);
            CreateBox(tableRoot, "table rim back", new Vector3(1.98f, 0.04f, 0.04f), new Vector3(0f, 0.56f, -0.45f), palette.Trim);
            CreateBox(tableRoot, "tablet glow", new Vector3(1.0f, 0.02f, 0.5f), new Vector3(0f, 0.555f, 0f), palette.Accent);

            for (var index = 0; index < 4; index += 1)
            {
                var x = index % 2 == 0 ? -0.82f : 0.82f;
                var z = index < 2 ? -0.36f : 0.36f;
                CreateBox(tableRoot, $"table leg {index}", new Vector3(0.06f, 0.46f, 0.06f), new Vector3(x, 0.23f, z), palette.Trim);
            }
        }

        private void BuildScreenSurround(RoomPalette palette)
        {
            var frameOuter = new Vector2(ScreenMatteSize.x + 0.32f, ScreenMatteSize.y + 0.2f);
            const float sideBorder = 0.16f;
            const float topBorder = 0.1f;
            const float frameDepth = 0.06f;
            var frameZ = ScreenPosition.z + 0.03f;

            CreateBox(geometryRoot, "screen frame top", new Vector3(frameOuter.x, topBorder, frameDepth), new Vector3(0f, ScreenPosition.y + frameOuter.y * 0.5f - topBorder * 0.5f, frameZ), palette.ScreenFrame);
            CreateBox(geometryRoot, "screen frame bottom", new Vector3(frameOuter.x, topBorder, frameDepth), new Vector3(0f, ScreenPosition.y - frameOuter.y * 0.5f + topBorder * 0.5f, frameZ), palette.ScreenFrame);
            CreateBox(geometryRoot, "screen frame left", new Vector3(sideBorder, frameOuter.y - topBorder * 2f, frameDepth), new Vector3(-frameOuter.x * 0.5f + sideBorder * 0.5f, ScreenPosition.y, frameZ), palette.ScreenFrame);
            CreateBox(geometryRoot, "screen frame right", new Vector3(sideBorder, frameOuter.y - topBorder * 2f, frameDepth), new Vector3(frameOuter.x * 0.5f - sideBorder * 0.5f, ScreenPosition.y, frameZ), palette.ScreenFrame);

            CreateBox(geometryRoot, "screen bottom trim", new Vector3(frameOuter.x + 0.4f, 0.05f, 0.06f), new Vector3(0f, ScreenPosition.y - frameOuter.y * 0.5f - 0.09f, frameZ + 0.02f), palette.Trim);
            CreateBox(geometryRoot, "screen left column", new Vector3(0.24f, 2.6f, 0.08f), new Vector3(-2.85f, 1.35f, FrontWallZ - 0.05f), palette.WinePanel);
            CreateBox(geometryRoot, "screen right column", new Vector3(0.24f, 2.6f, 0.08f), new Vector3(2.85f, 1.35f, FrontWallZ - 0.05f), palette.WinePanel);
        }

        private void BuildLevelBars(RoomPalette palette)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var sideName = side < 0 ? "left" : "right";
                CreateBox(
                    feedbackRoot,
                    $"{sideName} mic level track",
                    new Vector3(0.12f, LevelBarHeight, 0.03f),
                    new Vector3(LevelBarX * side, LevelBarCenterY, ScreenPosition.z + 0.02f),
                    palette.LevelTrack);

                var fill = CreateBox(
                    feedbackRoot,
                    $"{sideName} mic level fill",
                    new Vector3(0.09f, LevelBarMinHeight, 0.04f),
                    new Vector3(LevelBarX * side, LevelBarCenterY - (LevelBarHeight - LevelBarMinHeight) * 0.5f, ScreenPosition.z + 0.005f),
                    palette.Accent);
                if (fill.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    levelBarFills.Add(renderer);
                }
            }
        }

        private void BuildLightStrips(RoomPalette palette)
        {
            CreateBox(feedbackRoot, "ceiling light strip", new Vector3(6.2f, 0.05f, 0.06f), new Vector3(0f, RoomHeight - 0.08f, FrontWallZ - 0.3f), palette.Warm);
            CreateBox(feedbackRoot, "left light strip", new Vector3(0.05f, 0.05f, 4.8f), new Vector3(-RoomWidth * 0.5f + 0.06f, RoomHeight - 0.12f, 1.55f), palette.Warm);
            CreateBox(feedbackRoot, "right light strip", new Vector3(0.05f, 0.05f, 4.8f), new Vector3(RoomWidth * 0.5f - 0.06f, RoomHeight - 0.12f, 1.55f), palette.Warm);
        }

        private void ConfigureLighting()
        {
            RenderSettings.ambientLight = new Color(0.27f, 0.31f, 0.33f, 1f);

            var keySpot = FindOrCreateLight("V0.5 Key Spot", LightType.Spot);
            keySpot.transform.position = new Vector3(0f, RoomHeight - 0.18f, 1.3f);
            keySpot.transform.rotation = Quaternion.Euler(78f, 0f, 0f);
            keySpot.color = new Color(1f, 0.83f, 0.64f, 1f);
            keySpot.intensity = 2.6f;
            keySpot.range = 7.5f;
            keySpot.spotAngle = 82f;
            keySpot.shadows = LightShadows.None;

            screenGlow = FindOrCreateLight("V0.5 Screen Glow", LightType.Point);
            screenGlow.transform.position = new Vector3(0f, ScreenPosition.y, ScreenPosition.z - 0.9f);
            screenGlow.color = new Color(0.62f, 0.9f, 1f, 1f);
            screenGlow.range = 5f;
            screenGlow.shadows = LightShadows.None;

            loungeGlow = FindOrCreateLight("V0.5 Lounge Glow", LightType.Point);
            loungeGlow.transform.position = new Vector3(0f, 1.25f, 0.7f);
            loungeGlow.color = new Color(1f, 0.72f, 0.45f, 1f);
            loungeGlow.range = 4.5f;
            loungeGlow.shadows = LightShadows.None;
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
            SetEmission(lightStripMaterial, RoomPalette.WarmColor, 1.05f + level * 1.35f + pulse * 0.18f);

            if (screenGlow != null)
            {
                screenGlow.intensity = 0.9f + level * 0.6f;
            }

            if (loungeGlow != null)
            {
                loungeGlow.intensity = 0.75f + level * 0.5f + pulse * 0.1f;
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

        private static GameObject CreateBox(Transform parent, string objectName, Vector3 size, Vector3 localPosition, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            return ConfigurePrimitive(cube, parent, objectName, size, localPosition, material);
        }

        private static GameObject CreateCylinder(Transform parent, string objectName, float radius, float height, Vector3 localPosition, Material material)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            // Unity's cylinder primitive is 2 units tall, so halve the requested height for scale.
            return ConfigurePrimitive(cylinder, parent, objectName, new Vector3(radius * 2f, height * 0.5f, radius * 2f), localPosition, material);
        }

        private static GameObject ConfigurePrimitive(GameObject primitive, Transform parent, string objectName, Vector3 size, Vector3 localPosition, Material material)
        {
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = size;

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
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return primitive;
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
            public Material Ceiling;
            public Material Wall;
            public Material PaddedWall;
            public Material WinePanel;
            public Material Trim;
            public Material Rug;
            public Material Sofa;
            public Material SofaShadow;
            public Material Table;
            public Material Accent;
            public Material Warm;
            public Material ScreenFrame;
            public Material LevelTrack;

            public static RoomPalette Create()
            {
                return new RoomPalette
                {
                    Floor = CreateMaterial("V0.5 Floor", new Color(0.23f, 0.17f, 0.14f, 1f), 0f, 0f),
                    Ceiling = CreateMaterial("V0.5 Ceiling", new Color(0.1f, 0.11f, 0.12f, 1f), 0f, 0f),
                    Wall = CreateMaterial("V0.5 Wall", new Color(0.11f, 0.14f, 0.15f, 1f), 0f, 0f),
                    PaddedWall = CreateMaterial("V0.5 Padded Wall", new Color(0.15f, 0.22f, 0.22f, 1f), 0f, 0f),
                    WinePanel = CreateMaterial("V0.5 Wine Panel", new Color(0.29f, 0.14f, 0.19f, 1f), 0f, 0f),
                    Trim = CreateMaterial("V0.5 Trim", new Color(0.82f, 0.55f, 0.31f, 1f), 0f, 0.42f),
                    Rug = CreateMaterial("V0.5 Rug", new Color(0.13f, 0.32f, 0.32f, 1f), 0f, 0f),
                    Sofa = CreateMaterial("V0.5 Sofa", new Color(0.3f, 0.15f, 0.2f, 1f), 0f, 0f),
                    SofaShadow = CreateMaterial("V0.5 Sofa Shadow", new Color(0.14f, 0.08f, 0.1f, 1f), 0f, 0f),
                    Table = CreateMaterial("V0.5 Table", new Color(0.09f, 0.13f, 0.14f, 1f), 0f, 0.18f),
                    Accent = CreateMaterial("V0.5 Accent", AccentColor, 0.8f, 0.1f),
                    Warm = CreateMaterial("V0.5 Warm Light", WarmColor, 1.05f, 0.05f),
                    ScreenFrame = CreateMaterial("V0.5 Screen Frame", new Color(0.01f, 0.012f, 0.015f, 1f), 0f, 0.18f),
                    LevelTrack = CreateMaterial("V0.5 Level Track", new Color(0.05f, 0.11f, 0.11f, 1f), 0.35f, 0f),
                };
            }

            private static Material CreateMaterial(string materialName, Color color, float emissionIntensity, float metallic)
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
                    material.SetFloat("_Glossiness", 0.3f);
                }

                if (emissionIntensity > 0f)
                {
                    SetEmission(material, color, emissionIntensity);
                }

                return material;
            }
        }
    }
}
