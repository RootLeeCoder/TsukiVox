using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    public sealed class QuestAppShellPrototype : MonoBehaviour
    {
        // V0.5: the control shell rests above the coffee table like a KTV song-picker
        // tablet, laid back at a low angle so the seated sightline clears its top edge
        // and never blocks the video screen behind it.
        public static readonly Vector3 ControlPanelWorldPosition = new Vector3(0f, 0.74f, 1.1f);
        public static readonly Quaternion ControlPanelWorldRotation = Quaternion.Euler(68f, 0f, 0f);
        public static readonly Vector3 ControlPanelWorldScale = Vector3.one * 0.00115f;
        public static readonly Vector2 ControlPanelSize = new Vector2(1120f, 560f);

        private const string CanvasName = "Prototype Canvas";
        private const string PanelName = "Panel";
        private const string ShellObjectName = "Quest App Shell Prototype";

        private static readonly Color PanelBackground = new Color32(11, 13, 18, 240);
        private static readonly Color SectionBackground = new Color32(20, 23, 29, 209);
        private static readonly Color SectionLine = new Color32(57, 59, 64, 184);
        private static readonly Color TextPrimary = new Color32(242, 239, 232, 255);
        private static readonly Color TextSecondary = new Color32(169, 166, 151, 255);
        private static readonly Color Accent = new Color32(216, 196, 157, 255);
        [Header("Scene References")]
        [SerializeField] private Canvas controlCanvas;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text appStatusText;
        [SerializeField] private Button copyAppDebugButton;
        [SerializeField] private Toggle videoDebugToggle;
        [SerializeField] private QuestAudioPrototype audioPrototype;
        [SerializeField] private QuestPlaylistPrototype playlistPrototype;
        [SerializeField] private QuestVideoScreenPrototype videoScreenPrototype;
        [SerializeField] private QuestConsumerUiPrototype consumerUi;
        [SerializeField] private QuestTabletTiltController tabletTiltController;
        [SerializeField] private QuestRoomThemeController roomThemeController;
        [SerializeField] private QuestStageLightingPrototype stageLightingPrototype;

        [Header("Runtime")]
        [SerializeField] private bool organizePanelOnAwake = true;
        [SerializeField, Min(0.25f)] private float statusRefreshSeconds = 1f;

        private readonly StringBuilder debugBuilder = new StringBuilder(4096);
        private float nextStatusRefreshAt;

        public static QuestAppShellPrototype EnsureSceneShell()
        {
            var existing = FindAnyObjectByType<QuestAppShellPrototype>();
            if (existing != null)
            {
                existing.ConfigureSceneReferences();
                return existing;
            }

            var shellObject = new GameObject(ShellObjectName);
            var shell = shellObject.AddComponent<QuestAppShellPrototype>();
            shell.ConfigureSceneReferences();
            return shell;
        }

        private void Awake()
        {
            ConfigureSceneReferences();
        }

        private void Start()
        {
            RefreshAppStatus();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextStatusRefreshAt)
            {
                return;
            }

            nextStatusRefreshAt = Time.unscaledTime + statusRefreshSeconds;
            RefreshAppStatus();
        }

        public void ConfigureSceneReferences()
        {
            QuestXrBootstrap.EnsureSceneBootstrap();
            audioPrototype = audioPrototype != null ? audioPrototype : FindAnyObjectByType<QuestAudioPrototype>();
            playlistPrototype = playlistPrototype != null ? playlistPrototype : QuestPlaylistPrototype.EnsureScenePrototype();
            videoScreenPrototype = videoScreenPrototype != null ? videoScreenPrototype : QuestVideoScreenPrototype.EnsureScenePrototype();
            var ktvRoom = QuestKtvRoomPrototype.EnsureSceneRoom();
            stageLightingPrototype = QuestStageLightingPrototype.EnsureSceneLighting(ktvRoom, audioPrototype);
            var palette = QuestUiThemePalette.For(ktvRoom != null ? ktvRoom.CurrentTheme : RoomTheme.Dark);

            controlCanvas = controlCanvas != null ? controlCanvas : FindOrCreateControlCanvas();
            if (controlCanvas == null)
            {
                return;
            }

            ConfigureControlCanvas(controlCanvas);
            panel = panel != null ? panel : FindOrCreatePanel(controlCanvas.transform, palette);
            tabletTiltController = tabletTiltController != null
                ? tabletTiltController
                : GetComponent<QuestTabletTiltController>();
            tabletTiltController = tabletTiltController != null
                ? tabletTiltController
                : gameObject.AddComponent<QuestTabletTiltController>();
            tabletTiltController.Configure(controlCanvas, panel, ktvRoom);
            roomThemeController = roomThemeController != null
                ? roomThemeController
                : GetComponent<QuestRoomThemeController>();
            roomThemeController = roomThemeController != null
                ? roomThemeController
                : gameObject.AddComponent<QuestRoomThemeController>();
            roomThemeController.Configure(ktvRoom);
            EnsureEventSystem();
            QuestUiPointer.EnsureScenePointer();

            if (organizePanelOnAwake && panel != null)
            {
                panel.sizeDelta = ControlPanelSize;
                EnsurePanelImage(panel, palette);
            }

            appStatusText = appStatusText != null
                ? appStatusText
                : FindOrCreateText(panel, "App Status", "Quest App shell starting.", 15, FontStyle.Normal, new Vector2(0f, 292f), new Vector2(900f, 38f), TextAnchor.MiddleCenter, palette);
            copyAppDebugButton = copyAppDebugButton != null
                ? copyAppDebugButton
                : FindOrCreateButton(panel, "Copy App Debug", "Copy App Debug", new Vector2(522f, -300f), new Vector2(176f, 42f), 13, palette);
            WireCopyButton();
            videoDebugToggle = videoDebugToggle != null
                ? videoDebugToggle
                : FindOrCreateToggle(panel, "Video Debug", new Vector2(-240f, -300f), new Vector2(176f, 38f), false, palette);
            WireVideoDebugToggle();
            consumerUi = consumerUi != null ? consumerUi : GetComponent<QuestConsumerUiPrototype>();
            consumerUi = consumerUi != null ? consumerUi : gameObject.AddComponent<QuestConsumerUiPrototype>();
            consumerUi.Configure(
                panel,
                audioPrototype,
                playlistPrototype,
                videoScreenPrototype,
                this,
                ktvRoom,
                stageLightingPrototype);
            RefreshAppStatus();
        }

        public void CopyAppDebugInfoToClipboard()
        {
            var debugInfo = BuildAppDebugInfo();
            TsukiVoxClipboard.CopyPlainText("TsukiVox App Debug", debugInfo);
            if (appStatusText != null)
            {
                appStatusText.text = "App debug copied to clipboard.";
            }

            Debug.Log($"[TsukiVox App Shell] Copied app debug info:\n{debugInfo}");
        }

        public void CopyCompleteDebugInfoToClipboard()
        {
            var appDebugInfo = BuildAppDebugInfo();
            var videoDebugInfo = videoScreenPrototype != null ? videoScreenPrototype.GetDebugInfo() : string.Empty;
            debugBuilder.Clear();
            debugBuilder.Append(appDebugInfo);
            if (!string.IsNullOrEmpty(videoDebugInfo))
            {
                debugBuilder.AppendLine();
                debugBuilder.AppendLine("--- Video Diagnostics ---");
                debugBuilder.Append(videoDebugInfo);
            }

            var debugInfo = debugBuilder.ToString();
            TsukiVoxClipboard.CopyPlainText("TsukiVox Complete Debug", debugInfo);
            if (appStatusText != null)
            {
                appStatusText.text = "Complete diagnostics copied to clipboard.";
            }

            Debug.Log($"[TsukiVox App Shell] Copied complete diagnostics:\n{debugInfo}");
        }

        public string GetAppDebugInfo()
        {
            return BuildAppDebugInfo();
        }

        private void WireCopyButton()
        {
            if (copyAppDebugButton == null)
            {
                return;
            }

            copyAppDebugButton.onClick.RemoveListener(CopyAppDebugInfoToClipboard);
            copyAppDebugButton.onClick.AddListener(CopyAppDebugInfoToClipboard);
        }

        private void WireVideoDebugToggle()
        {
            if (videoDebugToggle == null)
            {
                return;
            }

            videoDebugToggle.SetIsOnWithoutNotify(videoScreenPrototype != null && videoScreenPrototype.IsStatusOverlayVisible);
            videoDebugToggle.onValueChanged.RemoveListener(HandleVideoDebugToggleChanged);
            videoDebugToggle.onValueChanged.AddListener(HandleVideoDebugToggleChanged);
        }

        private void HandleVideoDebugToggleChanged(bool visible)
        {
            if (videoScreenPrototype != null)
            {
                videoScreenPrototype.SetStatusOverlayVisible(visible);
            }
        }

        private void RefreshAppStatus()
        {
            if (appStatusText == null)
            {
                return;
            }

            var playlistStatus = playlistPrototype != null && playlistPrototype.IsConnected
                ? "helper connected"
                : "helper offline";
            var micStatus = audioPrototype != null && audioPrototype.IsMonitoring ? "mic active" : "mic standby";
            appStatusText.text = $"V0.5 KTV room shell  {playlistStatus}  {micStatus}  {Application.platform}";
        }

        private string BuildAppDebugInfo()
        {
            debugBuilder.Clear();
            debugBuilder.AppendLine("TsukiVox V0.4 App Debug");
            debugBuilder.AppendLine($"utc {DateTime.UtcNow:O}");
            debugBuilder.AppendLine($"platform {Application.platform}");
            debugBuilder.AppendLine($"unity {Application.unityVersion}");
            debugBuilder.AppendLine($"product {Application.productName}");
            debugBuilder.AppendLine($"identifier {Application.identifier}");
            debugBuilder.AppendLine($"version {Application.version}");
            debugBuilder.AppendLine($"buildId {QuestBuildInfo.BuildId}");
            debugBuilder.AppendLine($"buildTimeLocal {QuestBuildInfo.BuildTimeLocal}");
            debugBuilder.AppendLine($"buildTimeDisplay {QuestBuildInfo.BuildTimeDisplay}");
            debugBuilder.AppendLine($"buildGuid {Application.buildGUID}");
            debugBuilder.AppendLine($"gitCommit {QuestBuildInfo.GitCommit}");
            debugBuilder.AppendLine($"gitDirty {QuestBuildInfo.GitDirty}");
            debugBuilder.AppendLine($"persistentDataPath {Application.persistentDataPath}");
            debugBuilder.AppendLine($"temporaryCachePath {Application.temporaryCachePath}");
            debugBuilder.AppendLine($"internetReachability {Application.internetReachability}");
            debugBuilder.AppendLine($"audioSampleRate {AudioSettings.outputSampleRate}");
            AudioSettings.GetDSPBufferSize(out var dspBufferLength, out var dspBufferCount);
            debugBuilder.AppendLine($"dspBuffer {dspBufferLength}x{dspBufferCount}");
            debugBuilder.AppendLine($"mainCamera {(Camera.main == null ? "missing" : Camera.main.name)}");
            debugBuilder.AppendLine($"canvas {(controlCanvas == null ? "missing" : $"{controlCanvas.name} {controlCanvas.renderMode} {controlCanvas.GetComponent<RectTransform>().sizeDelta}")}");
            debugBuilder.AppendLine($"panel {(panel == null ? "missing" : $"{panel.name} {panel.sizeDelta} pos {panel.anchoredPosition}")}");
            debugBuilder.AppendLine($"audioPrototype {audioPrototype != null}");
            debugBuilder.AppendLine($"playlistPrototype {playlistPrototype != null}");
            debugBuilder.AppendLine($"videoScreenPrototype {videoScreenPrototype != null}");
            debugBuilder.AppendLine($"tabletTilt {(tabletTiltController == null ? "missing" : $"{tabletTiltController.CurrentTiltAngle:0}deg step {tabletTiltController.CurrentStepIndex} animating {tabletTiltController.IsAnimating}")}");
            var stageLightingStatus = stageLightingPrototype == null
                ? "missing"
                : $"enabled {stageLightingPrototype.LightingEnabled} " +
                  $"preset {stageLightingPrototype.CurrentPreset} color {stageLightingPrototype.ColorLook} " +
                  $"intensity {stageLightingPrototype.Intensity:0.00} speed {stageLightingPrototype.MovementSpeed:0.00} " +
                  $"width {stageLightingPrototype.BeamWidth:0.00} range {stageLightingPrototype.MotionRange:0.00} " +
                  $"motion {stageLightingPrototype.AutomaticMotion} pulse {stageLightingPrototype.BeatPulse} beams {stageLightingPrototype.BeamsVisible}";
            debugBuilder.AppendLine($"stageLighting {stageLightingStatus}");

            var handheldProps = FindAnyObjectByType<QuestHandheldPropsPrototype>();
            if (handheldProps != null)
            {
                var faceClearance = float.IsPositiveInfinity(handheldProps.MicrophoneFaceSurfaceClearance)
                    ? "unavailable"
                    : $"{handheldProps.MicrophoneFaceSurfaceClearance * 100f:0.0}cm";
                debugBuilder.AppendLine(
                    $"micFaceProximity clearance {faceClearance} " +
                    $"warningAt {handheldProps.MicFaceWarningClearance * 100f:0.0}cm " +
                    $"criticalAt {handheldProps.MicFaceCriticalClearance * 100f:0.0}cm " +
                    $"strength {handheldProps.MicFaceHapticStrength:0.00} " +
                    $"enabled {handheldProps.MicFaceHapticsEnabled} " +
                    $"intensity {handheldProps.MicrophoneFaceProximity:0.00} " +
                    $"warning {handheldProps.IsMicrophoneFaceWarningActive} " +
                    $"mouthLocal {handheldProps.MicFaceMouthLocalOffset:F3} " +
                    $"marker {handheldProps.IsMicFaceMouthMarkerVisible}");
            }

            if (playlistPrototype != null)
            {
                var state = playlistPrototype.CurrentState;
                var item = state?.CurrentItem;
                debugBuilder.AppendLine($"playlistConnected {playlistPrototype.IsConnected}");
                debugBuilder.AppendLine($"playlistCanSendControl {playlistPrototype.CanSendControl}");
                debugBuilder.AppendLine($"directApiOrigin {BilibiliDirectClient.ApiOrigin}");
                debugBuilder.AppendLine($"directSuggestOrigin {BilibiliDirectClient.SuggestOrigin}");
                debugBuilder.AppendLine($"playbackState {state?.playback}");
                debugBuilder.AppendLine($"queueCount {state?.QueueCount}");
                debugBuilder.AppendLine($"currentIndex {state?.currentIndex}");
                debugBuilder.AppendLine($"itemTitle {item?.title}");
                debugBuilder.AppendLine($"itemStatus {item?.status}");
                debugBuilder.AppendLine($"itemPlayableUrl {item?.playableUrl}");
                debugBuilder.AppendLine($"itemResolvedUrl {(item == null ? string.Empty : playlistPrototype.ResolvePlayableUrl(item.playableUrl))}");
            }

            if (audioPrototype != null)
            {
                debugBuilder.AppendLine($"audioMonitoring {audioPrototype.IsMonitoring}");
                debugBuilder.AppendLine($"audioWaitingForPermission {audioPrototype.IsWaitingForPermission}");
                debugBuilder.AppendLine($"audioSafetyReducingGain {audioPrototype.IsSafetyReducingGain}");
                debugBuilder.AppendLine($"audioMonitorRequested {audioPrototype.SelectedMonitorMode}");
                debugBuilder.AppendLine($"audioMonitorActive {audioPrototype.ActiveMonitorMode}");
                debugBuilder.AppendLine($"audioMonitorFallback {audioPrototype.IsNativeFallbackActive}");
                debugBuilder.AppendLine($"audioMonitorFallbackReason {audioPrototype.NativeFallbackReason}");
                debugBuilder.AppendLine($"audioBackend {audioPrototype.ActiveBackendName}");
                var nativeStats = audioPrototype.NativeStats;
                debugBuilder.AppendLine($"audioNativeApiVersion {nativeStats.version}/{NativeOboeDryMonitor.RequiredApiVersion}");
                debugBuilder.AppendLine($"audioNativeApi {audioPrototype.NativeApiName}");
                debugBuilder.AppendLine($"audioNativeError {audioPrototype.NativeError}");
                debugBuilder.AppendLine(
                    $"audioNativeStats running {nativeStats.IsRunning} rate {nativeStats.sampleRate} burst {nativeStats.framesPerBurst} " +
                    $"sharing {nativeStats.inputSharingMode}/{nativeStats.outputSharingMode} preset {nativeStats.inputPreset} " +
                    $"capacity {nativeStats.inputCapacityFrames}/{nativeStats.outputCapacityFrames} " +
                    $"requestedBuffers {nativeStats.requestedInputBufferFrames}/{nativeStats.requestedOutputBufferFrames} " +
                    $"actualBuffers {nativeStats.inputBufferFrames}/{nativeStats.outputBufferFrames} xruns {nativeStats.inputXRunCount}/{nativeStats.outputXRunCount} " +
                    $"callbacks {nativeStats.callbackCount} short {nativeStats.shortReadCount} ({nativeStats.ShortReadRatio:P2}) " +
                    $"mismatch {nativeStats.frameMismatchCount} inputFill {nativeStats.InputFrameFillRatio:P2} " +
                    $"frames {nativeStats.receivedInputFrameCount}/{nativeStats.requestedInputFrameCount} streamError {nativeStats.lastStreamError}");
                debugBuilder.AppendLine(
                    $"audioNativeGains gain {nativeStats.actualGain:0.000} drive {nativeStats.actualInputDrive:0.000} " +
                    $"distance {nativeStats.actualDistanceGain:0.000} safety {nativeStats.actualSafetyGain:0.000}");
                debugBuilder.AppendLine(
                    $"audioNativeLevels pre {nativeStats.preDspLevel:0.000} post {nativeStats.postDspLevel:0.000} " +
                    $"output {nativeStats.outputLevel:0.000} reduction {nativeStats.compressorReductionDb:0.0}/{nativeStats.limiterReductionDb:0.0}dB");
                var voiceEmitter = audioPrototype.VoiceEmitterPosition;
                debugBuilder.AppendLine($"audioVoiceSpatialEnabled {audioPrototype.IsSpatialVoiceEnabled}");
                debugBuilder.AppendLine($"audioVoiceEmitter {voiceEmitter.x:0.000},{voiceEmitter.y:0.000},{voiceEmitter.z:0.000}");
                debugBuilder.AppendLine(
                    $"audioVoiceSpatial blend {audioPrototype.VoiceSpatialBlend:0.000} " +
                    $"spread {audioPrototype.VoiceStereoSpread:0.0} " +
                    $"distance {audioPrototype.VoiceMinDistance:0.00}-{audioPrototype.VoiceMaxDistance:0.00} " +
                    $"reverbZoneMix {audioPrototype.VoiceReverbZoneMix:0.000}");
                debugBuilder.AppendLine($"audioNativeBackendSelectable {audioPrototype.IsNativeBackendSelectable}");
                debugBuilder.AppendLine($"audioPreset {audioPrototype.CurrentPresetName}");
                debugBuilder.AppendLine($"audioMonitorVolume {audioPrototype.MonitorVolume:0.000}");
                debugBuilder.AppendLine(
                    $"audioMonitorPreGain {audioPrototype.MonitorPreGain:0.000} " +
                    $"{audioPrototype.MonitorPreGainDecibels:+0.0;-0.0;0.0}dB");
                debugBuilder.AppendLine($"audioEffectiveMonitorVolume {audioPrototype.EffectiveMonitorVolume:0.000}");
                debugBuilder.AppendLine(
                    $"audioEffects ambience {audioPrototype.AmbienceAmount:0.00} " +
                    $"echo {audioPrototype.EchoAmount:0.00} dynamics {audioPrototype.DynamicsAmount:0.00} " +
                    $"custom {audioPrototype.HasCustomEffectSettings}");
                debugBuilder.AppendLine(
                    $"audioDistanceMonitor enabled {audioPrototype.IsDistanceMonitoringEnabled} " +
                    $"tracked {audioPrototype.IsMicrophoneDistanceTracked} " +
                    $"clearance {(audioPrototype.IsMicrophoneDistanceTracked ? audioPrototype.MicrophoneSurfaceClearance.ToString("0.000") : "unavailable")} " +
                    $"gain {audioPrototype.DistanceMonitorGain:0.00}");
            }

            if (videoScreenPrototype != null)
            {
                debugBuilder.AppendLine($"videoVolume {videoScreenPrototype.VideoVolume:0.000}");
            }

            AppendTextIfPresent("audioStatus", "Ready.");
            AppendTextIfPresent("audioPreset", "Preset");
            AppendTextIfPresent("audioMetrics", "Metrics");
            AppendTextIfPresent("connectionText", "Helper Connection");
            AppendTextIfPresent("songText", "Helper Current Song");
            AppendTextIfPresent("queueText", "Helper Queue");
            AppendTextIfPresent("playableUrlText", "Helper Playable URL");
            AppendCacheDirectoryInfo();
            return debugBuilder.ToString();
        }

        private void AppendTextIfPresent(string label, string objectName)
        {
            var text = panel == null ? null : FindText(panel, objectName);
            if (text == null)
            {
                return;
            }

            debugBuilder.AppendLine($"{label} {SanitizeLine(text.text)}");
        }

        private void AppendCacheDirectoryInfo()
        {
            AppendCacheDirectoryInfo(DirectPlaylist.MediaCacheDirectoryName);
            AppendCacheDirectoryInfo(DirectPlaylist.LegacyVideoCacheDirectoryName);
        }

        private void AppendCacheDirectoryInfo(string directoryName)
        {
            var cacheDirectory = Path.Combine(Application.persistentDataPath, directoryName);
            debugBuilder.AppendLine($"mediaCacheDirectory {directoryName} {cacheDirectory}");
            if (!Directory.Exists(cacheDirectory))
            {
                debugBuilder.AppendLine($"mediaCacheExists {directoryName} False");
                return;
            }

            debugBuilder.AppendLine($"mediaCacheExists {directoryName} True");
            var files = Directory.GetFiles(cacheDirectory);
            debugBuilder.AppendLine($"mediaCacheFileCount {directoryName} {files.Length}");
            for (var i = 0; i < Mathf.Min(files.Length, 8); i += 1)
            {
                var info = new FileInfo(files[i]);
                debugBuilder.AppendLine($"mediaCacheFile {directoryName} {info.Name} {info.Length} bytes");
            }
        }

        private static void ConfigureControlCanvas(Canvas canvas)
        {
            var camera = Camera.main;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;
            canvas.sortingOrder = 10;

            var rect = canvas.GetComponent<RectTransform>();
            rect.position = ControlPanelWorldPosition;
            rect.rotation = ControlPanelWorldRotation;
            rect.localScale = ControlPanelWorldScale;
            rect.sizeDelta = ControlPanelSize;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ControlPanelSize;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 24f;

            var raycaster = canvas.GetComponent<GraphicRaycaster>() ?? canvas.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.ignoreReversedGraphics = false;
        }

        private static Canvas FindOrCreateControlCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            for (var i = 0; i < canvases.Length; i += 1)
            {
                if (canvases[i].name == CanvasName)
                {
                    return canvases[i];
                }
            }

            var canvasObject = new GameObject(CanvasName);
            return canvasObject.AddComponent<Canvas>();
        }

        private static RectTransform FindOrCreatePanel(Transform canvasTransform, QuestUiThemePalette palette)
        {
            var existing = canvasTransform.Find(PanelName) as RectTransform;
            if (existing != null)
            {
                existing.sizeDelta = ControlPanelSize;
                EnsurePanelImage(existing, palette);
                return existing;
            }

            var panelObject = new GameObject(PanelName);
            panelObject.transform.SetParent(canvasTransform, false);
            var rect = panelObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = ControlPanelSize;
            EnsurePanelImage(rect, palette);
            return rect;
        }

        private static void EnsurePanelImage(RectTransform panelRect, QuestUiThemePalette palette)
        {
            var image = panelRect.GetComponent<Image>() ?? panelRect.gameObject.AddComponent<Image>();
            image.color = palette.ScreenBackground;
        }

        private static void OrganizeControlPanel(RectTransform panelRect)
        {
            var room = FindAnyObjectByType<QuestKtvRoomPrototype>();
            var palette = QuestUiThemePalette.For(room != null ? room.CurrentTheme : RoomTheme.Dark);
            panelRect.sizeDelta = new Vector2(1240f, 690f);
            EnsurePanelImage(panelRect, palette);

            var title = FindText(panelRect, "TsukiVox Quest Audio Prototype") ?? FindText(panelRect, "TsukiVox Quest Prototype");
            if (title != null)
            {
                title.name = "TsukiVox Quest";
                title.text = "TsukiVox Quest";
                title.fontSize = 30;
                title.fontStyle = FontStyle.Bold;
                title.alignment = TextAnchor.MiddleLeft;
                SetRect(title.rectTransform, new Vector2(-500f, 306f), new Vector2(260f, 42f));
            }

            var subtitle = FindOrCreateText(panelRect, "V0.4 Subtitle", "Native Quest control shell", 13, FontStyle.Normal, new Vector2(-290f, 306f), new Vector2(260f, 36f), TextAnchor.MiddleLeft);
            subtitle.color = TextSecondary;

            ConfigureSection(panelRect, "Connection Section", "Connection", new Vector2(310f, 204f), new Vector2(520f, 196f));
            ConfigureSection(panelRect, "Now Playing Section", "Now Playing", new Vector2(310f, -58f), new Vector2(520f, 302f));
            ConfigureSection(panelRect, "Mic Section", "Mic", new Vector2(-310f, 122f), new Vector2(560f, 360f));
            ConfigureSection(panelRect, "Debug Section", "Debug", new Vector2(-310f, -242f), new Vector2(560f, 126f));

            MoveText(panelRect, "V0.1 Audio", "Mic", new Vector2(-548f, 276f), new Vector2(130f, 32f), 18, TextAnchor.MiddleLeft);
            MoveText(panelRect, "Ready.", null, new Vector2(-310f, 230f), new Vector2(520f, 56f), 15, TextAnchor.UpperLeft);
            MoveText(panelRect, "Preset", null, new Vector2(-310f, 178f), new Vector2(520f, 30f), 18, TextAnchor.MiddleLeft);
            MoveText(panelRect, "Metrics", null, new Vector2(-310f, 90f), new Vector2(520f, 128f), 13, TextAnchor.UpperLeft);

            MoveRect(panelRect, "Input Level", new Vector2(-445f, 0f), new Vector2(250f, 46f));
            MoveRect(panelRect, "Output Level", new Vector2(-175f, 0f), new Vector2(250f, 46f));
            MoveRect(panelRect, "Monitor Volume", new Vector2(-310f, -64f), new Vector2(390f, 50f));
            MoveRect(panelRect, "Start Mic", new Vector2(-520f, -132f), new Vector2(124f, 44f));
            MoveRect(panelRect, "Stop", new Vector2(-386f, -132f), new Vector2(96f, 44f));
            MoveRect(panelRect, "Prev", new Vector2(-282f, -132f), new Vector2(88f, 44f));
            MoveRect(panelRect, "Next", new Vector2(-184f, -132f), new Vector2(88f, 44f));
            MoveRect(panelRect, "Monitor", new Vector2(-510f, -184f), new Vector2(150f, 36f));
            MoveRect(panelRect, "Native", new Vector2(-350f, -184f), new Vector2(150f, 36f));
            MoveRect(panelRect, "Safety", new Vector2(-190f, -184f), new Vector2(150f, 36f));

            var helperRoot = ResolveHelperRoot(panelRect);
            MoveText(helperRoot, "Helper Title", "Direct Request", new Vector2(40f, 276f), new Vector2(220f, 32f), 18, TextAnchor.MiddleLeft);
            MoveText(panelRect, "Direct Request", "Direct Request", new Vector2(92f, 276f), new Vector2(220f, 32f), 18, TextAnchor.MiddleLeft);
            MoveText(helperRoot, new[] { "Helper Connection", "Direct request ready" }, null, new Vector2(310f, 226f), new Vector2(480f, 58f), 13, TextAnchor.UpperLeft);

            MoveText(helperRoot, new[] { "Helper Current Song", "Current: no song selected." }, null, new Vector2(310f, 42f), new Vector2(480f, 86f), 14, TextAnchor.UpperLeft);
            MoveText(helperRoot, new[] { "Helper Queue", "Queue 0 item(s)" }, null, new Vector2(310f, -54f), new Vector2(480f, 64f), 13, TextAnchor.UpperLeft);
            MoveText(helperRoot, new[] { "Helper Playable URL", "Playable URL: none" }, null, new Vector2(310f, -128f), new Vector2(480f, 58f), 11, TextAnchor.UpperLeft);
            MoveRect(helperRoot, new[] { "Direct Play", "Play" }, new Vector2(92f, -210f), new Vector2(112f, 46f));
            MoveRect(helperRoot, new[] { "Direct Previous" }, new Vector2(216f, -210f), new Vector2(104f, 46f));
            MoveRect(helperRoot, new[] { "Direct Next" }, new Vector2(332f, -210f), new Vector2(104f, 46f));
            MoveRect(helperRoot, new[] { "Direct Replay", "Replay" }, new Vector2(452f, -210f), new Vector2(104f, 46f));

            MoveRect(panelRect, "Copy Debug", new Vector2(-420f, -300f), new Vector2(154f, 42f));
            MoveRect(panelRect, "Video Debug", new Vector2(-240f, -300f), new Vector2(176f, 38f));
            ApplyVisualStyle(panelRect);
        }

        private static RectTransform ResolveHelperRoot(RectTransform panelRect)
        {
            var helperRoot = panelRect.Find("V0.2 Runtime Helper") as RectTransform;
            if (helperRoot == null)
            {
                return panelRect;
            }

            SetRect(helperRoot, Vector2.zero, panelRect.sizeDelta);
            return helperRoot;
        }

        private static void ConfigureSection(RectTransform panelRect, string name, string title, Vector2 position, Vector2 size)
        {
            var section = panelRect.Find(name) as RectTransform;
            if (section == null)
            {
                var sectionObject = new GameObject(name);
                sectionObject.transform.SetParent(panelRect, false);
                section = sectionObject.AddComponent<RectTransform>();
                var image = sectionObject.AddComponent<Image>();
                image.color = SectionBackground;
            }

            section.SetAsFirstSibling();
            SetRect(section, position, size);
            var sectionImage = section.GetComponent<Image>();
            if (sectionImage != null)
            {
                sectionImage.color = SectionBackground;
            }

            var label = FindOrCreateText(section, "Label", title, 12, FontStyle.Bold, new Vector2(-size.x * 0.5f + 54f, size.y * 0.5f - 22f), new Vector2(120f, 24f), TextAnchor.MiddleLeft);
            label.color = TextSecondary;

            var line = section.Find("Line") as RectTransform;
            if (line == null)
            {
                var lineObject = new GameObject("Line");
                lineObject.transform.SetParent(section, false);
                line = lineObject.AddComponent<RectTransform>();
                lineObject.AddComponent<Image>();
            }

            SetRect(line, new Vector2(0f, size.y * 0.5f - 42f), new Vector2(size.x - 40f, 2f));
            var lineImage = line.GetComponent<Image>();
            if (lineImage != null)
            {
                lineImage.color = SectionLine;
            }
        }

        private static void ApplyVisualStyle(Transform root)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i += 1)
            {
                if (texts[i].name == "Label" && texts[i].transform.parent != null && texts[i].transform.parent.name.EndsWith("Section", StringComparison.Ordinal))
                {
                    continue;
                }

                texts[i].color = texts[i].fontStyle == FontStyle.Bold ? TextPrimary : TextSecondary;
                texts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                texts[i].verticalOverflow = VerticalWrapMode.Overflow;
            }

            var buttons = root.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i += 1)
            {
                buttons[i].colors = CreateSelectableColors();
                var image = buttons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = new Color32(26, 29, 36, 255);
                }
            }

            var sliders = root.GetComponentsInChildren<Slider>(true);
            for (var i = 0; i < sliders.Length; i += 1)
            {
                var fill = sliders[i].fillRect != null ? sliders[i].fillRect.GetComponent<Image>() : null;
                if (fill != null)
                {
                    fill.color = Accent;
                }
            }

            var toggles = root.GetComponentsInChildren<Toggle>(true);
            for (var i = 0; i < toggles.Length; i += 1)
            {
                toggles[i].colors = CreateSelectableColors();
                if (toggles[i].graphic is Image graphicImage)
                {
                    graphicImage.color = Accent;
                }
            }
        }

        private static Text FindOrCreateText(Transform parent, string name, string value, int fontSize, FontStyle style, Vector2 position, Vector2 size, TextAnchor alignment, QuestUiThemePalette palette = null)
        {
            var existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent<Text>(out var existingText))
            {
                existingText.text = value;
                existingText.fontSize = fontSize;
                existingText.fontStyle = style;
                existingText.alignment = alignment;
                SetRect(existingText.rectTransform, position, size);
                return existingText;
            }

            palette ??= QuestUiThemePalette.For(RoomTheme.Dark);
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = style == FontStyle.Bold ? palette.TextPrimary : palette.TextSecondary;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect(text.rectTransform, position, size);
            return text;
        }

        private static Button FindOrCreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, int fontSize, QuestUiThemePalette palette)
        {
            var existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent<Button>(out var existingButton))
            {
                SetRect(existingButton.GetComponent<RectTransform>(), position, size);
                SetButtonLabel(existingButton, label, fontSize);
                return existingButton;
            }

            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            SetRect(rect, position, size);
            var image = buttonObject.AddComponent<Image>();
            image.color = palette.ButtonSurface;
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateSelectableColors(palette);

            var labelText = FindOrCreateText(buttonObject.transform, "Label", label, fontSize, FontStyle.Bold, Vector2.zero, size, TextAnchor.MiddleCenter, palette);
            labelText.color = palette.TextPrimary;
            return button;
        }

        private static Toggle FindOrCreateToggle(Transform parent, string name, Vector2 position, Vector2 size, bool value, QuestUiThemePalette palette)
        {
            var existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent<Toggle>(out var existingToggle))
            {
                SetRect(existing.GetComponent<RectTransform>(), position, size);
                return existingToggle;
            }

            var toggleObject = new GameObject(name);
            toggleObject.transform.SetParent(parent, false);
            var rect = toggleObject.AddComponent<RectTransform>();
            SetRect(rect, position, size);

            var background = new GameObject("Checkmark Background");
            background.transform.SetParent(toggleObject.transform, false);
            var backgroundRect = background.AddComponent<RectTransform>();
            SetRect(backgroundRect, new Vector2(-size.x * 0.5f + 20f, 0f), new Vector2(28f, 28f));
            var backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = palette.ButtonSurface;

            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform, false);
            var checkmarkRect = checkmark.AddComponent<RectTransform>();
            SetRect(checkmarkRect, Vector2.zero, new Vector2(18f, 18f));
            var checkmarkImage = checkmark.AddComponent<Image>();
            checkmarkImage.color = palette.Accent;

            var label = FindOrCreateText(toggleObject.transform, "Label", name, 14, FontStyle.Normal, new Vector2(22f, 0f), new Vector2(size.x - 56f, size.y), TextAnchor.MiddleLeft, palette);
            label.color = palette.TextSecondary;

            var toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            toggle.isOn = value;
            toggle.colors = CreateSelectableColors(palette);
            return toggle;
        }

        private static void SetButtonLabel(Button button, string label, int fontSize)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text == null)
            {
                return;
            }

            text.text = label;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = TextPrimary;
            SetRect(text.rectTransform, Vector2.zero, button.GetComponent<RectTransform>().sizeDelta);
        }

        private static void MoveText(Transform parent, string name, string nextText, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var text = FindText(parent, name);
            if (text == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(nextText))
            {
                text.text = nextText;
            }

            text.fontSize = fontSize;
            text.alignment = alignment;
            SetRect(text.rectTransform, position, size);
        }

        private static void MoveText(Transform parent, string[] names, string nextText, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            for (var i = 0; i < names.Length; i += 1)
            {
                var text = FindText(parent, names[i]);
                if (text == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(nextText))
                {
                    text.text = nextText;
                }

                text.fontSize = fontSize;
                text.alignment = alignment;
                SetRect(text.rectTransform, position, size);
                return;
            }
        }

        private static void MoveRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var child = parent.Find(name);
            if (child == null || !child.TryGetComponent<RectTransform>(out var rect))
            {
                return;
            }

            SetRect(rect, position, size);
            var label = child.GetComponentInChildren<Text>();
            if (label != null && child.TryGetComponent<Button>(out _))
            {
                SetRect(label.rectTransform, Vector2.zero, size);
            }
        }

        private static void MoveRect(Transform parent, string[] names, Vector2 position, Vector2 size)
        {
            for (var i = 0; i < names.Length; i += 1)
            {
                var child = parent.Find(names[i]);
                if (child == null || !child.TryGetComponent<RectTransform>(out var rect))
                {
                    continue;
                }

                SetRect(rect, position, size);
                var label = child.GetComponentInChildren<Text>();
                if (label != null && child.TryGetComponent<Button>(out _))
                {
                    SetRect(label.rectTransform, Vector2.zero, size);
                }

                return;
            }
        }

        private static Text FindText(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null && child.TryGetComponent<Text>(out var text))
            {
                return text;
            }

            for (var i = 0; i < parent.childCount; i += 1)
            {
                var nested = FindText(parent.GetChild(i), name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static ColorBlock CreateSelectableColors(QuestUiThemePalette palette = null)
        {
            palette ??= QuestUiThemePalette.For(RoomTheme.Dark);
            return new ColorBlock
            {
                normalColor = palette.ButtonSurface,
                highlightedColor = palette.ButtonHighlighted,
                pressedColor = palette.ButtonPressed,
                selectedColor = palette.SurfaceHover,
                disabledColor = palette.ButtonDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.05f,
            };
        }

        private static void EnsureEventSystem()
        {
            QuestUiPointer.EnsureSceneEventSystem();
        }

        private static string SanitizeLine(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
