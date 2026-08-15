using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
        private const string BuiltInDefaultVideoResourcePath = "DefaultMedia/BV1Kx4y1h7vR-p1-off-vocal";
        private const string BuiltInDefaultItemId = "builtin-default-BV1Kx4y1h7vR-p1";
        private const string BuiltInDefaultTitle = "BV1Kx4y1h7vR P1 (off vocal)";
        private const float StallDetectionSeconds = 1.75f;
        private const float StallWindowSeconds = 30f;
        private const float StallFallbackSeconds = 3f;
        private const float StallProgressEpsilonSeconds = 0.08f;
        private const float StallSuppressionSeconds = 2f;
        private const float DefaultVideoVolume = 0.75f;
        private const float VideoPreferencesSaveDelay = 0.5f;
        private const string VideoVolumePrefsKey = "TsukiVox.Video.Volume.v1";

        private static readonly Vector3 DefaultScreenPosition = new Vector3(0f, 3.35f, 3.45f);
        private static readonly Vector2 DefaultScreenSafeSize = new Vector2(3.25f, 1.83f);
        private static readonly Vector2 DefaultScreenMatteSize = new Vector2(3.5f, 2.03f);

        [Header("Playlist")]
        [SerializeField] private QuestPlaylistPrototype playlistPrototype;
        [SerializeField] private bool autoPlayWhenPlaylistIsPlaying = true;
        [SerializeField] private bool sendNextWhenVideoEnds = true;

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float videoVolume = DefaultVideoVolume;

        [Header("Screen")]
        [SerializeField] private RawImage screenImage;
        [SerializeField] private MeshRenderer screenRenderer;
        [SerializeField] private MeshRenderer matteRenderer;
        [SerializeField] private Text statusText;
        [SerializeField] private Button copyDebugButton;
        // The legacy world-space status overlay stays disabled for consumer builds.
        // Diagnostics are surfaced inside the coffee-table control panel instead.
        [SerializeField] private bool showStatusOverlay;
        [SerializeField] private bool allowWorldStatusOverlay;
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
        [SerializeField, Min(30f)] private float downloadTimeoutSeconds = 900f;

        private readonly StringBuilder debugBuilder = new StringBuilder(2048);
        private readonly List<PlaybackStall> recentStalls = new List<PlaybackStall>();
        private GameObject statusCanvasObject;
        private VideoPlayer videoPlayer;
        private AudioSource videoAudioSource;
        private RenderTexture renderTexture;
        private Material screenMaterial;
        private Material matteMaterial;
        private RectTransform screenCanvasRect;
        private string activeMediaIdentity;
        private string activeItemId;
        private string activePlayableUrl;
        private string activeRawPlayableUrl;
        private string activePlaybackUrl;
        private string latestStreamUrl;
        private string latestCacheUrl;
        private string activeTransport = "idle";
        private string cachePhase = "idle";
        private string lastFallbackReason = "none";
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
        private bool isCachingVideo;
        private bool cacheAuthenticationFailure;
        private bool hlsFallbackAttempted;
        private bool signedUrlRetryAttempted;
        private bool useMp4StreamingFallback;
        private bool stallFallbackTriggered;
        private float cacheProgress;
        private float lastPlaybackTime;
        private float lastPlaybackProgressAt;
        private float stallStartedAt = -1f;
        private float suppressStallDetectionUntil;
        private double resumeTimeAfterPrepare = -1d;
        private float prepareStartedAt;
        private float lastStatusRefreshAt;
        private Coroutine sendNextRoutine;
        private Coroutine loadRoutine;
        private UnityWebRequest activeCacheRequest;
        private VideoClip builtInDefaultVideoClip;
        private bool isPlayingBuiltInDefault;
        private bool builtInDefaultPlaybackFailed;
        private string builtInDefaultPlaylistItemId = string.Empty;
        private bool videoPreferencesDirty;
        private float videoPreferencesSaveAt;

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
            videoVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VideoVolumePrefsKey, DefaultVideoVolume));
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
                AbortActiveCacheRequest();
                StopCoroutine(loadRoutine);
                loadRoutine = null;
            }
            isCachingVideo = false;
        }

        private void OnDestroy()
        {
            SavePendingVideoPreferences();
            if (renderTexture != null)
            {
                renderTexture.Release();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SavePendingVideoPreferences();
            }
        }

        private void OnApplicationQuit()
        {
            SavePendingVideoPreferences();
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
                HandlePlaybackFailure("Prepare timed out.", false);
            }

            SynchronizeVideoSurface();
            UpdatePlaybackDiagnostics();
            MonitorPlaybackStalls();
            SaveVideoPreferencesIfDue();
        }

        private void ConfigureSceneReferences()
        {
            showStatusOverlay = showStatusOverlay && allowWorldStatusOverlay;
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

        public bool IsPlayingBuiltInDefault => isPlayingBuiltInDefault;

        public bool IsBuiltInDefaultPreparing => isPlayingBuiltInDefault && isPreparingVideo;

        public bool IsBuiltInDefaultPaused =>
            isPlayingBuiltInDefault &&
            !isPreparingVideo &&
            videoPlayer != null &&
            videoPlayer.isPrepared &&
            !videoPlayer.isPlaying;

        public bool IsCachingVideo => isCachingVideo;

        public float CacheProgress => cacheProgress;

        public float VideoVolume => videoVolume;

        public string CachePhase => cachePhase;

        public string ActiveItemId => activeItemId;

        public void SetVideoVolume(float value)
        {
            videoVolume = Mathf.Clamp01(value);
            if (videoAudioSource != null)
            {
                videoAudioSource.volume = videoVolume;
            }

            PlayerPrefs.SetFloat(VideoVolumePrefsKey, videoVolume);
            videoPreferencesDirty = true;
            videoPreferencesSaveAt = Time.unscaledTime + VideoPreferencesSaveDelay;
        }

        public void RestoreDefaultVolume()
        {
            SetVideoVolume(DefaultVideoVolume);
        }

        public void SetStatusOverlayVisible(bool visible)
        {
            showStatusOverlay = visible && allowWorldStatusOverlay;
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
            videoAudioSource.volume = videoVolume;

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

        private void SaveVideoPreferencesIfDue()
        {
            if (videoPreferencesDirty && Time.unscaledTime >= videoPreferencesSaveAt)
            {
                SavePendingVideoPreferences();
            }
        }

        private void SavePendingVideoPreferences()
        {
            if (!videoPreferencesDirty)
            {
                return;
            }

            PlayerPrefs.Save();
            videoPreferencesDirty = false;
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
            statusText.color = new Color32(218, 214, 205, 230);
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
            if (isPlayingBuiltInDefault)
            {
                if (state == null)
                {
                    return;
                }

                var incomingItemId = state.CurrentItem?.id ?? string.Empty;
                if (string.Equals(incomingItemId, builtInDefaultPlaylistItemId, StringComparison.Ordinal))
                {
                    return;
                }

                isPlayingBuiltInDefault = false;
                builtInDefaultPlaylistItemId = string.Empty;
            }

            if (state == null)
            {
                StopCurrentVideo("Video: waiting for playlist sync.");
                return;
            }

            var item = state.CurrentItem;
            if (item == null)
            {
                StopCurrentVideo("Video: no song selected.");
                return;
            }

            if (string.Equals(item.status, DirectPlaylist.StatusDownloading, StringComparison.OrdinalIgnoreCase))
            {
                StopCurrentVideo($"Video: waiting for {SafeTitle(item)} to finish downloading.");
                return;
            }

            if (string.Equals(item.status, DirectPlaylist.StatusError, StringComparison.OrdinalIgnoreCase))
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

            var resolvedCacheUrl = ResolveVideoPlaybackUrl(
                string.IsNullOrWhiteSpace(item.cacheUrl) ? item.playableUrl : item.cacheUrl);
            var resolvedStreamUrl = ResolveVideoPlaybackUrl(item.streamUrl);
            if (string.IsNullOrWhiteSpace(resolvedCacheUrl) && string.IsNullOrWhiteSpace(resolvedStreamUrl))
            {
                StopCurrentVideo($"Video: {SafeTitle(item)} has no playable URL.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(resolvedStreamUrl) && !IsSupportedVideoUrl(resolvedStreamUrl))
            {
                resolvedStreamUrl = string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(resolvedCacheUrl) && !IsSupportedVideoUrl(resolvedCacheUrl))
            {
                StopCurrentVideo("Video: supported sources are HLS, MP4, and WebM.");
                return;
            }

            latestStreamUrl = resolvedStreamUrl;
            latestCacheUrl = resolvedCacheUrl;
            var mediaIdentity = BuildMediaIdentity(item, resolvedCacheUrl, resolvedStreamUrl);
            var needsLoad = !string.Equals(activeMediaIdentity, mediaIdentity, StringComparison.Ordinal);
            if (needsLoad)
            {
                LoadVideo(item, resolvedStreamUrl, resolvedCacheUrl, state, false, 0d, true);
                return;
            }

            activeItemId = item.id;
            ApplyPlaybackState(state, item);
        }

        private void LoadVideo(
            PlaylistItem item,
            string resolvedStreamUrl,
            string resolvedCacheUrl,
            PlaylistState state,
            bool forceCache,
            double resumeTime,
            bool resetFallbacks)
        {
            if (loadRoutine != null)
            {
                AbortActiveCacheRequest();
                StopCoroutine(loadRoutine);
            }

            if (resetFallbacks)
            {
                activeMediaIdentity = BuildMediaIdentity(item, resolvedCacheUrl, resolvedStreamUrl);
                hlsFallbackAttempted = false;
                signedUrlRetryAttempted = false;
                useMp4StreamingFallback = false;
                stallFallbackTriggered = false;
                lastFallbackReason = "none";
                recentStalls.Clear();
            }

            activeItemId = item.id;
            latestStreamUrl = resolvedStreamUrl;
            latestCacheUrl = resolvedCacheUrl;
            lastVideoProbeSummary = "probe pending";
            lastVideoCacheSummary = "cache pending";
            lastProbeContentLength = -1;
            sentNextForCurrentClip = false;
            hasPreparedFirstFrame = false;
            isPreparingVideo = false;
            pendingPlayAfterPrepare = false;
            resumeTimeAfterPrepare = Math.Max(0d, resumeTime);
            suppressStallDetectionUntil = Time.unscaledTime + StallSuppressionSeconds;
            ResetStallObservation();
            loadRoutine = StartCoroutine(LoadVideoRoutine(
                item,
                resolvedStreamUrl,
                resolvedCacheUrl,
                state,
                forceCache));
        }

        private IEnumerator LoadVideoRoutine(
            PlaylistItem item,
            string resolvedStreamUrl,
            string resolvedCacheUrl,
            PlaylistState state,
            bool forceCache)
        {
            suppressNextOnStop = true;
            videoPlayer.Stop();
            suppressNextOnStop = false;
            isPlayingBuiltInDefault = false;
            builtInDefaultPlaylistItemId = string.Empty;
            videoPlayer.isLooping = false;
            videoPlayer.clip = null;
            videoPlayer.source = VideoSource.Url;

            var shouldCache = forceCache || ShouldCacheRemoteVideos();
            var playbackUrl = shouldCache
                ? resolvedCacheUrl
                : useMp4StreamingFallback || string.IsNullOrWhiteSpace(resolvedStreamUrl)
                    ? resolvedCacheUrl
                    : resolvedStreamUrl;
            if (string.IsNullOrWhiteSpace(playbackUrl))
            {
                isCachingVideo = false;
                loadRoutine = null;
                SetStatus($"Video: {SafeTitle(item)} has no compatible playback source.");
                yield break;
            }

            activePlayableUrl = playbackUrl;
            activeRawPlayableUrl = shouldCache || useMp4StreamingFallback || string.IsNullOrWhiteSpace(resolvedStreamUrl)
                ? (string.IsNullOrWhiteSpace(item.cacheUrl) ? item.playableUrl : item.cacheUrl)
                : item.streamUrl;
            activePlaybackUrl = playbackUrl;
            activeTransport = DescribeTransport(playbackUrl, shouldCache);
            if (shouldCache && IsHttpUrl(resolvedCacheUrl))
            {
                isCachingVideo = true;
                cacheProgress = 0f;
                cachePhase = "probing";
                yield return ProbeVideoUrl(item, resolvedCacheUrl);

                var cachePath = GetCacheFilePath(resolvedCacheUrl);
                if (ShouldDownloadCacheFile(cachePath))
                {
                    yield return CacheRemoteVideo(item, resolvedCacheUrl, cachePath);
                    if (cacheAuthenticationFailure &&
                        !signedUrlRetryAttempted &&
                        !string.IsNullOrWhiteSpace(latestCacheUrl) &&
                        !string.Equals(latestCacheUrl, resolvedCacheUrl, StringComparison.Ordinal))
                    {
                        signedUrlRetryAttempted = true;
                        resolvedCacheUrl = latestCacheUrl;
                        activePlayableUrl = resolvedCacheUrl;
                        lastFallbackReason = "Refreshed an expired cache URL.";
                        yield return ProbeVideoUrl(item, resolvedCacheUrl);
                        yield return CacheRemoteVideo(item, resolvedCacheUrl, cachePath);
                    }
                    if (ShouldDownloadCacheFile(cachePath))
                    {
                        loadRoutine = null;
                        yield break;
                    }
                }
                else
                {
                    isCachingVideo = false;
                    cacheProgress = 1f;
                    cachePhase = "ready";
                    lastVideoCacheSummary = $"cache hit {new FileInfo(cachePath).Length} bytes";
                }

                playbackUrl = ToFileUrl(cachePath);
                activeTransport = "quest-cache";
            }
            else
            {
                isCachingVideo = false;
                cachePhase = IsHttpUrl(playbackUrl) ? "streaming" : "not needed";
                lastVideoCacheSummary = IsHttpUrl(playbackUrl) ? "cache skipped" : "cache not needed";
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
            Debug.Log($"[TsukiVox Video] Preparing {SanitizeMediaUrl(playbackUrl)} via {activeTransport}.");
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
            if (string.Equals(command.action, DirectPlaylist.ControlReplay, StringComparison.OrdinalIgnoreCase))
            {
                if (IsVideoUsable())
                {
                    videoPlayer.time = 0d;
                    suppressStallDetectionUntil = Time.unscaledTime + StallSuppressionSeconds;
                    ResetStallObservation();
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
            if (resumeTimeAfterPrepare > 0.05d && source.canSetTime)
            {
                var maximumTime = source.length > 0.2d ? source.length - 0.1d : resumeTimeAfterPrepare;
                source.time = Math.Max(0d, Math.Min(resumeTimeAfterPrepare, maximumTime));
                Debug.Log($"[TsukiVox Video] Resuming {activeTransport} playback at {source.time:0.0}s.");
            }
            resumeTimeAfterPrepare = -1d;
            suppressStallDetectionUntil = Time.unscaledTime + StallSuppressionSeconds;
            ResetStallObservation();
            Debug.Log($"[TsukiVox Video] Prepared {SanitizeMediaUrl(activePlayableUrl)} via {activeTransport} ({source.width}x{source.height}, {source.length:0.0}s)");

            if (isPlayingBuiltInDefault)
            {
                pendingPlayAfterPrepare = false;
                source.Play();
                SetStatus($"Video: playing built-in default {BuiltInDefaultTitle}.");
                return;
            }

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
            if (isPlayingBuiltInDefault)
            {
                HandleBuiltInDefaultFailure(message);
                return;
            }
            HandlePlaybackFailure(message, IsAuthenticationFailure(message));
        }

        private void HandleFrameReady(VideoPlayer source, long frameIndex)
        {
            if (!hasPreparedFirstFrame)
            {
                Debug.Log($"[TsukiVox Video] First frame {frameIndex} for {SanitizeMediaUrl(activePlayableUrl)}");
            }

            isPreparingVideo = false;
            hasPreparedFirstFrame = true;
            lastPlaybackTime = (float)source.time;
            lastPlaybackProgressAt = Time.unscaledTime;
            FitScreenToVideo(source.width, source.height);
            SynchronizeVideoSurface();
        }

        private void HandleVideoEnded(VideoPlayer source)
        {
            if (suppressNextOnStop || !hasPreparedFirstFrame)
            {
                return;
            }

            if (isPlayingBuiltInDefault)
            {
                isPlayingBuiltInDefault = false;
                builtInDefaultPlaylistItemId = string.Empty;
                var state = playlistPrototype != null ? playlistPrototype.CurrentState : null;
                if (state?.CurrentItem != null)
                {
                    ApplyPlaylistState(state);
                }
                else
                {
                    StopCurrentVideo("Video: built-in demo ended.");
                }
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

        private void HandlePlaybackFailure(string message, bool authenticationFailure)
        {
            var safeMessage = SanitizeDiagnosticText(message);
            if (isPlayingBuiltInDefault)
            {
                HandleBuiltInDefaultFailure(safeMessage);
                return;
            }

            Debug.LogWarning(
                $"[TsukiVox Video] {activeTransport} failure for {SanitizeMediaUrl(activePlaybackUrl)}: {safeMessage}");

            var state = playlistPrototype != null ? playlistPrototype.CurrentState : null;
            var item = state?.CurrentItem;
            if (item == null || !string.Equals(item.id, activeItemId, StringComparison.Ordinal))
            {
                SetStatus($"Video error: {safeMessage}\n{GetUrlDiagnostics(activePlaybackUrl)}");
                return;
            }

            var resumeTime = SafePlaybackTime();
            var latestUrl = string.Equals(activeTransport, "hls", StringComparison.Ordinal)
                ? latestStreamUrl
                : latestCacheUrl;
            if ((authenticationFailure || IsAuthenticationFailure(safeMessage)) &&
                !signedUrlRetryAttempted &&
                !string.IsNullOrWhiteSpace(latestUrl) &&
                !string.Equals(latestUrl, activePlayableUrl, StringComparison.Ordinal))
            {
                signedUrlRetryAttempted = true;
                ReloadActiveMedia(false, useMp4StreamingFallback, resumeTime, "Refreshed an expired media URL.");
                return;
            }

            if (string.Equals(activeTransport, "hls", StringComparison.Ordinal) && !hlsFallbackAttempted)
            {
                hlsFallbackAttempted = true;
                ReloadActiveMedia(false, true, resumeTime, "HLS failed; switched to MP4 Range.");
                return;
            }

            SetStatus($"Video error: {safeMessage}\n{GetUrlDiagnostics(activePlaybackUrl)}");
        }

        private void HandleBuiltInDefaultFailure(string message)
        {
            var safeMessage = SanitizeDiagnosticText(message);
            builtInDefaultPlaybackFailed = true;
            isPlayingBuiltInDefault = false;
            builtInDefaultPlaylistItemId = string.Empty;
            pendingPlayAfterPrepare = false;
            SetStatus($"Video error: built-in default failed: {safeMessage}");
            Debug.LogWarning($"[TsukiVox Video] Built-in default failed: {safeMessage}");
        }

        private void ReloadActiveMedia(bool forceCache, bool forceMp4, double resumeTime, string reason)
        {
            var state = playlistPrototype != null ? playlistPrototype.CurrentState : null;
            var item = state?.CurrentItem;
            if (item == null || !item.IsReady || !string.Equals(item.id, activeItemId, StringComparison.Ordinal))
            {
                return;
            }

            useMp4StreamingFallback = forceMp4;
            lastFallbackReason = reason;
            suppressStallDetectionUntil = Time.unscaledTime + StallSuppressionSeconds;
            Debug.Log($"[TsukiVox Video] {reason} Resume target {resumeTime:0.0}s.");
            LoadVideo(item, latestStreamUrl, latestCacheUrl, state, forceCache, resumeTime, false);
        }

        private void StartFullCacheFallback(string reason)
        {
            if (isCachingVideo || loadRoutine != null || string.IsNullOrWhiteSpace(latestCacheUrl))
            {
                return;
            }

            var state = playlistPrototype != null ? playlistPrototype.CurrentState : null;
            var item = state?.CurrentItem;
            if (item == null || !item.IsReady || !string.Equals(item.id, activeItemId, StringComparison.Ordinal))
            {
                return;
            }

            stallFallbackTriggered = true;
            ReloadActiveMedia(true, true, SafePlaybackTime(), reason);
        }

        private void MonitorPlaybackStalls()
        {
            if (!ShouldMonitorPlaybackStalls())
            {
                ResetStallObservation();
                return;
            }

            var now = Time.unscaledTime;
            var playbackTime = (float)videoPlayer.time;
            if (lastPlaybackProgressAt <= 0f)
            {
                lastPlaybackTime = playbackTime;
                lastPlaybackProgressAt = now;
                return;
            }

            if (playbackTime >= lastPlaybackTime + StallProgressEpsilonSeconds)
            {
                if (stallStartedAt >= 0f)
                {
                    var duration = Mathf.Max(0f, now - stallStartedAt);
                    recentStalls.Add(new PlaybackStall(stallStartedAt, duration));
                    Debug.Log($"[TsukiVox Video] Buffering recovered after {duration:0.00}s via {activeTransport}.");
                }
                stallStartedAt = -1f;
                lastPlaybackTime = playbackTime;
                lastPlaybackProgressAt = now;
                PruneRecentStalls(now);
                return;
            }

            var timeWithoutProgress = now - lastPlaybackProgressAt;
            if (timeWithoutProgress < StallDetectionSeconds)
            {
                return;
            }

            if (stallStartedAt < 0f)
            {
                stallStartedAt = lastPlaybackProgressAt;
                Debug.Log($"[TsukiVox Video] Buffering started at {playbackTime:0.00}s via {activeTransport}.");
            }

            PruneRecentStalls(now);
            var currentDuration = now - stallStartedAt;
            var cumulativeDuration = currentDuration;
            for (var index = 0; index < recentStalls.Count; index += 1)
            {
                cumulativeDuration += recentStalls[index].duration;
            }

            SetStatus($"Video: buffering {SafeTitle(playlistPrototype?.CurrentState?.CurrentItem)}...\n{activeTransport} · {currentDuration:0.0}s");
            var stallCount = recentStalls.Count + 1;
            if (!stallFallbackTriggered && (stallCount >= 2 || cumulativeDuration >= StallFallbackSeconds))
            {
                StartFullCacheFallback(
                    $"Detected {stallCount} playback stall(s), {cumulativeDuration:0.0}s in {StallWindowSeconds:0}s.");
            }
        }

        private bool ShouldMonitorPlaybackStalls()
        {
            if (videoPlayer == null || playlistPrototype == null ||
                !Application.isFocused || isPreparingVideo || isCachingVideo || loadRoutine != null ||
                !IsHttpUrl(activePlaybackUrl) || !videoPlayer.isPrepared || !hasPreparedFirstFrame ||
                Time.unscaledTime < suppressStallDetectionUntil)
            {
                return false;
            }

            var state = playlistPrototype.CurrentState;
            if (!string.Equals(state?.playback, "playing", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return videoPlayer.length <= 0.2d || videoPlayer.time < videoPlayer.length - 0.5d;
        }

        private void PruneRecentStalls(float now)
        {
            for (var index = recentStalls.Count - 1; index >= 0; index -= 1)
            {
                if (now - recentStalls[index].startedAt > StallWindowSeconds)
                {
                    recentStalls.RemoveAt(index);
                }
            }
        }

        private void ResetStallObservation()
        {
            lastPlaybackTime = videoPlayer != null ? (float)videoPlayer.time : 0f;
            lastPlaybackProgressAt = 0f;
            stallStartedAt = -1f;
        }

        private double SafePlaybackTime()
        {
            if (videoPlayer == null || double.IsNaN(videoPlayer.time) || double.IsInfinity(videoPlayer.time))
            {
                return 0d;
            }
            return Math.Max(0d, videoPlayer.time);
        }

        private void StopCurrentVideo(string status)
        {
            if (loadRoutine != null)
            {
                AbortActiveCacheRequest();
                StopCoroutine(loadRoutine);
                loadRoutine = null;
            }

            isPreparingVideo = false;
            hasPreparedFirstFrame = false;
            isCachingVideo = false;
            cacheProgress = 0f;
            cachePhase = "idle";
            activeMediaIdentity = string.Empty;
            activeItemId = string.Empty;
            activePlayableUrl = string.Empty;
            activeRawPlayableUrl = string.Empty;
            activePlaybackUrl = string.Empty;
            latestStreamUrl = string.Empty;
            latestCacheUrl = string.Empty;
            activeTransport = "idle";
            isPlayingBuiltInDefault = false;
            builtInDefaultPlaylistItemId = string.Empty;
            lastVideoProbeSummary = "probe pending";
            sentNextForCurrentClip = false;
            recentStalls.Clear();
            ResetStallObservation();
            if (videoPlayer != null)
            {
                suppressNextOnStop = true;
                videoPlayer.Stop();
                suppressNextOnStop = false;
                videoPlayer.isLooping = false;
                videoPlayer.clip = null;
                videoPlayer.source = VideoSource.Url;
            }

            SetStatus(status);
        }

        private void AbortActiveCacheRequest()
        {
            if (activeCacheRequest == null)
            {
                return;
            }

            activeCacheRequest.Abort();
            activeCacheRequest = null;
        }

        public bool PlayBuiltInDefault()
        {
            if (videoPlayer == null)
            {
                return false;
            }

            if (isPlayingBuiltInDefault && videoPlayer.isPrepared)
            {
                if (videoPlayer.canSetTime)
                {
                    videoPlayer.time = 0d;
                }

                videoPlayer.Play();
                suppressStallDetectionUntil = Time.unscaledTime + StallSuppressionSeconds;
                ResetStallObservation();
                SetStatus($"Video: playing built-in default {BuiltInDefaultTitle}.");
                return true;
            }

            builtInDefaultVideoClip ??= Resources.Load<VideoClip>(BuiltInDefaultVideoResourcePath);
            if (builtInDefaultVideoClip == null)
            {
                builtInDefaultPlaybackFailed = true;
                Debug.LogWarning($"[TsukiVox Video] Missing built-in default at Resources/{BuiltInDefaultVideoResourcePath}.");
                return false;
            }

            builtInDefaultPlaybackFailed = false;

            if (loadRoutine != null)
            {
                AbortActiveCacheRequest();
                StopCoroutine(loadRoutine);
                loadRoutine = null;
            }

            suppressNextOnStop = true;
            videoPlayer.Stop();
            suppressNextOnStop = false;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = builtInDefaultVideoClip;
            videoPlayer.isLooping = false;

            activeMediaIdentity = BuiltInDefaultItemId;
            activeItemId = BuiltInDefaultItemId;
            activePlayableUrl = $"resource://{BuiltInDefaultVideoResourcePath}";
            activeRawPlayableUrl = activePlayableUrl;
            activePlaybackUrl = activePlayableUrl;
            latestStreamUrl = string.Empty;
            latestCacheUrl = string.Empty;
            activeTransport = "built-in";
            cachePhase = "not needed";
            cacheProgress = 1f;
            lastVideoProbeSummary = "probe not needed";
            lastVideoCacheSummary = "built into APK";
            sentNextForCurrentClip = false;
            hasPreparedFirstFrame = false;
            isCachingVideo = false;
            isPreparingVideo = true;
            pendingPlayAfterPrepare = true;
            resumeTimeAfterPrepare = 0d;
            prepareStartedAt = Time.unscaledTime;
            lastStatusRefreshAt = 0f;
            isPlayingBuiltInDefault = true;
            builtInDefaultPlaylistItemId = playlistPrototype?.CurrentState?.CurrentItem?.id ?? string.Empty;
            suppressStallDetectionUntil = Time.unscaledTime + StallSuppressionSeconds;
            ResetStallObservation();

            SetStatus($"Video: preparing built-in default {BuiltInDefaultTitle}...");
            Debug.Log($"[TsukiVox Video] Preparing built-in default {BuiltInDefaultTitle}.");
            videoPlayer.Prepare();
            return true;
        }

        public bool ToggleBuiltInDefaultPlayback()
        {
            if (!isPlayingBuiltInDefault)
            {
                return PlayBuiltInDefault();
            }

            if (videoPlayer == null || isPreparingVideo || !videoPlayer.isPrepared)
            {
                return false;
            }

            if (videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
                SetStatus($"Video: built-in default {BuiltInDefaultTitle} paused.");
                return true;
            }

            videoPlayer.Play();
            suppressStallDetectionUntil = Time.unscaledTime + StallSuppressionSeconds;
            ResetStallObservation();
            SetStatus($"Video: playing built-in default {BuiltInDefaultTitle}.");
            return true;
        }

        public bool StopBuiltInDefault()
        {
            if (!isPlayingBuiltInDefault)
            {
                return false;
            }

            StopCurrentVideo("Video: built-in demo stopped.");
            return true;
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
            return $"{GetUrlDiagnostics(activePlaybackUrl)}\ntransport {activeTransport} cache {cachePhase} {cacheProgress:P0} fallback {lastFallbackReason}\nprepared {videoPlayer.isPrepared} preparing {isPreparingVideo} prep {prepareElapsed} pendingPlay {pendingPlayAfterPrepare} playing {videoPlayer.isPlaying} frame {videoPlayer.frame} time {videoPlayer.time:0.0}s {videoTextureInfo} {renderTextureInfo}\n{lastVideoProbeSummary}\n{lastVideoCacheSummary}";
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
            debugBuilder.AppendLine($"videoVolume {videoVolume:0.000}");

            var state = playlistPrototype != null ? playlistPrototype.CurrentState : null;
            var item = state?.CurrentItem;
            debugBuilder.AppendLine($"playlistConnected {playlistPrototype?.IsConnected}");
            debugBuilder.AppendLine($"directApiOrigin {BilibiliDirectClient.ApiOrigin}");
            debugBuilder.AppendLine($"directSuggestOrigin {BilibiliDirectClient.SuggestOrigin}");
            debugBuilder.AppendLine($"playbackState {state?.playback}");
            debugBuilder.AppendLine($"currentIndex {state?.currentIndex}");
            debugBuilder.AppendLine($"itemId {item?.id}");
            debugBuilder.AppendLine($"itemTitle {item?.title}");
            debugBuilder.AppendLine($"itemStatus {item?.status}");
            debugBuilder.AppendLine($"itemMessage {item?.message}");
            debugBuilder.AppendLine($"mediaIdentity {activeMediaIdentity}");
            debugBuilder.AppendLine($"itemPlayableUrl {SanitizeMediaUrl(item?.playableUrl)}");
            debugBuilder.AppendLine($"itemStreamUrl {SanitizeMediaUrl(item?.streamUrl)}");
            debugBuilder.AppendLine($"itemCacheUrl {SanitizeMediaUrl(item?.cacheUrl)}");
            debugBuilder.AppendLine($"activeRawUrl {SanitizeMediaUrl(activeRawPlayableUrl)}");
            debugBuilder.AppendLine($"activeResolvedUrl {SanitizeMediaUrl(activePlayableUrl)}");
            debugBuilder.AppendLine($"activePlaybackUrl {SanitizeMediaUrl(activePlaybackUrl)}");
            debugBuilder.AppendLine($"transport {activeTransport}");
            debugBuilder.AppendLine($"builtInDefault {isPlayingBuiltInDefault} loaded {builtInDefaultVideoClip != null} failed {builtInDefaultPlaybackFailed}");
            debugBuilder.AppendLine($"cacheState {isCachingVideo} {cachePhase} {cacheProgress:P0}");
            debugBuilder.AppendLine($"fallback {lastFallbackReason}");
            debugBuilder.AppendLine($"recentStalls {recentStalls.Count}");
            debugBuilder.AppendLine($"probe {lastVideoProbeSummary}");
            debugBuilder.AppendLine($"cache {lastVideoCacheSummary}");

            var cachePath = IsHttpUrl(latestCacheUrl) ? GetCacheFilePath(latestCacheUrl) : string.Empty;
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
                debugBuilder.AppendLine($"vpUrl {SanitizeMediaUrl(videoPlayer.url)}");
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

            return playlistPrototype != null ? playlistPrototype.ResolvePlayableUrl(playableUrl) : playableUrl.Trim();
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
            return path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
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
            cacheAuthenticationFailure = false;
            isCachingVideo = true;
            cacheProgress = 0f;
            cachePhase = "downloading";
            SetStatus($"Video: caching {SafeTitle(item)}...\n{GetVideoDiagnostics()}");
            using (var request = UnityWebRequest.Get(sourceUrl))
            {
                activeCacheRequest = request;
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
                    cacheProgress = Mathf.Clamp01(progress);
                    lastVideoCacheSummary = $"caching {progress:P0} {downloadedBytes} bytes";
                    SetStatus($"Video: caching {SafeTitle(item)} {progress:P0}\n{GetVideoDiagnostics()}");
                    yield return null;
                }
                activeCacheRequest = null;

                if (!IsRequestSuccessful(request))
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }

                    var error = string.IsNullOrWhiteSpace(request.error) ? $"HTTP {request.responseCode}" : request.error;
                    cacheAuthenticationFailure = request.responseCode == 401 || request.responseCode == 403;
                    lastVideoCacheSummary = $"cache failed: {SanitizeDiagnosticText(error)}";
                    isCachingVideo = false;
                    cacheProgress = 0f;
                    cachePhase = "failed";
                    Debug.LogWarning($"[TsukiVox Video] Cache failed for {SanitizeMediaUrl(sourceUrl)}: {SanitizeDiagnosticText(error)}");
                    SetStatus($"Video: cache failed: {SanitizeDiagnosticText(error)}\n{GetVideoDiagnostics()}");
                    yield break;
                }
            }

            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }

            File.Move(temporaryPath, cachePath);
            var cachedLength = new FileInfo(cachePath).Length;
            isCachingVideo = false;
            cacheProgress = 1f;
            cachePhase = "ready";
            lastVideoCacheSummary = $"cache ready {cachedLength} bytes";
            Debug.Log($"[TsukiVox Video] Cached {SanitizeMediaUrl(sourceUrl)} ({cachedLength} bytes).");
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
                    lastVideoProbeSummary = $"probe failed: {SanitizeDiagnosticText(error)}";
                    Debug.LogWarning($"[TsukiVox Video] Probe failed for {SanitizeMediaUrl(videoUrl)}: {SanitizeDiagnosticText(error)}");
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
                Debug.Log($"[TsukiVox Video] Probe ok for {SanitizeMediaUrl(videoUrl)}: {lastVideoProbeSummary}");
            }
        }

        private static bool IsRequestSuccessful(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success &&
                   request.responseCode >= 200 &&
                   request.responseCode < 300;
        }

        private static bool IsHttpUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
                   (string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetCacheFilePath(string url)
        {
            Directory.CreateDirectory(Path.Combine(
                Application.persistentDataPath,
                DirectPlaylist.LegacyVideoCacheDirectoryName));
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

            return Path.Combine(
                Application.persistentDataPath,
                DirectPlaylist.LegacyVideoCacheDirectoryName,
                $"{StableHash(CanonicalizeMediaUrl(url))}{extension}");
        }

        private static string BuildMediaIdentity(
            PlaylistItem item,
            string resolvedCacheUrl,
            string resolvedStreamUrl)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(item.jobId))
            {
                return $"{item.id}|{item.jobId}";
            }
            if (!string.IsNullOrWhiteSpace(item.id))
            {
                return item.id;
            }
            return CanonicalizeMediaUrl(
                string.IsNullOrWhiteSpace(resolvedCacheUrl) ? resolvedStreamUrl : resolvedCacheUrl);
        }

        private static string DescribeTransport(string playbackUrl, bool caching)
        {
            if (caching)
            {
                return "quest-cache";
            }
            if (!Uri.TryCreate(playbackUrl, UriKind.Absolute, out var parsed))
            {
                return "unknown";
            }
            if (string.Equals(parsed.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                return "quest-cache";
            }
            if (parsed.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
            {
                return "hls";
            }
            return IsHttpUrl(playbackUrl) ? "mp4-range" : "local";
        }

        private static string CanonicalizeMediaUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                return url?.Trim() ?? string.Empty;
            }

            if (!IsHttpUrl(url))
            {
                return parsed.GetLeftPart(UriPartial.Path);
            }

            var port = parsed.IsDefaultPort ? string.Empty : $":{parsed.Port}";
            return $"{parsed.Scheme.ToLowerInvariant()}://{parsed.IdnHost.ToLowerInvariant()}{port}{parsed.AbsolutePath}";
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
            image.color = new Color32(26, 29, 36, 255);
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
            text.color = new Color32(242, 239, 232, 255);
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
                normalColor = new Color32(26, 29, 36, 255),
                highlightedColor = new Color32(60, 57, 50, 255),
                pressedColor = new Color32(184, 158, 109, 255),
                selectedColor = new Color32(78, 69, 54, 255),
                disabledColor = new Color32(21, 23, 27, 140),
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
            var safeUrl = SanitizeMediaUrl(url);
            if (string.IsNullOrWhiteSpace(safeUrl))
            {
                return string.Empty;
            }

            return safeUrl.Length <= 72 ? safeUrl : $"{safeUrl[..34]}...{safeUrl[^34..]}";
        }

        private static string SanitizeMediaUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                return string.IsNullOrWhiteSpace(url) ? string.Empty : "invalid-url";
            }
            return parsed.GetLeftPart(UriPartial.Path);
        }

        private static string SanitizeDiagnosticText(string value)
        {
            var safe = string.IsNullOrWhiteSpace(value) ? "Unknown playback error." : value.Trim();
            safe = Regex.Replace(safe, @"(?i)(signature=)[^&\s]+", "$1<redacted>");
            safe = Regex.Replace(safe, @"(?i)(expires=)\d+", "$1<redacted>");
            return safe.Replace('\r', ' ').Replace('\n', ' ');
        }

        private static bool IsAuthenticationFailure(string message)
        {
            var normalized = message?.ToLowerInvariant() ?? string.Empty;
            return normalized.Contains("401") ||
                   normalized.Contains("403") ||
                   normalized.Contains("unauthorized") ||
                   normalized.Contains("forbidden") ||
                   normalized.Contains("expired");
        }

        private static string SafeTitle(PlaylistItem item)
        {
            return item == null || string.IsNullOrWhiteSpace(item.title) ? "current song" : item.title.Trim();
        }

        private readonly struct PlaybackStall
        {
            public PlaybackStall(float startedAt, float duration)
            {
                this.startedAt = startedAt;
                this.duration = duration;
            }

            public readonly float startedAt;
            public readonly float duration;
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
