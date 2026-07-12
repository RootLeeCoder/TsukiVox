using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TsukiVox.AudioPrototype
{
    public sealed class QuestVideoScreenPrototype : MonoBehaviour
    {
        private const int DefaultTextureWidth = 1920;
        private const int DefaultTextureHeight = 1080;
        private const int ScreenLayer = 0;
        private const string MainTextureProperty = "_MainTex";

        private static readonly Vector3 DefaultScreenPosition = new Vector3(0f, 3.35f, 3.45f);
        private static readonly Vector2 DefaultScreenSafeSize = new Vector2(3.25f, 1.83f);
        private static readonly Vector2 DefaultScreenMatteSize = new Vector2(3.5f, 2.03f);

        [Header("Playlist")]
        [SerializeField] private QuestPlaylistPrototype playlistPrototype;
        [SerializeField] private bool autoPlayWhenPlaylistIsPlaying = true;
        [SerializeField] private bool sendNextWhenVideoEnds = true;

        [Header("Screen")]
        [SerializeField] private RawImage screenImage;
        [SerializeField] private MeshRenderer screenRenderer;
        [SerializeField] private MeshRenderer matteRenderer;
        [SerializeField] private Text statusText;
        [SerializeField] private Button copyDebugButton;
        // V0.5: the on-screen status/debug overlay is hidden by default; the coffee
        // table panel exposes a toggle so it can be brought back when troubleshooting.
        [SerializeField] private bool showStatusOverlay;
        [SerializeField] private int renderTextureWidth = DefaultTextureWidth;
        [SerializeField] private int renderTextureHeight = DefaultTextureHeight;

        [Header("Screen Layout")]
        [SerializeField] private Vector3 screenPosition = DefaultScreenPosition;
        [SerializeField] private Vector3 screenEulerAngles = Vector3.zero;
        [SerializeField] private Vector2 screenSafeSize = DefaultScreenSafeSize;
        [SerializeField] private Vector2 screenMatteSize = DefaultScreenMatteSize;

        [Header("Loading")]
        [SerializeField] private bool cacheRemoteVideosBeforePlayback = true;
        [SerializeField, Min(3f)] private float prepareTimeoutSeconds = 20f;
        [SerializeField, Min(5f)] private float downloadTimeoutSeconds = 90f;

        private readonly StringBuilder debugBuilder = new StringBuilder(2048);
        private GameObject statusCanvasObject;
        private VideoPlayer videoPlayer;
        private AudioSource videoAudioSource;
        private RenderTexture renderTexture;
        private Material screenMaterial;
        private Material matteMaterial;
        private RectTransform screenCanvasRect;
        private string activeItemId;
        private string activePlayableUrl;
        private string activeRawPlayableUrl;
        private string activePlaybackUrl;
        private string lastVideoProbeSummary = "probe pending";
        private string lastVideoCacheSummary = "cache pending";
        private string lastStatusMessage = "Video: waiting for ready playlist item.";
        private long lastProbeContentLength = -1;
        private long lastAppliedCommandAt;
        private bool suppressNextOnStop;
        private bool sentNextForCurrentClip;
        private bool hasPreparedFirstFrame;
        private bool isPreparingVideo;
        private bool pendingPlayAfterPrepare;
        private float prepareStartedAt;
        private float lastStatusRefreshAt;
        private Coroutine sendNextRoutine;
        private Coroutine loadRoutine;

        public static QuestVideoScreenPrototype EnsureScenePrototype()
        {
            var existing = FindAnyObjectByType<QuestVideoScreenPrototype>();
            if (existing != null)
            {
                existing.ConfigureSceneReferences();
                return existing;
            }

            var screenObject = new GameObject("Quest Video Screen Prototype");
            var prototype = screenObject.AddComponent<QuestVideoScreenPrototype>();
            prototype.ConfigureSceneReferences();
            return prototype;
        }

        private void Awake()
        {
            ConfigureSceneReferences();
        }

        private void OnEnable()
        {
            SubscribePlaylist();
            ApplyPlaylistState(playlistPrototype != null ? playlistPrototype.CurrentState : null);
        }

        private void OnDisable()
        {
            UnsubscribePlaylist();
            if (sendNextRoutine != null)
            {
                StopCoroutine(sendNextRoutine);
                sendNextRoutine = null;
            }

            if (loadRoutine != null)
            {
                StopCoroutine(loadRoutine);
                loadRoutine = null;
            }
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
            }
        }

        private void Update()
        {
            if (playlistPrototype == null)
            {
                playlistPrototype = FindAnyObjectByType<QuestPlaylistPrototype>();
                SubscribePlaylist();
            }

            if (isPreparingVideo &&
                !videoPlayer.isPrepared &&
                Time.unscaledTime - prepareStartedAt > prepareTimeoutSeconds)
            {
                isPreparingVideo = false;
                Debug.LogWarning($"[TsukiVox Video] Prepare timed out for {activePlayableUrl}");
                SetStatus($"Video: prepare timed out.\n{GetVideoDiagnostics()}");
            }

            SynchronizeVideoSurface();
            UpdatePlaybackDiagnostics();
        }

        private void ConfigureSceneReferences()
        {
            playlistPrototype = playlistPrototype != null ? playlistPrototype : QuestPlaylistPrototype.EnsureScenePrototype();
            EnsureVideoPlayer();
            EnsureRenderTexture();
            EnsureScreenObjects();
            ConfigureVideoOutput();
            EnsureStatusText();
            EnsureDebugCopyUi();
            ApplyStatusOverlayVisibility();
            SubscribePlaylist();
        }

        public bool IsStatusOverlayVisible => showStatusOverlay;

        public string StatusSummary => lastStatusMessage;

        public bool IsPreparing => isPreparingVideo;

        public bool IsPlaying => videoPlayer != null && videoPlayer.isPlaying;

        public void SetStatusOverlayVisible(bool visible)
        {
            showStatusOverlay = visible;
            ApplyStatusOverlayVisibility();
        }

        private void ApplyStatusOverlayVisibility()
        {
            var canvasObject = ResolveStatusCanvasObject();
            if (canvasObject != null && canvasObject.activeSelf != showStatusOverlay)
            {
                canvasObject.SetActive(showStatusOverlay);
            }
        }

        private GameObject ResolveStatusCanvasObject()
        {
            if (statusCanvasObject != null)
            {
                return statusCanvasObject;
            }

            if (statusText != null)
            {
                var canvas = statusText.GetComponentInParent<Canvas>(true);
                if (canvas != null)
                {
                    statusCanvasObject = canvas.gameObject;
                    return statusCanvasObject;
                }
            }

            statusCanvasObject = GameObject.Find("V0.3 Video Screen Status Canvas");
            return statusCanvasObject;
        }

        private void EnsureVideoPlayer()
        {
            videoPlayer = GetComponent<VideoPlayer>();
            if (videoPlayer == null)
            {
                videoPlayer = gameObject.AddComponent<VideoPlayer>();
            }

            videoAudioSource = GetComponent<AudioSource>();
            if (videoAudioSource == null)
            {
                videoAudioSource = gameObject.AddComponent<AudioSource>();
            }

            videoAudioSource.playOnAwake = false;
            videoAudioSource.loop = false;
            videoAudioSource.spatialBlend = 0f;
            videoAudioSource.priority = 64;
            videoAudioSource.bypassReverbZones = true;

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.skipOnDrop = true;
            videoPlayer.waitForFirstFrame = false;
            videoPlayer.sendFrameReadyEvents = true;
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);

            videoPlayer.prepareCompleted -= HandlePrepared;
            videoPlayer.errorReceived -= HandleVideoError;
            videoPlayer.loopPointReached -= HandleVideoEnded;
            videoPlayer.frameReady -= HandleFrameReady;
            videoPlayer.prepareCompleted += HandlePrepared;
            videoPlayer.errorReceived += HandleVideoError;
            videoPlayer.loopPointReached += HandleVideoEnded;
            videoPlayer.frameReady += HandleFrameReady;
        }

        private void EnsureRenderTexture()
        {
            if (renderTexture != null &&
                renderTexture.width == renderTextureWidth &&
                renderTexture.height == renderTextureHeight)
            {
                return;
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
            }

            renderTexture = new RenderTexture(renderTextureWidth, renderTextureHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = "TsukiVox Video Screen RenderTexture",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
            };
            renderTexture.Create();
        }

        private Quaternion ScreenRotation => Quaternion.Euler(screenEulerAngles);

        private Vector3 ScreenViewerOffset => ScreenRotation * Vector3.back;

        public void ApplyScreenLayout(Vector3 position, Quaternion rotation, Vector2 safeSize, Vector2 matteSize)
        {
            screenPosition = position;
            screenEulerAngles = rotation.eulerAngles;
            screenSafeSize = safeSize;
            screenMatteSize = matteSize;
            ApplyScreenLayoutToObjects();
        }

        private void ApplyScreenLayoutToObjects()
        {
            if (matteRenderer != null)
            {
                matteRenderer.transform.SetPositionAndRotation(screenPosition, ScreenRotation);
                matteRenderer.transform.localScale = new Vector3(screenMatteSize.x, screenMatteSize.y, 1f);
            }

            if (screenRenderer != null)
            {
                screenRenderer.transform.SetPositionAndRotation(screenPosition + ScreenViewerOffset * 0.01f, ScreenRotation);
            }

            ConfigureScreenImageTransform();
            ApplyStatusCanvasLayout();
            var width = videoPlayer != null && videoPlayer.width > 0 ? videoPlayer.width : (ulong)DefaultTextureWidth;
            var height = videoPlayer != null && videoPlayer.height > 0 ? videoPlayer.height : (ulong)DefaultTextureHeight;
            FitScreenToVideo(width, height);
        }

        private void ApplyStatusCanvasLayout()
        {
            var canvasObject = ResolveStatusCanvasObject();
            if (canvasObject == null || !canvasObject.TryGetComponent<RectTransform>(out var rect))
            {
                return;
            }

            rect.position = screenPosition +
                            ScreenRotation * new Vector3(0f, -(screenMatteSize.y * 0.5f - 0.17f), 0f) +
                            ScreenViewerOffset * 0.02f;
            rect.rotation = ScreenRotation;
        }

        private void EnsureScreenObjects()
        {
            if (matteRenderer == null)
            {
                var matteObject = GameObject.Find("V0.3 Video Screen Matte") ?? GameObject.CreatePrimitive(PrimitiveType.Quad);
                matteObject.name = "V0.3 Video Screen Matte";
                matteObject.layer = ScreenLayer;
                matteObject.transform.SetParent(null, false);
                matteObject.transform.position = screenPosition;
                matteObject.transform.rotation = ScreenRotation;
                matteObject.transform.localScale = new Vector3(screenMatteSize.x, screenMatteSize.y, 1f);
                DestroyCollider(matteObject);
                matteRenderer = matteObject.GetComponent<MeshRenderer>();
            }

            if (screenRenderer == null)
            {
                var screenObject = GameObject.Find("V0.3 Video Screen") ?? GameObject.CreatePrimitive(PrimitiveType.Quad);
                screenObject.name = "V0.3 Video Screen";
                screenObject.layer = ScreenLayer;
                screenObject.transform.SetParent(null, false);
                screenObject.transform.position = screenPosition + ScreenViewerOffset * 0.01f;
                screenObject.transform.rotation = ScreenRotation;
                screenObject.transform.localScale = new Vector3(screenSafeSize.x, screenSafeSize.y, 1f);
                DestroyCollider(screenObject);
                screenRenderer = screenObject.GetComponent<MeshRenderer>();
            }

            EnsureScreenImage();

            if (matteMaterial == null)
            {
                matteMaterial = CreateUnlitMaterial(new Color(0.004f, 0.006f, 0.008f, 1f));
            }

            if (screenMaterial == null)
            {
                screenMaterial = CreateUnlitMaterial(Color.black);
                screenMaterial.mainTexture = renderTexture;
            }

            matteRenderer.sharedMaterial = matteMaterial;
            screenRenderer.material = screenMaterial;
            SetScreenTexture(renderTexture);
            FitScreenToVideo(DefaultTextureWidth, DefaultTextureHeight);
        }

        private void EnsureScreenImage()
        {
            if (screenImage != null)
            {
                ConfigureScreenImageTransform();
                return;
            }

            var canvasObject = GameObject.Find("V0.3 Video Screen Canvas");
            if (canvasObject == null)
            {
                canvasObject = new GameObject("V0.3 Video Screen Canvas");
            }

            var canvas = canvasObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 2;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasObject.AddComponent<CanvasScaler>();
            }

            scaler.dynamicPixelsPerUnit = 10f;

            screenCanvasRect = canvasObject.GetComponent<RectTransform>();

            var imageTransform = canvasObject.transform.Find("Video Screen Image");
            GameObject imageObject;
            if (imageTransform == null)
            {
                imageObject = new GameObject("Video Screen Image");
                imageObject.transform.SetParent(canvasObject.transform, false);
            }
            else
            {
                imageObject = imageTransform.gameObject;
            }

            screenImage = imageObject.GetComponent<RawImage>();
            if (screenImage == null)
            {
                screenImage = imageObject.AddComponent<RawImage>();
            }

            ConfigureScreenImageTransform();
        }

        private void ConfigureScreenImageTransform()
        {
            if (screenImage == null)
            {
                return;
            }

            screenCanvasRect = screenImage.canvas != null ? screenImage.canvas.GetComponent<RectTransform>() : screenCanvasRect;
            if (screenCanvasRect != null)
            {
                screenCanvasRect.position = screenPosition + ScreenViewerOffset * 0.02f;
                screenCanvasRect.rotation = ScreenRotation;
                screenCanvasRect.localScale = Vector3.one * (screenSafeSize.x / DefaultTextureWidth);
                screenCanvasRect.sizeDelta = new Vector2(DefaultTextureWidth, DefaultTextureHeight);
            }

            var imageRect = screenImage.rectTransform;
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            screenImage.color = Color.white;
            screenImage.raycastTarget = false;
            screenImage.texture = renderTexture;
        }

        private void ConfigureVideoOutput()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            // Preserve every source pixel in the fixed-size RenderTexture. The screen
            // quad then applies the source aspect ratio with contain semantics.
            videoPlayer.aspectRatio = VideoAspectRatio.Stretch;
            SetScreenTexture(renderTexture);
        }

        private void EnsureStatusText()
        {
            if (statusText != null)
            {
                return;
            }

            var canvas = FindOrCreateStatusCanvas();
            var existing = canvas.transform.Find("Video Screen Status");
            if (existing != null && existing.TryGetComponent<Text>(out var existingText))
            {
                statusText = existingText;
                return;
            }

            var statusObject = new GameObject("Video Screen Status");
            statusObject.transform.SetParent(canvas.transform, false);
            statusText = statusObject.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 18;
            statusText.fontStyle = FontStyle.Bold;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            statusText.color = new Color(0.82f, 0.92f, 0.94f, 0.9f);
            statusText.text = "Video: waiting for ready playlist item.";
            statusText.rectTransform.sizeDelta = new Vector2(960f, 72f);
            statusText.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void EnsureDebugCopyUi()
        {
            if (copyDebugButton != null)
            {
                WireDebugCopyButton();
                return;
            }

            var canvas = FindOrCreateMainPanelCanvas();
            if (canvas == null)
            {
                return;
            }

            var panel = canvas.transform.Find("Panel") as RectTransform;
            if (panel == null)
            {
                return;
            }

            copyDebugButton = FindOrCreateButton(panel, "Copy Debug", "Copy Debug", new Vector2(300f, -292f), new Vector2(150f, 38f), 13);
            WireDebugCopyButton();
        }

        private void WireDebugCopyButton()
        {
            if (copyDebugButton == null)
            {
                return;
            }

            copyDebugButton.onClick.RemoveListener(CopyDebugInfoToClipboard);
            copyDebugButton.onClick.AddListener(CopyDebugInfoToClipboard);
        }

        private Canvas FindOrCreateStatusCanvas()
        {
            var existing = ResolveStatusCanvasObject();
            if (existing != null && existing.TryGetComponent<Canvas>(out var existingCanvas))
            {
                return existingCanvas;
            }

            var canvasObject = new GameObject("V0.3 Video Screen Status Canvas");
            statusCanvasObject = canvasObject;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 4;
            canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(960f, 72f);
            rect.position = screenPosition +
                            ScreenRotation * new Vector3(0f, -(screenMatteSize.y * 0.5f - 0.17f), 0f) +
                            ScreenViewerOffset * 0.02f;
            rect.rotation = ScreenRotation;
            rect.localScale = Vector3.one * 0.0025f;
            return canvas;
        }

        private static Canvas FindOrCreateMainPanelCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            for (var i = 0; i < canvases.Length; i += 1)
            {
                if (canvases[i].name == "Prototype Canvas")
                {
                    return canvases[i];
                }
            }

            return canvases.Length > 0 ? canvases[0] : null;
        }

        private void SubscribePlaylist()
        {
            if (playlistPrototype == null)
            {
                return;
            }

            playlistPrototype.StateChanged -= HandlePlaylistStateChanged;
            playlistPrototype.StateChanged += HandlePlaylistStateChanged;
        }

        private void UnsubscribePlaylist()
        {
            if (playlistPrototype != null)
            {
                playlistPrototype.StateChanged -= HandlePlaylistStateChanged;
            }
        }

        private void HandlePlaylistStateChanged(QuestPlaylistPrototype source, PlaylistState state)
        {
            ApplyPlaylistState(state);
        }

        private void ApplyPlaylistState(PlaylistState state)
        {
            if (state == null)
            {
                SetStatus("Video: waiting for playlist sync.");
                return;
            }

            var item = state.CurrentItem;
            if (item == null)
            {
                StopCurrentVideo("Video: no song selected.");
                return;
            }

            if (string.Equals(item.status, PlaylistClient.StatusDownloading, StringComparison.OrdinalIgnoreCase))
            {
                StopCurrentVideo($"Video: waiting for {SafeTitle(item)} to finish downloading.");
                return;
            }

            if (string.Equals(item.status, PlaylistClient.StatusError, StringComparison.OrdinalIgnoreCase))
            {
                StopCurrentVideo(string.IsNullOrWhiteSpace(item.message)
                    ? $"Video: {SafeTitle(item)} failed."
                    : $"Video: {item.message.Trim()}");
                return;
            }

            if (!item.IsReady)
            {
                StopCurrentVideo($"Video: {SafeTitle(item)} is not ready.");
                return;
            }

            var resolvedUrl = ResolveVideoPlaybackUrl(item.playableUrl);
            if (string.IsNullOrWhiteSpace(resolvedUrl))
            {
                StopCurrentVideo($"Video: {SafeTitle(item)} has no playable URL.");
                return;
            }

            if (!IsSupportedVideoUrl(resolvedUrl))
            {
                StopCurrentVideo("Video: first V0.3 pass supports direct MP4/WebM URLs.");
                return;
            }

            var needsLoad = !string.Equals(activeItemId, item.id, StringComparison.Ordinal) ||
                            !string.Equals(activePlayableUrl, resolvedUrl, StringComparison.Ordinal);
            if (needsLoad)
            {
                LoadVideo(item, resolvedUrl, state);
                return;
            }

            ApplyPlaybackState(state, item);
        }

        private void LoadVideo(PlaylistItem item, string resolvedUrl, PlaylistState state)
        {
            if (loadRoutine != null)
            {
                StopCoroutine(loadRoutine);
            }

            activeItemId = item.id;
            activePlayableUrl = resolvedUrl;
            activeRawPlayableUrl = item.playableUrl;
            lastVideoProbeSummary = "probe pending";
            lastVideoCacheSummary = "cache pending";
            lastProbeContentLength = -1;
            sentNextForCurrentClip = false;
            hasPreparedFirstFrame = false;
            isPreparingVideo = false;
            pendingPlayAfterPrepare = false;
            loadRoutine = StartCoroutine(LoadVideoRoutine(item, resolvedUrl, state));
        }

        private IEnumerator LoadVideoRoutine(PlaylistItem item, string resolvedUrl, PlaylistState state)
        {
            suppressNextOnStop = true;
            videoPlayer.Stop();
            suppressNextOnStop = false;

            var playbackUrl = resolvedUrl;
            activePlaybackUrl = playbackUrl;
            if (ShouldCacheRemoteVideos() && IsHttpUrl(resolvedUrl))
            {
                yield return ProbeVideoUrl(item, resolvedUrl);

                var cachePath = GetCacheFilePath(resolvedUrl);
                if (ShouldDownloadCacheFile(cachePath))
                {
                    yield return CacheRemoteVideo(item, resolvedUrl, cachePath);
                    if (ShouldDownloadCacheFile(cachePath))
                    {
                        loadRoutine = null;
                        yield break;
                    }
                }
                else
                {
                    lastVideoCacheSummary = $"cache hit {new FileInfo(cachePath).Length} bytes";
                }

                playbackUrl = ToFileUrl(cachePath);
            }
            else
            {
                lastVideoCacheSummary = IsHttpUrl(resolvedUrl) ? "cache skipped" : "cache not needed";
            }

            if (IsHttpUrl(playbackUrl))
            {
                yield return ProbeVideoUrl(item, playbackUrl);
            }

            videoPlayer.url = playbackUrl;
            activePlaybackUrl = playbackUrl;
            isPreparingVideo = true;
            pendingPlayAfterPrepare = string.Equals(state.playback, "playing", StringComparison.OrdinalIgnoreCase);
            prepareStartedAt = Time.unscaledTime;
            lastStatusRefreshAt = 0f;
            Debug.Log($"[TsukiVox Video] Preparing {playbackUrl} from raw {item.playableUrl}, resolved {resolvedUrl}");
            SetStatus($"Video: preparing {SafeTitle(item)}...\n{GetVideoDiagnostics()}");
            videoPlayer.Prepare();

            ApplyCommandIfNeeded(state);
            loadRoutine = null;
        }

        private bool ShouldCacheRemoteVideos()
        {
            return cacheRemoteVideosBeforePlayback || Application.platform == RuntimePlatform.Android;
        }

        private void ApplyPlaybackState(PlaylistState state, PlaylistItem item)
        {
            ApplyCommandIfNeeded(state);

            if (string.Equals(state.playback, "playing", StringComparison.OrdinalIgnoreCase))
            {
                if ((loadRoutine != null || isPreparingVideo) && !videoPlayer.isPrepared)
                {
                    pendingPlayAfterPrepare = autoPlayWhenPlaylistIsPlaying;
                }
                else if (autoPlayWhenPlaylistIsPlaying && !videoPlayer.isPlaying)
                {
                    videoPlayer.Play();
                }

                SetStatus(IsVideoUsable() ? $"Video: playing {SafeTitle(item)}." : $"Video: preparing {SafeTitle(item)}...\n{GetVideoDiagnostics()}");
                return;
            }

            if (videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
            }

            pendingPlayAfterPrepare = false;
            SetStatus(IsVideoUsable() ? $"Video: {SafeTitle(item)} is ready." : $"Video: preparing {SafeTitle(item)}...\n{GetVideoDiagnostics()}");
        }

        private void ApplyCommandIfNeeded(PlaylistState state)
        {
            var command = state.command;
            if (command == null || command.issuedAt <= lastAppliedCommandAt)
            {
                return;
            }

            lastAppliedCommandAt = command.issuedAt;
            if (string.Equals(command.action, PlaylistClient.ControlReplay, StringComparison.OrdinalIgnoreCase))
            {
                if (IsVideoUsable())
                {
                    videoPlayer.time = 0d;
                }

                sentNextForCurrentClip = false;
            }
        }

        private void HandlePrepared(VideoPlayer source)
        {
            isPreparingVideo = false;
            hasPreparedFirstFrame = true;
            FitScreenToVideo(source.width, source.height);
            SynchronizeVideoSurface();
            Debug.Log($"[TsukiVox Video] Prepared {activePlayableUrl} ({source.width}x{source.height}, {source.length:0.0}s)");

            if (pendingPlayAfterPrepare && autoPlayWhenPlaylistIsPlaying)
            {
                pendingPlayAfterPrepare = false;
                source.Play();
            }

            var state = playlistPrototype != null ? playlistPrototype.CurrentState : null;
            if (state != null)
            {
                ApplyPlaybackState(state, state.CurrentItem);
            }
            else
            {
                SetStatus("Video: ready.");
            }
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            isPreparingVideo = false;
            Debug.LogWarning($"[TsukiVox Video] Error for raw {activeRawPlayableUrl}, resolved {activePlayableUrl}, playback {activePlaybackUrl}: {message}");
            SetStatus($"Video error: {message}\n{GetUrlDiagnostics(activePlaybackUrl)}");
        }

        private void HandleFrameReady(VideoPlayer source, long frameIndex)
        {
            if (!hasPreparedFirstFrame)
            {
                Debug.Log($"[TsukiVox Video] First frame {frameIndex} for {activePlayableUrl}");
            }

            isPreparingVideo = false;
            hasPreparedFirstFrame = true;
            FitScreenToVideo(source.width, source.height);
            SynchronizeVideoSurface();
        }

        private void HandleVideoEnded(VideoPlayer source)
        {
            if (suppressNextOnStop || !hasPreparedFirstFrame)
            {
                return;
            }

            SetStatus("Video: ended.");
            if (sendNextWhenVideoEnds && !sentNextForCurrentClip && playlistPrototype != null)
            {
                sentNextForCurrentClip = true;
                sendNextRoutine = StartCoroutine(SendNextWhenPlaylistIsReady());
            }
        }

        private System.Collections.IEnumerator SendNextWhenPlaylistIsReady()
        {
            var timeout = Time.time + 3f;
            while (playlistPrototype != null && !playlistPrototype.CanSendControl && Time.time < timeout)
            {
                yield return null;
            }

            if (playlistPrototype != null && playlistPrototype.CanSendControl)
            {
                playlistPrototype.SendNext();
            }

            sendNextRoutine = null;
        }

        private void StopCurrentVideo(string status)
        {
            if (loadRoutine != null)
            {
                StopCoroutine(loadRoutine);
                loadRoutine = null;
            }

            isPreparingVideo = false;
            hasPreparedFirstFrame = false;
            activeItemId = string.Empty;
            activePlayableUrl = string.Empty;
            activeRawPlayableUrl = string.Empty;
            activePlaybackUrl = string.Empty;
            lastVideoProbeSummary = "probe pending";
            sentNextForCurrentClip = false;
            if (videoPlayer != null)
            {
                suppressNextOnStop = true;
                videoPlayer.Stop();
                suppressNextOnStop = false;
            }

            SetStatus(status);
        }

        private void SynchronizeVideoSurface()
        {
            if (videoPlayer == null || screenMaterial == null)
            {
                return;
            }

            var texture = renderTexture != null ? renderTexture : videoPlayer.texture;
            if (texture != null)
            {
                SetScreenTexture(texture);
            }
        }

        private void SetScreenTexture(Texture texture)
        {
            if (screenMaterial == null || texture == null)
            {
                return;
            }

            if (screenMaterial.HasProperty(MainTextureProperty))
            {
                screenMaterial.SetTexture(MainTextureProperty, texture);
            }
            else if (screenMaterial.HasProperty("_BaseMap"))
            {
                screenMaterial.SetTexture("_BaseMap", texture);
            }
            else
            {
                screenMaterial.mainTexture = texture;
            }

            if (screenImage != null)
            {
                screenImage.texture = texture;
            }
        }

        private void UpdatePlaybackDiagnostics()
        {
            if (videoPlayer == null || !IsVideoUsable())
            {
                return;
            }

            if (!hasPreparedFirstFrame)
            {
                Debug.Log($"[TsukiVox Video] Video became usable without prepare event: {GetVideoDiagnostics()}");
            }

            isPreparingVideo = false;
            hasPreparedFirstFrame = true;

            if (Time.unscaledTime - lastStatusRefreshAt < 0.5f)
            {
                return;
            }

            lastStatusRefreshAt = Time.unscaledTime;
            var state = playlistPrototype != null ? playlistPrototype.CurrentState : null;
            var item = state?.CurrentItem;
            if (item == null)
            {
                return;
            }

            if (string.Equals(state.playback, "playing", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus($"Video: playing {SafeTitle(item)}.\n{GetVideoDiagnostics()}");
            }
            else
            {
                SetStatus($"Video: {SafeTitle(item)} is ready.\n{GetVideoDiagnostics()}");
            }
        }

        private bool IsVideoUsable()
        {
            if (videoPlayer == null)
            {
                return false;
            }

            return videoPlayer.isPrepared ||
                   hasPreparedFirstFrame ||
                   videoPlayer.frame > 0 ||
                   videoPlayer.time > 0.05d;
        }

        private string GetVideoDiagnostics()
        {
            if (videoPlayer == null)
            {
                return "player missing";
            }

            var videoTexture = videoPlayer.texture;
            var videoTextureInfo = videoTexture == null ? "vpTex none" : $"vpTex {videoTexture.width}x{videoTexture.height}";
            var renderTextureInfo = renderTexture == null ? "rt none" : $"rt {renderTexture.width}x{renderTexture.height}";
            var prepareElapsed = isPreparingVideo ? $"{Time.unscaledTime - prepareStartedAt:0.0}s" : "idle";
            return $"{GetUrlDiagnostics(activePlaybackUrl)}\nprepared {videoPlayer.isPrepared} preparing {isPreparingVideo} prep {prepareElapsed} pendingPlay {pendingPlayAfterPrepare} playing {videoPlayer.isPlaying} frame {videoPlayer.frame} time {videoPlayer.time:0.0}s {videoTextureInfo} {renderTextureInfo}\n{lastVideoProbeSummary}\n{lastVideoCacheSummary}";
        }

        private void FitScreenToVideo(ulong width, ulong height)
        {
            var videoAspect = width > 0 && height > 0
                ? (float)width / height
                : (float)DefaultTextureWidth / DefaultTextureHeight;

            var displayWidth = screenSafeSize.x;
            var displayHeight = displayWidth / videoAspect;
            if (displayHeight > screenSafeSize.y)
            {
                displayHeight = screenSafeSize.y;
                displayWidth = displayHeight * videoAspect;
            }

            if (screenRenderer != null)
            {
                screenRenderer.transform.localScale = new Vector3(displayWidth, displayHeight, 1f);
            }

            if (screenCanvasRect != null)
            {
                var scale = screenSafeSize.x / DefaultTextureWidth;
                screenCanvasRect.sizeDelta = new Vector2(displayWidth / scale, displayHeight / scale);
            }
        }

        private void SetStatus(string message)
        {
            lastStatusMessage = message;
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        public void CopyDebugInfoToClipboard()
        {
            var debugInfo = BuildDebugInfo();
            CopyToClipboard(debugInfo);
            SetStatus($"Video: debug info copied.\n{GetVideoDiagnostics()}");
            Debug.Log($"[TsukiVox Video] Copied debug info:\n{debugInfo}");
        }

        public string GetDebugInfo()
        {
            return BuildDebugInfo();
        }

        private string BuildDebugInfo()
        {
            debugBuilder.Clear();
            debugBuilder.AppendLine("TsukiVox V0.3 Video Debug");
            debugBuilder.AppendLine($"utc {DateTime.UtcNow:O}");
            debugBuilder.AppendLine($"platform {Application.platform}");
            debugBuilder.AppendLine($"unity {Application.unityVersion}");
            debugBuilder.AppendLine($"persistentDataPath {Application.persistentDataPath}");
            debugBuilder.AppendLine($"status {lastStatusMessage}");

            var state = playlistPrototype != null ? playlistPrototype.CurrentState : null;
            var item = state?.CurrentItem;
            debugBuilder.AppendLine($"playlistConnected {playlistPrototype?.IsConnected}");
            debugBuilder.AppendLine($"playlistOrigin {playlistPrototype?.PlaylistOrigin}");
            debugBuilder.AppendLine($"downloadOrigin {playlistPrototype?.DownloadOrigin}");
            debugBuilder.AppendLine($"playbackState {state?.playback}");
            debugBuilder.AppendLine($"currentIndex {state?.currentIndex}");
            debugBuilder.AppendLine($"itemId {item?.id}");
            debugBuilder.AppendLine($"itemTitle {item?.title}");
            debugBuilder.AppendLine($"itemStatus {item?.status}");
            debugBuilder.AppendLine($"itemMessage {item?.message}");
            debugBuilder.AppendLine($"itemPlayableUrl {item?.playableUrl}");
            debugBuilder.AppendLine($"activeRawUrl {activeRawPlayableUrl}");
            debugBuilder.AppendLine($"activeResolvedUrl {activePlayableUrl}");
            debugBuilder.AppendLine($"activePlaybackUrl {activePlaybackUrl}");
            debugBuilder.AppendLine($"probe {lastVideoProbeSummary}");
            debugBuilder.AppendLine($"cache {lastVideoCacheSummary}");

            var cachePath = IsHttpUrl(activePlayableUrl) ? GetCacheFilePath(activePlayableUrl) : string.Empty;
            if (!string.IsNullOrWhiteSpace(cachePath))
            {
                debugBuilder.AppendLine($"cachePath {cachePath}");
                debugBuilder.AppendLine($"cacheExists {File.Exists(cachePath)}");
                debugBuilder.AppendLine($"cacheLength {(File.Exists(cachePath) ? new FileInfo(cachePath).Length : 0)}");
            }

            if (videoPlayer == null)
            {
                debugBuilder.AppendLine("videoPlayer missing");
            }
            else
            {
                debugBuilder.AppendLine($"vpUrl {videoPlayer.url}");
                debugBuilder.AppendLine($"vpPrepared {videoPlayer.isPrepared}");
                debugBuilder.AppendLine($"vpPlaying {videoPlayer.isPlaying}");
                debugBuilder.AppendLine($"vpFrame {videoPlayer.frame}");
                debugBuilder.AppendLine($"vpTime {videoPlayer.time:0.000}");
                debugBuilder.AppendLine($"vpLength {videoPlayer.length:0.000}");
                debugBuilder.AppendLine($"vpWidth {videoPlayer.width}");
                debugBuilder.AppendLine($"vpHeight {videoPlayer.height}");
                debugBuilder.AppendLine($"vpFrameRate {videoPlayer.frameRate:0.000}");
                debugBuilder.AppendLine($"vpCanSetTime {videoPlayer.canSetTime}");
                debugBuilder.AppendLine($"vpCanSetPlaybackSpeed {videoPlayer.canSetPlaybackSpeed}");
                debugBuilder.AppendLine($"vpCanStep {videoPlayer.canStep}");
                debugBuilder.AppendLine($"vpAspectRatio {videoPlayer.aspectRatio}");
                debugBuilder.AppendLine($"vpAudioTrackCount {videoPlayer.audioTrackCount}");
                debugBuilder.AppendLine($"vpControlledAudioTrackCount {videoPlayer.controlledAudioTrackCount}");
                var texture = videoPlayer.texture;
                debugBuilder.AppendLine(texture == null ? "vpTexture none" : $"vpTexture {texture.width}x{texture.height}");
            }

            debugBuilder.AppendLine(renderTexture == null ? "renderTexture none" : $"renderTexture {renderTexture.width}x{renderTexture.height} created {renderTexture.IsCreated()}");
            return debugBuilder.ToString();
        }

        private string ResolveVideoPlaybackUrl(string playableUrl)
        {
            if (string.IsNullOrWhiteSpace(playableUrl))
            {
                return string.Empty;
            }

            var resolvedUrl = playlistPrototype != null ? playlistPrototype.ResolvePlayableUrl(playableUrl) : playableUrl.Trim();
            if (!string.IsNullOrWhiteSpace(resolvedUrl) &&
                resolvedUrl.TrimStart().StartsWith("/", StringComparison.Ordinal) &&
                playlistPrototype != null &&
                !string.IsNullOrWhiteSpace(playlistPrototype.DownloadOrigin))
            {
                resolvedUrl = CombineUrl(playlistPrototype.DownloadOrigin, resolvedUrl.Trim());
            }

            return resolvedUrl;
        }

        private string GetUrlDiagnostics(string playbackUrl)
        {
            if (string.IsNullOrWhiteSpace(activeRawPlayableUrl))
            {
                return $"url {ShortUrl(playbackUrl)}";
            }

            return $"raw {ShortUrl(activeRawPlayableUrl)}\nresolved {ShortUrl(activePlayableUrl)}\nplayback {ShortUrl(playbackUrl)}";
        }

        private static void CopyToClipboard(string text)
        {
            TsukiVoxClipboard.CopyPlainText("TsukiVox Video Debug", text);
        }

        private static bool IsSupportedVideoUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                return false;
            }

            var path = parsed.AbsolutePath;
            return path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldDownloadCacheFile(string cachePath)
        {
            if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
            {
                return true;
            }

            var length = new FileInfo(cachePath).Length;
            if (length <= 0)
            {
                return true;
            }

            return lastProbeContentLength > 0 && length != lastProbeContentLength;
        }

        private IEnumerator CacheRemoteVideo(PlaylistItem item, string sourceUrl, string cachePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
            var temporaryPath = $"{cachePath}.download";
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            lastVideoCacheSummary = "cache starting";
            SetStatus($"Video: caching {SafeTitle(item)}...\n{GetVideoDiagnostics()}");
            using (var request = UnityWebRequest.Get(sourceUrl))
            {
                request.timeout = Mathf.CeilToInt(downloadTimeoutSeconds);
                request.downloadHandler = new DownloadHandlerFile(temporaryPath)
                {
                    removeFileOnAbort = true,
                };

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    var progress = request.downloadProgress > 0f ? request.downloadProgress : 0f;
                    var downloadedBytes = request.downloadedBytes;
                    lastVideoCacheSummary = $"caching {progress:P0} {downloadedBytes} bytes";
                    SetStatus($"Video: caching {SafeTitle(item)} {progress:P0}\n{GetVideoDiagnostics()}");
                    yield return null;
                }

                if (!IsRequestSuccessful(request))
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }

                    var error = string.IsNullOrWhiteSpace(request.error) ? $"HTTP {request.responseCode}" : request.error;
                    lastVideoCacheSummary = $"cache failed: {error}";
                    Debug.LogWarning($"[TsukiVox Video] Cache failed for {sourceUrl}: {error}");
                    SetStatus($"Video: cache failed: {error}\n{GetVideoDiagnostics()}");
                    yield break;
                }
            }

            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }

            File.Move(temporaryPath, cachePath);
            var cachedLength = new FileInfo(cachePath).Length;
            lastVideoCacheSummary = $"cache ready {cachedLength} bytes";
            Debug.Log($"[TsukiVox Video] Cached {sourceUrl} to {cachePath} ({cachedLength} bytes)");
        }

        private IEnumerator ProbeVideoUrl(PlaylistItem item, string videoUrl)
        {
            lastVideoProbeSummary = $"probing {ShortUrl(videoUrl)}";
            SetStatus($"Video: probing {SafeTitle(item)}...\n{ShortUrl(videoUrl)}");

            using (var request = UnityWebRequest.Head(videoUrl))
            {
                request.timeout = 6;
                yield return request.SendWebRequest();

                if (!IsRequestSuccessful(request))
                {
                    var error = string.IsNullOrWhiteSpace(request.error) ? $"HTTP {request.responseCode}" : request.error;
                    lastVideoProbeSummary = $"probe failed: {error}";
                    Debug.LogWarning($"[TsukiVox Video] Probe failed for {videoUrl}: {error}");
                    yield break;
                }

                var contentType = request.GetResponseHeader("Content-Type");
                var contentLength = request.GetResponseHeader("Content-Length");
                var acceptRanges = request.GetResponseHeader("Accept-Ranges");
                contentType = string.IsNullOrWhiteSpace(contentType) ? "type unknown" : contentType;
                contentLength = string.IsNullOrWhiteSpace(contentLength) ? "length unknown" : contentLength;
                acceptRanges = string.IsNullOrWhiteSpace(acceptRanges) ? "ranges unknown" : acceptRanges;
                lastProbeContentLength = long.TryParse(contentLength, out var parsedLength) ? parsedLength : -1;
                lastVideoProbeSummary = $"probe HTTP {request.responseCode} {contentType} {contentLength} {acceptRanges}";
                Debug.Log($"[TsukiVox Video] Probe ok for {videoUrl}: {lastVideoProbeSummary}");
            }
        }

        private static bool IsRequestSuccessful(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success &&
                   request.responseCode >= 200 &&
                   request.responseCode < 300;
        }

        private static string CombineUrl(string origin, string path)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                return path;
            }

            var normalizedOrigin = origin.Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(path))
            {
                return normalizedOrigin;
            }

            var normalizedPath = path.Trim();
            return normalizedPath.StartsWith("/", StringComparison.Ordinal)
                ? $"{normalizedOrigin}{normalizedPath}"
                : $"{normalizedOrigin}/{normalizedPath}";
        }

        private static bool IsHttpUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
                   (string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetCacheFilePath(string url)
        {
            Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, "video-cache"));
            var extension = ".mp4";
            if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                var parsedExtension = Path.GetExtension(parsed.AbsolutePath);
                if (string.Equals(parsedExtension, ".mp4", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parsedExtension, ".webm", StringComparison.OrdinalIgnoreCase))
                {
                    extension = parsedExtension.ToLowerInvariant();
                }
            }

            return Path.Combine(Application.persistentDataPath, "video-cache", $"{StableHash(url)}{extension}");
        }

        private static Button FindOrCreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, int fontSize)
        {
            var existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent<Button>(out var existingButton))
            {
                if (existing.TryGetComponent<RectTransform>(out var existingRect))
                {
                    existingRect.anchoredPosition = position;
                    existingRect.sizeDelta = size;
                }

                SetButtonLabel(existingButton, label);
                return existingButton;
            }

            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.1f, 0.16f, 0.18f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateSelectableColors();

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(buttonObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.93f, 0.97f, 0.98f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.sizeDelta = size;
            text.rectTransform.anchoredPosition = Vector2.zero;
            return button;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private static ColorBlock CreateSelectableColors()
        {
            return new ColorBlock
            {
                normalColor = new Color(0.1f, 0.16f, 0.18f),
                highlightedColor = new Color(0.16f, 0.3f, 0.32f),
                pressedColor = new Color(0.28f, 0.95f, 0.72f),
                selectedColor = new Color(0.18f, 0.38f, 0.4f),
                disabledColor = new Color(0.08f, 0.1f, 0.11f, 0.55f),
                colorMultiplier = 1f,
                fadeDuration = 0.05f,
            };
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                var hash = 14695981039346656037UL;
                for (var i = 0; i < value.Length; i += 1)
                {
                    hash ^= value[i];
                    hash *= 1099511628211UL;
                }

                return hash.ToString("x16");
            }
        }

        private static string ToFileUrl(string path)
        {
            return new Uri(path).AbsoluteUri;
        }

        private static string ShortUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            return url.Length <= 72 ? url : $"{url[..34]}...{url[^34..]}";
        }

        private static string SafeTitle(PlaylistItem item)
        {
            return item == null || string.IsNullOrWhiteSpace(item.title) ? "current song" : item.title.Trim();
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                color = color,
            };
            return material;
        }

        private static void DestroyCollider(GameObject target)
        {
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
        }
    }
}
