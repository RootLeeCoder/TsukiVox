using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TsukiVox.AudioPrototype
{
    public enum StageLightingPreset
    {
        Custom = -1,
        Atmosphere = 0,
        Live = 1,
        Aurora = 2,
        Finale = 3,
    }

    public enum StageLightingColorLook
    {
        Ocean = 0,
        Neon = 1,
        Sunset = 2,
        Ice = 3,
        Spectrum = 4,
    }

    /// <summary>
    /// Quest-friendly livehouse lighting rig. Four real-time spot lights provide
    /// surface lighting while six additive beam meshes provide the visible haze.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestStageLightingPrototype : MonoBehaviour
    {
        public const string RigRootName = "Livehouse Stage Lighting";
        public const int CurrentDesignRevision = 3;

        private const string EnabledPrefsKey = "TsukiVox.StageLighting.Enabled";
        private const string PresetPrefsKey = "TsukiVox.StageLighting.Preset";
        private const string ColorLookPrefsKey = "TsukiVox.StageLighting.ColorLook";
        private const string IntensityPrefsKey = "TsukiVox.StageLighting.Intensity";
        private const string SpeedPrefsKey = "TsukiVox.StageLighting.Speed";
        private const string WidthPrefsKey = "TsukiVox.StageLighting.Width";
        private const string MotionRangePrefsKey = "TsukiVox.StageLighting.MotionRange";
        private const string AutoMotionPrefsKey = "TsukiVox.StageLighting.AutoMotion";
        private const string BeatPulsePrefsKey = "TsukiVox.StageLighting.BeatPulse";
        private const string BeamsVisiblePrefsKey = "TsukiVox.StageLighting.BeamsVisible";
        private const StageLightingPreset DefaultPreset = StageLightingPreset.Aurora;
        private const StageLightingColorLook DefaultColorLook = StageLightingColorLook.Ocean;
        private const float DefaultIntensity = 0.64f;
        private const float DefaultMovementSpeed = 0.3f;
        private const float DefaultBeamWidth = 0.78f;
        private const float DefaultMotionRange = 0.46f;
        private const string BeamShaderName = "TsukiVox/Quest Stage Beam";
        private const float StageFloorHeight = 0.13f;
        private const float TrussDepth = 2.35f;
        private const float TrussWingCenter = 1.875f;
        private const float TrussWingLength = 1.55f;

        private static readonly Vector3[] FixturePositions =
        {
            new Vector3(-2.1f, 2.62f, 2.35f),
            new Vector3(-1.42f, 2.62f, 2.35f),
            new Vector3(1.42f, 2.62f, 2.35f),
            new Vector3(2.1f, 2.62f, 2.35f),
            new Vector3(-3.18f, 2.38f, 3.72f),
            new Vector3(3.18f, 2.38f, 3.72f),
        };

        private static readonly Vector3[] BaseTargets =
        {
            new Vector3(-0.72f, StageFloorHeight, 3.25f),
            new Vector3(0.28f, StageFloorHeight, 3.12f),
            new Vector3(-0.28f, StageFloorHeight, 3.68f),
            new Vector3(0.72f, StageFloorHeight, 3.42f),
            new Vector3(0.48f, StageFloorHeight, 3.25f),
            new Vector3(-0.48f, StageFloorHeight, 3.55f),
        };

        [Header("Scene References")]
        [SerializeField] private QuestKtvRoomPrototype roomPrototype;
        [SerializeField] private QuestAudioPrototype audioPrototype;

        [Header("Stage Lighting")]
        [SerializeField] private bool lightingEnabled = true;
        [SerializeField] private StageLightingPreset currentPreset = DefaultPreset;
        [SerializeField] private StageLightingColorLook colorLook = DefaultColorLook;
        [SerializeField, Range(0f, 1f)] private float intensity = DefaultIntensity;
        [SerializeField, Range(0f, 1f)] private float movementSpeed = DefaultMovementSpeed;
        [SerializeField, Range(0f, 1f)] private float beamWidth = DefaultBeamWidth;
        [SerializeField, Range(0f, 1f)] private float motionRange = DefaultMotionRange;
        [SerializeField] private bool automaticMotion = true;
        [SerializeField] private bool beatPulse = true;
        [SerializeField] private bool beamsVisible = true;
        [SerializeField, HideInInspector] private int generatedDesignRevision;

        private readonly Fixture[] fixtures = new Fixture[FixturePositions.Length];
        private MaterialPropertyBlock propertyBlock;
        private Transform rigRoot;
        [SerializeField, HideInInspector] private Material housingMaterial;
        [SerializeField, HideInInspector] private Material bracketMaterial;
        [SerializeField, HideInInspector] private Material lensMaterial;
        [SerializeField, HideInInspector] private Material beamMaterial;
        [SerializeField, HideInInspector] private Mesh beamMesh;
        [SerializeField, HideInInspector] private Mesh poolMesh;
        private bool preferencesLoaded;
        private bool preferencesDirty;
        private float preferencesSaveAt;
        private float smoothedAudioLevel;
        private QuestKtvRoomPrototype subscribedRoom;

        public bool LightingEnabled => lightingEnabled;
        public StageLightingPreset CurrentPreset => currentPreset;
        public StageLightingColorLook ColorLook => colorLook;
        public float Intensity => intensity;
        public float MovementSpeed => movementSpeed;
        public float BeamWidth => beamWidth;
        public float MotionRange => motionRange;
        public bool AutomaticMotion => automaticMotion;
        public bool BeatPulse => beatPulse;
        public bool BeamsVisible => beamsVisible;

        public event Action<bool> LightingEnabledChanged;

        public static QuestStageLightingPrototype EnsureSceneLighting(
            QuestKtvRoomPrototype room,
            QuestAudioPrototype audio)
        {
            var existing = FindAnyObjectByType<QuestStageLightingPrototype>();
            if (existing == null)
            {
                var host = room != null ? room.gameObject : new GameObject("Quest Stage Lighting Prototype");
                existing = host.AddComponent<QuestStageLightingPrototype>();
            }

            existing.Configure(room, audio);
            return existing;
        }

        private void Awake()
        {
            Configure(
                roomPrototype != null ? roomPrototype : FindAnyObjectByType<QuestKtvRoomPrototype>(),
                audioPrototype != null ? audioPrototype : FindAnyObjectByType<QuestAudioPrototype>());
        }

        private void OnEnable()
        {
            SubscribeRoom();
        }

        private void Update()
        {
            if (rigRoot == null || fixtures[0] == null)
            {
                Configure(roomPrototype, audioPrototype);
            }

            var audioLevel = audioPrototype != null
                ? Mathf.Max(audioPrototype.InputLevel, audioPrototype.OutputLevel * 0.78f)
                : 0f;
            smoothedAudioLevel = Mathf.Lerp(smoothedAudioLevel, Mathf.Clamp01(audioLevel), 0.18f);
            UpdateLighting(Time.unscaledTime);
            SavePendingPreferencesIfDue();
        }

        private void OnDisable()
        {
            SavePendingPreferences();
            UnsubscribeRoom();
        }

        public void Configure(QuestKtvRoomPrototype room, QuestAudioPrototype audio)
        {
            var nextRoom = room != null ? room : roomPrototype;
            if (nextRoom != roomPrototype)
            {
                UnsubscribeRoom();
                roomPrototype = nextRoom;
            }
            audioPrototype = audio != null ? audio : audioPrototype;
            LoadPreferencesOnce();
            EnsureRigRoot();
            if (rigRoot.childCount == 0 || generatedDesignRevision < CurrentDesignRevision)
            {
                BuildRig();
            }
            else
            {
                BindRig();
            }

            ApplyHousingTheme();
            SubscribeRoom();
            UpdateLighting(Application.isPlaying ? Time.unscaledTime : 0f);
        }

        public void SetLightingEnabled(bool enabled)
        {
            var changed = enabled != lightingEnabled;
            lightingEnabled = enabled;
            PersistBool(EnabledPrefsKey, enabled);
            UpdateLighting(Time.unscaledTime);
            if (changed)
            {
                LightingEnabledChanged?.Invoke(enabled);
            }
        }

        public void ApplyPreset(StageLightingPreset preset)
        {
            var lightingWasEnabled = lightingEnabled;
            switch (preset)
            {
                case StageLightingPreset.Atmosphere:
                    intensity = 0.42f;
                    movementSpeed = 0.18f;
                    beamWidth = 0.74f;
                    motionRange = 0.28f;
                    colorLook = StageLightingColorLook.Ice;
                    automaticMotion = true;
                    beatPulse = false;
                    break;
                case StageLightingPreset.Aurora:
                    intensity = DefaultIntensity;
                    movementSpeed = DefaultMovementSpeed;
                    beamWidth = DefaultBeamWidth;
                    motionRange = DefaultMotionRange;
                    colorLook = DefaultColorLook;
                    automaticMotion = true;
                    beatPulse = true;
                    break;
                case StageLightingPreset.Finale:
                    intensity = 1f;
                    movementSpeed = 0.88f;
                    beamWidth = 0.38f;
                    motionRange = 0.96f;
                    colorLook = StageLightingColorLook.Spectrum;
                    automaticMotion = true;
                    beatPulse = true;
                    break;
                default:
                    preset = StageLightingPreset.Live;
                    intensity = 0.72f;
                    movementSpeed = 0.52f;
                    beamWidth = 0.52f;
                    motionRange = 0.62f;
                    colorLook = StageLightingColorLook.Neon;
                    automaticMotion = true;
                    beatPulse = true;
                    break;
            }

            lightingEnabled = true;
            beamsVisible = true;
            currentPreset = preset;
            PersistAll();
            UpdateLighting(Time.unscaledTime);
            if (!lightingWasEnabled)
            {
                LightingEnabledChanged?.Invoke(true);
            }
        }

        public void SetColorLook(StageLightingColorLook look)
        {
            colorLook = Enum.IsDefined(typeof(StageLightingColorLook), look)
                ? look
                : StageLightingColorLook.Neon;
            MarkCustom();
            PersistInt(ColorLookPrefsKey, (int)colorLook);
        }

        public void SetIntensity(float value)
        {
            intensity = Mathf.Clamp01(value);
            MarkCustom();
            PersistFloat(IntensityPrefsKey, intensity);
        }

        public void SetMovementSpeed(float value)
        {
            movementSpeed = Mathf.Clamp01(value);
            MarkCustom();
            PersistFloat(SpeedPrefsKey, movementSpeed);
        }

        public void SetBeamWidth(float value)
        {
            beamWidth = Mathf.Clamp01(value);
            MarkCustom();
            PersistFloat(WidthPrefsKey, beamWidth);
        }

        public void SetMotionRange(float value)
        {
            motionRange = Mathf.Clamp01(value);
            MarkCustom();
            PersistFloat(MotionRangePrefsKey, motionRange);
        }

        public void SetAutomaticMotion(bool enabled)
        {
            automaticMotion = enabled;
            MarkCustom();
            PersistBool(AutoMotionPrefsKey, enabled);
        }

        public void SetBeatPulse(bool enabled)
        {
            beatPulse = enabled;
            MarkCustom();
            PersistBool(BeatPulsePrefsKey, enabled);
        }

        public void SetBeamsVisible(bool visible)
        {
            beamsVisible = visible;
            MarkCustom();
            PersistBool(BeamsVisiblePrefsKey, visible);
        }

        public void RestoreDefaultSettings()
        {
            ApplyPreset(DefaultPreset);
        }

        public static Color GetColorLookSwatch(StageLightingColorLook look)
        {
            return look switch
            {
                StageLightingColorLook.Ocean => Rgb(0x39D9FF),
                StageLightingColorLook.Neon => Rgb(0xFF3FA4),
                StageLightingColorLook.Sunset => Rgb(0xFFB547),
                StageLightingColorLook.Ice => Rgb(0xD8F3FF),
                StageLightingColorLook.Spectrum => Rgb(0x7CF5A5),
                _ => Rgb(0xFF3FA4),
            };
        }

        private void EnsureRigRoot()
        {
            var parent = roomPrototype != null ? roomPrototype.transform : transform;
            var existing = parent.Find(RigRootName);
            if (existing == null)
            {
                existing = new GameObject(RigRootName).transform;
                existing.SetParent(parent, false);
            }

            rigRoot = existing;
        }

        private void BuildRig()
        {
            ClearChildren(rigRoot);
            CreateResources();

            CreateTrussWing("left", -TrussWingCenter, false);
            CreateTrussWing("right", TrussWingCenter, true);

            for (var index = 0; index < fixtures.Length; index += 1)
            {
                fixtures[index] = CreateFixture(index, FixturePositions[index], index < 4);
            }

            generatedDesignRevision = CurrentDesignRevision;
        }

        private void CreateTrussWing(string side, float centerX, bool mirrorBraces)
        {
            CreateBox(
                rigRoot,
                $"{side} truss upper rail",
                new Vector3(TrussWingLength, 0.055f, 0.055f),
                new Vector3(centerX, 2.78f, TrussDepth),
                bracketMaterial);
            CreateBox(
                rigRoot,
                $"{side} truss lower rail",
                new Vector3(TrussWingLength, 0.055f, 0.055f),
                new Vector3(centerX, 2.64f, TrussDepth),
                bracketMaterial);

            const int braceCount = 4;
            const float braceInset = 0.11f;
            var usableLength = TrussWingLength - braceInset * 2f;
            for (var index = 0; index < braceCount; index += 1)
            {
                var normalized = index / (float)(braceCount - 1);
                var x = centerX - usableLength * 0.5f + normalized * usableLength;
                var alternatesForward = (index % 2 == 0) ^ mirrorBraces;
                var brace = CreateBox(
                    rigRoot,
                    $"{side} truss brace {index}",
                    new Vector3(0.035f, 0.18f, 0.035f),
                    new Vector3(x, 2.71f, TrussDepth),
                    bracketMaterial);
                brace.transform.localRotation = Quaternion.Euler(0f, 0f, alternatesForward ? 32f : -32f);
            }

            var hangerOffset = TrussWingLength * 0.5f - 0.08f;
            CreateBox(
                rigRoot,
                $"{side} truss outer hanger",
                new Vector3(0.045f, 0.16f, 0.045f),
                new Vector3(centerX + (centerX < 0f ? -hangerOffset : hangerOffset), 2.85f, TrussDepth),
                bracketMaterial);
            CreateBox(
                rigRoot,
                $"{side} truss inner hanger",
                new Vector3(0.045f, 0.16f, 0.045f),
                new Vector3(centerX + (centerX < 0f ? hangerOffset : -hangerOffset), 2.85f, TrussDepth),
                bracketMaterial);
        }

        private void BindRig()
        {
            CreateResources();
            for (var index = 0; index < fixtures.Length; index += 1)
            {
                var fixtureRoot = rigRoot.Find($"fixture {index}");
                if (fixtureRoot == null)
                {
                    BuildRig();
                    return;
                }

                var aimPivot = fixtureRoot.Find("aim pivot");
                var lens = aimPivot != null ? aimPivot.Find("lens")?.GetComponent<MeshRenderer>() : null;
                var beam = aimPivot != null ? aimPivot.Find("beam")?.GetComponent<MeshRenderer>() : null;
                var pool = rigRoot.Find($"pool {index}")?.GetComponent<MeshRenderer>();
                if (aimPivot == null || lens == null || beam == null || pool == null)
                {
                    BuildRig();
                    return;
                }

                fixtures[index] = new Fixture
                {
                    AimPivot = aimPivot,
                    LensRenderer = lens,
                    BeamRenderer = beam,
                    PoolRenderer = pool,
                    SpotLight = aimPivot.Find("spot light")?.GetComponent<Light>(),
                };
                ConfigureFixtureInteraction(aimPivot);
            }
        }

        private Fixture CreateFixture(int index, Vector3 position, bool createRealLight)
        {
            var fixtureRoot = new GameObject($"fixture {index}").transform;
            fixtureRoot.SetParent(rigRoot, false);
            fixtureRoot.localPosition = position;

            CreateCylinder(fixtureRoot, "ceiling mount", 0.055f, 0.16f, new Vector3(0f, 0.08f, 0f), bracketMaterial);
            CreateBox(fixtureRoot, "yoke", new Vector3(0.28f, 0.035f, 0.045f), new Vector3(0f, -0.015f, 0f), bracketMaterial);

            var aimPivot = new GameObject("aim pivot").transform;
            aimPivot.SetParent(fixtureRoot, false);
            aimPivot.localPosition = new Vector3(0f, -0.09f, 0f);

            var housing = CreateCylinder(aimPivot, "housing", 0.14f, 0.24f, Vector3.zero, housingMaterial);
            housing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var rearCap = CreateCylinder(aimPivot, "rear cap", 0.115f, 0.035f, new Vector3(0f, 0f, -0.13f), bracketMaterial);
            rearCap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var lens = CreateCylinder(aimPivot, "lens", 0.108f, 0.018f, new Vector3(0f, 0f, 0.13f), lensMaterial);
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var lensRenderer = lens.GetComponent<MeshRenderer>();
            ConfigureFixtureInteraction(aimPivot);

            var beamObject = new GameObject("beam");
            beamObject.transform.SetParent(aimPivot, false);
            beamObject.transform.localPosition = new Vector3(0f, 0f, 0.14f);
            beamObject.AddComponent<MeshFilter>().sharedMesh = beamMesh;
            var beamRenderer = beamObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(beamRenderer, beamMaterial, 12 + index);

            Light spotLight = null;
            if (createRealLight)
            {
                var lightObject = new GameObject("spot light");
                lightObject.transform.SetParent(aimPivot, false);
                lightObject.transform.localPosition = new Vector3(0f, 0f, 0.15f);
                spotLight = lightObject.AddComponent<Light>();
                spotLight.type = LightType.Spot;
                spotLight.range = 6.2f;
                spotLight.shadows = LightShadows.None;
                spotLight.renderMode = LightRenderMode.ForcePixel;
            }

            var poolObject = new GameObject($"pool {index}");
            poolObject.transform.SetParent(rigRoot, false);
            poolObject.AddComponent<MeshFilter>().sharedMesh = poolMesh;
            var poolRenderer = poolObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(poolRenderer, beamMaterial, 20 + index);

            return new Fixture
            {
                AimPivot = aimPivot,
                LensRenderer = lensRenderer,
                BeamRenderer = beamRenderer,
                PoolRenderer = poolRenderer,
                SpotLight = spotLight,
            };
        }

        private void ConfigureFixtureInteraction(Transform aimPivot)
        {
            if (aimPivot == null)
            {
                return;
            }

            var hitCollider = aimPivot.GetComponent<BoxCollider>();
            if (hitCollider == null)
            {
                hitCollider = aimPivot.gameObject.AddComponent<BoxCollider>();
            }

            hitCollider.center = new Vector3(0f, 0.035f, 0f);
            hitCollider.size = new Vector3(0.36f, 0.38f, 0.42f);
            hitCollider.isTrigger = false;

            var interactionTarget = aimPivot.GetComponent<QuestStageLightingFixtureTarget>();
            if (interactionTarget == null)
            {
                interactionTarget = aimPivot.gameObject.AddComponent<QuestStageLightingFixtureTarget>();
            }

            interactionTarget.Configure(this);
        }

        private void CreateResources()
        {
            propertyBlock ??= new MaterialPropertyBlock();
            housingMaterial ??= QuestStylizedMaterial.CreateLit("Livehouse fixture housing", Rgb(0x11151C), 0.28f, 0.64f);
            bracketMaterial ??= QuestStylizedMaterial.CreateLit("Livehouse fixture hardware", Rgb(0x353B45), 0.36f, 0.78f);
            lensMaterial ??= QuestStylizedMaterial.CreateLit("Livehouse fixture lens", Color.white, 0.12f, 0.08f, 1.2f);

            if (beamMaterial == null)
            {
                var shader = Shader.Find(BeamShaderName);
                if (shader == null)
                {
                    Debug.LogError($"[TsukiVox Stage Lighting] Shader {BeamShaderName} was not found.");
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                beamMaterial = new Material(shader)
                {
                    name = "Livehouse volumetric beams",
                    renderQueue = (int)RenderQueue.Transparent + 20,
                    enableInstancing = true,
                };
            }

            beamMesh ??= CreateBeamMesh();
            poolMesh ??= CreatePoolMesh();
        }

        private void UpdateLighting(float time)
        {
            if (fixtures[0] == null)
            {
                return;
            }

            var speed = Mathf.Lerp(0.18f, 1.8f, movementSpeed);
            var movement = Mathf.Lerp(0.12f, 1.02f, motionRange);
            var rhythmicPulse = beatPulse
                ? Mathf.Pow(Mathf.Clamp01(Mathf.Sin(time * 6.1f) * 0.5f + 0.5f), 7f)
                : 0f;
            var pulse = lightingEnabled
                ? 1f + rhythmicPulse * 0.28f + smoothedAudioLevel * (beatPulse ? 0.7f : 0.18f)
                : 0f;
            var spotAngle = Mathf.Lerp(20f, 46f, beamWidth);

            for (var index = 0; index < fixtures.Length; index += 1)
            {
                var fixture = fixtures[index];
                if (fixture == null || fixture.AimPivot == null)
                {
                    continue;
                }

                var target = BaseTargets[index];
                if (automaticMotion && lightingEnabled)
                {
                    var phase = index * 1.17f + (index % 2 == 0 ? 0.35f : 1.05f);
                    target.x += Mathf.Sin(time * speed * (0.72f + index * 0.035f) + phase) * movement;
                    target.z += Mathf.Cos(time * speed * (0.48f + index * 0.028f) + phase * 1.41f) * movement * 0.44f;
                    if (currentPreset == StageLightingPreset.Finale)
                    {
                        target.x += Mathf.Sin(time * speed * 1.75f + phase * 0.6f) * 0.24f;
                    }
                }

                target.x = Mathf.Clamp(target.x, -1.15f, 1.15f);
                target.z = Mathf.Clamp(target.z, 2.86f, 3.94f);
                var direction = target - fixture.AimPivot.position;
                if (direction.sqrMagnitude > 0.001f)
                {
                    fixture.AimPivot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }

                var color = ResolveFixtureColor(index, time);
                var distance = Mathf.Max(0.5f, direction.magnitude - 0.14f);
                var endRadius = Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad) * distance;
                fixture.BeamRenderer.transform.localScale = new Vector3(endRadius, endRadius, distance);
                fixture.PoolRenderer.transform.position = target + Vector3.up * (0.008f + index * 0.0007f);
                var poolDiameter = Mathf.Lerp(0.52f, 1.08f, beamWidth);
                fixture.PoolRenderer.transform.localScale = new Vector3(poolDiameter, 1f, poolDiameter);

                var activeBeams = lightingEnabled && beamsVisible;
                fixture.BeamRenderer.enabled = activeBeams;
                fixture.PoolRenderer.enabled = activeBeams;
                ApplyBeamProperties(fixture.BeamRenderer, color, intensity * pulse, 0f);
                ApplyBeamProperties(fixture.PoolRenderer, color, intensity * pulse * 0.72f, 1f);
                ApplyLensProperties(fixture.LensRenderer, color, lightingEnabled ? 1.2f + intensity * pulse * 3.4f : 0.06f);

                if (fixture.SpotLight != null)
                {
                    fixture.SpotLight.enabled = lightingEnabled;
                    fixture.SpotLight.color = color;
                    fixture.SpotLight.intensity = Mathf.Lerp(0.65f, 4.8f, intensity) * pulse;
                    fixture.SpotLight.spotAngle = spotAngle;
                    fixture.SpotLight.innerSpotAngle = spotAngle * 0.58f;
                }
            }
        }

        private Color ResolveFixtureColor(int index, float time)
        {
            Color first;
            Color second;
            switch (colorLook)
            {
                case StageLightingColorLook.Ocean:
                    first = Rgb(0x39D9FF);
                    second = Rgb(0x826BFF);
                    break;
                case StageLightingColorLook.Sunset:
                    first = Rgb(0xFFB547);
                    second = Rgb(0xFF5D7A);
                    break;
                case StageLightingColorLook.Ice:
                    first = Rgb(0xBDE9FF);
                    second = Rgb(0xF6FCFF);
                    break;
                case StageLightingColorLook.Spectrum:
                    return Color.HSVToRGB(Mathf.Repeat(index / (float)fixtures.Length + time * 0.035f, 1f), 0.72f, 1f);
                default:
                    first = Rgb(0xFF3FA4);
                    second = Rgb(0x42E5FF);
                    break;
            }

            return index % 2 == 0 ? first : second;
        }

        private void ApplyBeamProperties(MeshRenderer renderer, Color color, float effectIntensity, float effectMode)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", new Color(color.r, color.g, color.b, effectMode < 0.5f ? 0.2f : 0.36f));
            propertyBlock.SetFloat("_Intensity", Mathf.Clamp(effectIntensity, 0f, 2f));
            propertyBlock.SetFloat("_EffectMode", effectMode);
            propertyBlock.SetFloat("_NoiseSpeed", Mathf.Lerp(0.35f, 1.6f, movementSpeed));
            renderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyLensProperties(MeshRenderer renderer, Color color, float emission)
        {
            if (renderer == null)
            {
                return;
            }

            var displayColor = lightingEnabled ? color : Rgb(0x28303A);
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", displayColor);
            propertyBlock.SetColor("_Color", displayColor);
            propertyBlock.SetColor("_EmissionColor", color * emission);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyHousingTheme()
        {
            var bright = roomPrototype != null && roomPrototype.CurrentTheme == RoomTheme.Bright;
            QuestStylizedMaterial.SetBaseColor(housingMaterial, bright ? Rgb(0x2A2E35) : Rgb(0x0D1118));
            QuestStylizedMaterial.SetBaseColor(bracketMaterial, bright ? Rgb(0x777D85) : Rgb(0x353B45));
        }

        private void SubscribeRoom()
        {
            if (!isActiveAndEnabled || roomPrototype == null || subscribedRoom == roomPrototype)
            {
                return;
            }

            UnsubscribeRoom();
            subscribedRoom = roomPrototype;
            subscribedRoom.ThemeChanged += HandleRoomThemeChanged;
        }

        private void UnsubscribeRoom()
        {
            if (subscribedRoom == null)
            {
                return;
            }

            subscribedRoom.ThemeChanged -= HandleRoomThemeChanged;
            subscribedRoom = null;
        }

        private void HandleRoomThemeChanged(RoomTheme theme)
        {
            ApplyHousingTheme();
        }

        private void LoadPreferencesOnce()
        {
            if (preferencesLoaded || !Application.isPlaying)
            {
                return;
            }

            preferencesLoaded = true;
            lightingEnabled = PlayerPrefs.GetInt(EnabledPrefsKey, 1) != 0;
            currentPreset = ParsePreset(PlayerPrefs.GetInt(PresetPrefsKey, (int)DefaultPreset));
            colorLook = ParseColorLook(PlayerPrefs.GetInt(ColorLookPrefsKey, (int)DefaultColorLook));
            intensity = Mathf.Clamp01(PlayerPrefs.GetFloat(IntensityPrefsKey, DefaultIntensity));
            movementSpeed = Mathf.Clamp01(PlayerPrefs.GetFloat(SpeedPrefsKey, DefaultMovementSpeed));
            beamWidth = Mathf.Clamp01(PlayerPrefs.GetFloat(WidthPrefsKey, DefaultBeamWidth));
            motionRange = Mathf.Clamp01(PlayerPrefs.GetFloat(MotionRangePrefsKey, DefaultMotionRange));
            automaticMotion = PlayerPrefs.GetInt(AutoMotionPrefsKey, 1) != 0;
            beatPulse = PlayerPrefs.GetInt(BeatPulsePrefsKey, 1) != 0;
            beamsVisible = PlayerPrefs.GetInt(BeamsVisiblePrefsKey, 1) != 0;
        }

        private void MarkCustom()
        {
            if (currentPreset == StageLightingPreset.Custom)
            {
                return;
            }

            currentPreset = StageLightingPreset.Custom;
            PersistInt(PresetPrefsKey, (int)currentPreset);
        }

        private void PersistAll()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerPrefs.SetInt(EnabledPrefsKey, lightingEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PresetPrefsKey, (int)currentPreset);
            PlayerPrefs.SetInt(ColorLookPrefsKey, (int)colorLook);
            PlayerPrefs.SetFloat(IntensityPrefsKey, intensity);
            PlayerPrefs.SetFloat(SpeedPrefsKey, movementSpeed);
            PlayerPrefs.SetFloat(WidthPrefsKey, beamWidth);
            PlayerPrefs.SetFloat(MotionRangePrefsKey, motionRange);
            PlayerPrefs.SetInt(AutoMotionPrefsKey, automaticMotion ? 1 : 0);
            PlayerPrefs.SetInt(BeatPulsePrefsKey, beatPulse ? 1 : 0);
            PlayerPrefs.SetInt(BeamsVisiblePrefsKey, beamsVisible ? 1 : 0);
            PlayerPrefs.Save();
            preferencesDirty = false;
        }

        private void PersistBool(string key, bool value)
        {
            PersistInt(key, value ? 1 : 0);
        }

        private void PersistInt(string key, int value)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerPrefs.SetInt(key, value);
            QueuePreferencesSave();
        }

        private void PersistFloat(string key, float value)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerPrefs.SetFloat(key, value);
            QueuePreferencesSave();
        }

        private void QueuePreferencesSave()
        {
            preferencesDirty = true;
            preferencesSaveAt = Time.unscaledTime + 0.35f;
        }

        private void SavePendingPreferencesIfDue()
        {
            if (preferencesDirty && Time.unscaledTime >= preferencesSaveAt)
            {
                SavePendingPreferences();
            }
        }

        private void SavePendingPreferences()
        {
            if (!preferencesDirty || !Application.isPlaying)
            {
                return;
            }

            PlayerPrefs.Save();
            preferencesDirty = false;
        }

        private static StageLightingPreset ParsePreset(int value)
        {
            return value is >= (int)StageLightingPreset.Custom and <= (int)StageLightingPreset.Finale
                ? (StageLightingPreset)value
                : DefaultPreset;
        }

        private static StageLightingColorLook ParseColorLook(int value)
        {
            return value is >= (int)StageLightingColorLook.Ocean and <= (int)StageLightingColorLook.Spectrum
                ? (StageLightingColorLook)value
                : DefaultColorLook;
        }

        private static GameObject CreateBox(Transform parent, string objectName, Vector3 size, Vector3 position, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ConfigurePrimitive(box, parent, objectName, size, position, material);
            return box;
        }

        private static GameObject CreateCylinder(Transform parent, string objectName, float radius, float height, Vector3 position, Material material)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ConfigurePrimitive(cylinder, parent, objectName, new Vector3(radius * 2f, height * 0.5f, radius * 2f), position, material);
            return cylinder;
        }

        private static void ConfigurePrimitive(GameObject primitive, Transform parent, string objectName, Vector3 scale, Vector3 position, Material material)
        {
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            if (primitive.TryGetComponent<Collider>(out var collider))
            {
                DestroyGeneratedObject(collider);
            }

            var renderer = primitive.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void ConfigureRenderer(MeshRenderer renderer, Material material, int sortingOrder)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
        }

        private static Mesh CreateBeamMesh()
        {
            const int planeCount = 4;
            const int sectionCount = 3;
            const int pointsPerSection = 3;
            var vertices = new Vector3[planeCount * sectionCount * pointsPerSection];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[planeCount * (sectionCount - 1) * 4 * 3];
            var vertexOffset = 0;
            var triangleOffset = 0;

            for (var plane = 0; plane < planeCount; plane += 1)
            {
                var angle = plane * Mathf.PI / planeCount;
                var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                for (var section = 0; section < sectionCount; section += 1)
                {
                    var v = section / (float)(sectionCount - 1);
                    var z = Mathf.Lerp(0.03f, 1f, v);
                    var radius = Mathf.Lerp(0.025f, 1f, v);
                    for (var point = 0; point < pointsPerSection; point += 1)
                    {
                        var u = point / (float)(pointsPerSection - 1);
                        vertices[vertexOffset + section * pointsPerSection + point] = radial * ((u * 2f - 1f) * radius) + Vector3.forward * z;
                        uvs[vertexOffset + section * pointsPerSection + point] = new Vector2(u, v);
                    }
                }

                for (var section = 0; section < sectionCount - 1; section += 1)
                {
                    for (var strip = 0; strip < pointsPerSection - 1; strip += 1)
                    {
                        var a = vertexOffset + section * pointsPerSection + strip;
                        var b = a + 1;
                        var c = a + pointsPerSection;
                        var d = c + 1;
                        triangles[triangleOffset++] = a;
                        triangles[triangleOffset++] = c;
                        triangles[triangleOffset++] = b;
                        triangles[triangleOffset++] = b;
                        triangles[triangleOffset++] = c;
                        triangles[triangleOffset++] = d;
                    }
                }

                vertexOffset += sectionCount * pointsPerSection;
            }

            var mesh = new Mesh
            {
                name = "Livehouse crossed volumetric beam mesh",
                vertices = vertices,
                uv = uvs,
                triangles = triangles,
                bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2.2f, 2.2f, 1.1f)),
            };
            return mesh;
        }

        private static Mesh CreatePoolMesh()
        {
            return new Mesh
            {
                name = "Livehouse light pool mesh",
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, 0.5f),
                    new Vector3(-0.5f, 0f, 0.5f),
                },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.02f, 1f)),
            };
        }

        private static void ClearChildren(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index -= 1)
            {
                DestroyGeneratedObject(parent.GetChild(index).gameObject);
            }
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static Color Rgb(uint hex)
        {
            return new Color(
                ((hex >> 16) & 0xff) / 255f,
                ((hex >> 8) & 0xff) / 255f,
                (hex & 0xff) / 255f,
                1f);
        }

        private sealed class Fixture
        {
            public Transform AimPivot;
            public MeshRenderer LensRenderer;
            public MeshRenderer BeamRenderer;
            public MeshRenderer PoolRenderer;
            public Light SpotLight;
        }
    }
}
