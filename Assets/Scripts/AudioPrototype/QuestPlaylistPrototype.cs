using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    public enum TsukiVoxServiceMode
    {
        Companion = 0,
        Online = 1,
        Direct = 2,
    }

    public sealed class QuestPlaylistPrototype : MonoBehaviour
    {
        public const string DefaultHelperHostAddress = "192.168.50.191";
        public const string DefaultOnlineServiceOrigin = "https://api.tsukivox.com";
        public const string LocalDevelopmentServiceOrigin = "http://192.168.50.41:8080";

        private const string LegacyOnlineServiceOrigin = "http://192.168.50.41:8080";
        private const int CurrentServiceSettingsVersion = 2;

        private const float MinimumPollIntervalSeconds = 0.25f;
        private const float MinimumRetryIntervalSeconds = 0.5f;
        private const int PlaylistPort = 5175;
        private const int DownloadPort = 5174;
        private const string HelperHostPrefsKey = "TsukiVox.HelperHost";
        private const string ServiceModePrefsKey = "TsukiVox.ServiceMode";
        private const string OnlineServiceOriginPrefsKey = "TsukiVox.OnlineServiceOrigin";
        private const string ServiceSettingsVersionPrefsKey = "TsukiVox.ServiceSettingsVersion";
        private const string DeviceTokenPrefsPrefix = "TsukiVox.DeviceToken.";
        private const string DeviceIdPrefsKey = "TsukiVox.DeviceId";

        [Header("Service Origins")]
        [SerializeField] private TsukiVoxServiceMode serviceMode = TsukiVoxServiceMode.Direct;
        [SerializeField] private string helperHost = DefaultHelperHostAddress;
        [SerializeField] private string onlineServiceOrigin = DefaultOnlineServiceOrigin;
        [SerializeField] private string playlistOrigin = "http://192.168.50.191:5175";
        [SerializeField] private string downloadOrigin = "http://192.168.50.191:5174";

        [Header("Polling")]
        [SerializeField, Min(MinimumPollIntervalSeconds)] private float pollIntervalSeconds = 1f;
        [SerializeField, Min(MinimumRetryIntervalSeconds)] private float retryIntervalSeconds = 2f;
        [SerializeField] private bool startPollingOnAwake = true;

        [Tooltip("Clear this device's queue once the service is reached after launch.")]
        [SerializeField] private bool clearQueueOnStartup = true;

        [Header("UI")]
        [SerializeField] private Text connectionText;
        [SerializeField] private Text currentSongText;
        [SerializeField] private Text queueText;
        [SerializeField] private Text playableUrlText;
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button replayButton;
        [SerializeField] private InputField helperHostInput;
        [SerializeField] private Button applyHostButton;
        [SerializeField] private Button defaultHostButton;

        private readonly StringBuilder songBuilder = new StringBuilder(512);
        private readonly StringBuilder queueBuilder = new StringBuilder(256);

        private PlaylistClient client;
        private BilibiliDirectClient directClient;
        private readonly Queue<string> directDownloadQueue = new Queue<string>();
        private Coroutine directDownloadRoutine;
        private string activeDirectDownloadItemId = string.Empty;
        private long directStateClock;
        private string deviceToken;
        private bool hasValidatedDeviceCredential;
        private PlaylistState state;
        private Coroutine pollRoutine;
        private bool isConnected;
        private bool isRequestInFlight;
        private bool lastRequestFailed;
        private string lastError = "Not connected.";
        private string pendingStatus = "Connecting to playlist sync...";
        private Coroutine searchRoutine;
        private Coroutine addItemRoutine;
        private Coroutine suggestRoutine;
        private BilibiliSearchResponse searchResults;
        private BilibiliSuggestResponse suggestResults;
        private BilibiliCatalogItem pendingAddItem;
        private BilibiliCatalogItem lastAddedItem;
        private bool isSearching;
        private bool isAddingItem;
        private bool isFetchingSuggestions;
        private int searchSequence;
        private int suggestSequence;
        private string lastSearchError = string.Empty;
        private string lastSuggestError = string.Empty;
        private string lastAddItemError = string.Empty;
        private string deviceId = string.Empty;

        private bool hasClearedQueueOnStartup;

        public event Action<QuestPlaylistPrototype, PlaylistState> StateChanged;

        public event Action<QuestPlaylistPrototype> SearchStateChanged;

        public event Action<QuestPlaylistPrototype> AddItemStateChanged;

        public event Action<QuestPlaylistPrototype> SuggestStateChanged;

        public PlaylistState CurrentState => state;

        public bool IsConnected => isConnected;

        public bool CanSendControl => isConnected && !isRequestInFlight && !isAddingItem;

        public bool IsRequestInFlight => isRequestInFlight || isAddingItem;

        public BilibiliSearchResponse SearchResults => searchResults;

        public BilibiliSuggestResponse SuggestResults => suggestResults;

        public bool IsSearching => isSearching;

        public bool IsFetchingSuggestions => isFetchingSuggestions;

        public string LastSearchError => lastSearchError;

        public string LastSuggestError => lastSuggestError;

        public bool IsAddingItem => isAddingItem;

        public bool CanAddItem => isConnected && !isAddingItem;

        public BilibiliCatalogItem PendingAddItem => pendingAddItem;

        public BilibiliCatalogItem LastAddedItem => lastAddedItem;

        public string LastAddItemError => lastAddItemError;

        public string HelperHost => helperHost;

        public TsukiVoxServiceMode ServiceMode => serviceMode;

        public bool IsOnlineService => serviceMode == TsukiVoxServiceMode.Online;

        public bool IsDirectService => serviceMode == TsukiVoxServiceMode.Direct;

        public bool IsCompanionService => serviceMode == TsukiVoxServiceMode.Companion;

        public bool SupportsServiceAddress => !IsDirectService;

        public bool IsLocalDevelopmentService => IsOnlineService &&
                                                 string.Equals(
                                                     onlineServiceOrigin,
                                                     LocalDevelopmentServiceOrigin,
                                                     StringComparison.OrdinalIgnoreCase);

        public string ServiceAddress => IsDirectService
            ? "无需服务地址"
            : IsOnlineService ? onlineServiceOrigin : helperHost;

        public string ServiceDisplayName => IsDirectService
            ? "直接请求"
            : IsLocalDevelopmentService
                ? "本地开发"
                : IsOnlineService ? "公网服务" : "局域网 Companion";

        public string LastConnectionError => lastError;

        public string ConnectionStatusMessage => pendingStatus;

        public string PlaylistOrigin => IsDirectService
            ? BilibiliDirectClient.ApiOrigin
            : client != null ? client.PlaylistOrigin : playlistOrigin;

        public string DownloadOrigin => IsDirectService
            ? BilibiliDirectClient.ApiOrigin
            : client != null ? client.DownloadOrigin : downloadOrigin;

        public static QuestPlaylistPrototype EnsureScenePrototype()
        {
            var existing = FindAnyObjectByType<QuestPlaylistPrototype>();
            if (existing != null)
            {
                existing.EnsureUiReferences();
                return existing;
            }

            var prototypeObject = new GameObject("Quest Playlist Prototype");
            var prototype = prototypeObject.AddComponent<QuestPlaylistPrototype>();
            prototype.EnsureUiReferences();
            return prototype;
        }

        private void Awake()
        {
            LoadServiceSettings();
            ApplyOriginsFromService();
            LoadDeviceCredential();
            EnsureUiReferences();
            client = new PlaylistClient(playlistOrigin, downloadOrigin, deviceId, deviceToken);
            WireUi();
            SyncHostInput();
            RefreshUi();

            if (startPollingOnAwake)
            {
                StartPolling();
            }
        }

        private void OnEnable()
        {
            if (startPollingOnAwake && pollRoutine == null && client != null)
            {
                StartPolling();
            }
        }

        private void OnDisable()
        {
            StopPolling();
            CancelCatalogRequests();
            CancelDirectDownloads();
        }

        public void StartPolling()
        {
            if (IsDirectService)
            {
                EnsureDirectState();
                if (directDownloadRoutine == null &&
                    directDownloadQueue.Count == 0 &&
                    HasPendingDirectDownloads())
                {
                    RestartDirectDownloadWorker();
                }
                return;
            }

            if (pollRoutine != null)
            {
                return;
            }

            client.SetOrigins(playlistOrigin, downloadOrigin);
            client.SetDeviceId(deviceId);
            client.SetDeviceToken(deviceToken);
            pollRoutine = StartCoroutine(PollLoop());
        }

        public void StopPolling()
        {
            if (pollRoutine == null)
            {
                return;
            }

            StopCoroutine(pollRoutine);
            pollRoutine = null;
            isRequestInFlight = false;
        }

        public void ApplyHelperHostFromUi()
        {
            ApplyHelperHost(helperHostInput != null ? helperHostInput.text : helperHost, true);
        }

        public void ApplyDefaultHelperHost()
        {
            ApplyHelperHost(DefaultHelperHostAddress, true);
        }

        public void ApplyHelperHost(string nextHelperHost)
        {
            ApplyHelperHost(nextHelperHost, true);
        }

        public void UseCompanionService()
        {
            ApplyServiceMode(TsukiVoxServiceMode.Companion, true);
        }

        public void UseDirectService()
        {
            ApplyServiceMode(TsukiVoxServiceMode.Direct, true);
        }

        public void UseOnlineService()
        {
            ApplyOnlineServiceOrigin(DefaultOnlineServiceOrigin, true);
        }

        public void UseLocalDevelopmentService()
        {
            ApplyOnlineServiceOrigin(LocalDevelopmentServiceOrigin, true);
        }

        public void ApplyServiceAddress(string nextAddress)
        {
            if (IsDirectService)
            {
                return;
            }

            if (IsOnlineService)
            {
                onlineServiceOrigin = NormalizeOnlineOrigin(nextAddress, DefaultOnlineServiceOrigin);
                PlayerPrefs.SetString(OnlineServiceOriginPrefsKey, onlineServiceOrigin);
                PlayerPrefs.Save();
                ApplyServiceConfiguration(true);
                return;
            }

            ApplyHelperHost(nextAddress, true);
        }

        public void ApplyDefaultServiceAddress()
        {
            if (IsDirectService)
            {
                return;
            }

            ApplyServiceAddress(IsOnlineService ? DefaultOnlineServiceOrigin : DefaultHelperHostAddress);
        }

        public void SendPlayPause()
        {
            var action = string.Equals(state?.playback, "playing", StringComparison.OrdinalIgnoreCase)
                ? PlaylistClient.ControlPause
                : PlaylistClient.ControlPlay;
            SendControl(action);
        }

        public void SendPrevious()
        {
            SendControl(PlaylistClient.ControlPrevious);
        }

        public void SendNext()
        {
            SendControl(PlaylistClient.ControlNext);
        }

        public void SendReplay()
        {
            SendControl(PlaylistClient.ControlReplay);
        }

        /// <summary>Starts or resumes one queue entry selected by id.</summary>
        public void PlayQueueItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }
            SendControl(PlaylistClient.ControlPlay, itemId);
        }

        /// <summary>
        /// Clears future entries while preserving playback history and the current
        /// song, so clearing never interrupts what is playing.
        /// </summary>
        public void ClearQueue()
        {
            SendControl(PlaylistClient.ControlClear);
        }

        /// <summary>Removes playback history while preserving the current and future songs.</summary>
        public void ClearPlayedQueue()
        {
            SendControl(PlaylistClient.ControlClearPlayed);
        }

        /// <summary>Removes every queue entry except the current song.</summary>
        public void ClearQueueExceptCurrent()
        {
            SendControl(PlaylistClient.ControlClearExceptCurrent);
        }

        /// <summary>Removes one queue entry by id.</summary>
        public void RemoveQueueItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }
            SendControl(PlaylistClient.ControlRemove, itemId);
        }

        /// <summary>
        /// Drops locally held search results. This is client-only state, so it needs
        /// no request; the search field keeps its text so the query can be re-run.
        /// </summary>
        public void ClearSearchResults()
        {
            CancelSearch();
            searchResults = null;
            lastSearchError = string.Empty;
            lastAddedItem = null;
            lastAddItemError = string.Empty;
            pendingAddItem = null;
            SearchStateChanged?.Invoke(this);
            AddItemStateChanged?.Invoke(this);
        }

        /// <summary>True when there is anything to clear from the search page.</summary>
        public bool HasSearchResults =>
            (searchResults != null && searchResults.ItemCount > 0) ||
            !string.IsNullOrEmpty(lastSearchError);

        /// <summary>True when the queue holds at least one item.</summary>
        public bool HasQueueItems => state != null && state.queue != null && state.queue.Length > 0;

        public void SearchBilibili(string query, int page = 1, int pageSize = 4)
        {
            EnsureClient();
            CancelSearch();

            var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Min(30, Math.Max(1, pageSize));
            searchResults = new BilibiliSearchResponse
            {
                query = normalizedQuery,
                page = normalizedPage,
                pageSize = normalizedPageSize,
            };
            lastSearchError = string.Empty;
            isSearching = true;
            searchSequence += 1;
            var sequence = searchSequence;
            SearchStateChanged?.Invoke(this);
            searchRoutine = StartCoroutine(SearchBilibiliRoutine(
                normalizedQuery,
                normalizedPage,
                normalizedPageSize,
                sequence));
        }

        public void FetchBilibiliSuggestions(string term)
        {
            EnsureClient();
            CancelSuggestions();
            lastSuggestError = string.Empty;

            var normalizedTerm = string.IsNullOrWhiteSpace(term) ? string.Empty : term.Trim();
            if (string.IsNullOrEmpty(normalizedTerm))
            {
                suggestResults = new BilibiliSuggestResponse
                {
                    code = 0,
                    result = new BilibiliSuggestResult { tag = Array.Empty<BilibiliSuggestItem>() },
                };
                isFetchingSuggestions = false;
                SuggestStateChanged?.Invoke(this);
                return;
            }

            isFetchingSuggestions = true;
            suggestSequence += 1;
            var sequence = suggestSequence;
            SuggestStateChanged?.Invoke(this);
            suggestRoutine = StartCoroutine(FetchBilibiliSuggestionsRoutine(normalizedTerm, sequence));
        }

        public void ClearSuggestions()
        {
            CancelSuggestions();
            suggestResults = null;
            lastSuggestError = string.Empty;
            SuggestStateChanged?.Invoke(this);
        }

        public void AddItem(BilibiliCatalogItem item, bool playNow = false)
        {
            if (isAddingItem)
            {
                lastAddItemError = "另一首歌曲正在加入队列，请稍候。";
                AddItemStateChanged?.Invoke(this);
                return;
            }

            if (item == null || !item.IsValid)
            {
                pendingAddItem = item;
                lastAddItemError = "请选择有效的 Bilibili 视频。";
                AddItemStateChanged?.Invoke(this);
                return;
            }

            if (IsDirectService)
            {
                AddDirectItem(item, playNow);
                return;
            }

            EnsureClient();
            pendingAddItem = item;
            lastAddItemError = string.Empty;
            isAddingItem = true;
            AddItemStateChanged?.Invoke(this);
            addItemRoutine = StartCoroutine(AddItemRoutine(item, playNow));
        }

        public string ResolveCatalogAssetUrl(string assetUrl)
        {
            EnsureClient();
            return client.ResolveCatalogAssetUrl(assetUrl);
        }

        private IEnumerator SearchBilibiliRoutine(string query, int page, int pageSize, int sequence)
        {
            BilibiliSearchResponse response = null;
            string error = null;
            if (IsDirectService)
            {
                EnsureDirectClient();
                yield return directClient.Search(
                    query,
                    page,
                    pageSize,
                    value => response = value,
                    value => error = value);
            }
            else
            {
                yield return client.SearchBilibili(
                    query,
                    page,
                    pageSize,
                    value => response = value,
                    value => error = value);
            }

            if (sequence != searchSequence)
            {
                yield break;
            }

            searchRoutine = null;
            isSearching = false;
            if (response != null)
            {
                searchResults = response;
                lastSearchError = string.Empty;
            }
            else
            {
                var diagnosticError = string.IsNullOrWhiteSpace(error) ? "Unknown search failure." : error.Trim();
                Debug.LogWarning($"[TsukiVox Search] Request failed: {diagnosticError}");
                lastSearchError = IsDirectService
                    ? diagnosticError
                    : "服务器端暂时繁忙，请稍后再试";
            }

            SearchStateChanged?.Invoke(this);
        }

        private IEnumerator FetchBilibiliSuggestionsRoutine(string term, int sequence)
        {
            BilibiliSuggestResponse response = null;
            string error = null;
            if (IsDirectService)
            {
                EnsureDirectClient();
                yield return directClient.FetchSuggestions(
                    term,
                    9,
                    value => response = value,
                    value => error = value);
            }
            else
            {
                yield return client.FetchBilibiliSuggestions(
                    term,
                    value => response = value,
                    value => error = value);
            }

            if (sequence != suggestSequence)
            {
                yield break;
            }

            suggestRoutine = null;
            isFetchingSuggestions = false;
            if (response != null)
            {
                suggestResults = response;
                lastSuggestError = string.Empty;
                Debug.Log($"[TsukiVox Suggest] Received {response.result?.tag?.Length ?? 0} suggestions.");
            }
            else
            {
                suggestResults = new BilibiliSuggestResponse
                {
                    code = -1,
                    result = new BilibiliSuggestResult { tag = Array.Empty<BilibiliSuggestItem>() },
                };
                lastSuggestError = string.IsNullOrWhiteSpace(error)
                    ? "获取搜索建议失败，请稍后重试"
                    : error.Trim();
                Debug.LogWarning($"[TsukiVox Suggest] Request failed: {lastSuggestError}");
            }

            SuggestStateChanged?.Invoke(this);
        }

        private IEnumerator AddItemRoutine(BilibiliCatalogItem item, bool playNow)
        {
            PlaylistState nextState = null;
            string error = null;
            yield return client.AddItem(
                item,
                playNow,
                value => nextState = value,
                value => error = value);

            addItemRoutine = null;
            isAddingItem = false;
            if (nextState != null)
            {
                lastAddedItem = item;
                pendingAddItem = null;
                lastAddItemError = string.Empty;
                OnStateReceived(nextState);
            }
            else
            {
                lastAddItemError = string.IsNullOrWhiteSpace(error)
                    ? "歌曲未能加入队列，请稍后重试。"
                    : error.Trim();
            }

            AddItemStateChanged?.Invoke(this);
        }

        private void EnsureDirectClient()
        {
            directClient ??= new BilibiliDirectClient();
        }

        private void EnsureDirectState()
        {
            if (!IsDirectService)
            {
                return;
            }

            EnsureDirectClient();
            var created = state == null;
            if (created)
            {
                state = new PlaylistState
                {
                    queue = Array.Empty<PlaylistItem>(),
                    currentIndex = -1,
                    playback = "paused",
                };
            }

            isConnected = true;
            lastRequestFailed = false;
            lastError = string.Empty;
            pendingStatus = "Direct request is ready.";
            hasClearedQueueOnStartup = true;
            if (created)
            {
                PublishDirectState();
            }
        }

        private void AddDirectItem(BilibiliCatalogItem item, bool playNow)
        {
            EnsureDirectState();
            item.Normalize();

            var queue = new List<PlaylistItem>(state.queue ?? Array.Empty<PlaylistItem>());
            var playlistItem = new PlaylistItem
            {
                id = $"direct-{Guid.NewGuid():N}",
                title = item.title,
                author = item.author,
                coverUrl = item.coverUrl,
                durationSeconds = item.durationSeconds,
                durationText = item.durationText,
                sourceType = "bilibili",
                sourceInput = string.IsNullOrWhiteSpace(item.pageUrl) ? item.bvid : item.pageUrl,
                status = PlaylistClient.StatusDownloading,
                message = "正在解析匿名 480P / 360P 视频",
                progress = 0f,
            };
            queue.Add(playlistItem);
            state.queue = queue.ToArray();
            if (state.currentIndex < 0 || playNow)
            {
                state.currentIndex = queue.Count - 1;
            }
            if (playNow)
            {
                state.playback = "playing";
            }

            pendingAddItem = null;
            lastAddedItem = item;
            lastAddItemError = string.Empty;
            isAddingItem = false;
            directDownloadQueue.Enqueue(playlistItem.id);
            PublishDirectState();
            AddItemStateChanged?.Invoke(this);
            EnsureDirectDownloadWorker();
        }

        private void EnsureDirectDownloadWorker()
        {
            if (!IsDirectService || directDownloadRoutine != null || directDownloadQueue.Count == 0)
            {
                return;
            }

            directDownloadRoutine = StartCoroutine(DirectDownloadWorker());
        }

        private IEnumerator DirectDownloadWorker()
        {
            EnsureDirectClient();
            try
            {
                while (IsDirectService && directDownloadQueue.Count > 0)
                {
                    var itemId = directDownloadQueue.Dequeue();
                    var queueItem = FindDirectQueueItem(itemId);
                    if (queueItem == null ||
                        !string.Equals(queueItem.status, PlaylistClient.StatusDownloading, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    activeDirectDownloadItemId = itemId;
                    var catalogItem = new BilibiliCatalogItem
                    {
                        bvid = ExtractBvid(queueItem.sourceInput),
                        title = queueItem.title,
                        author = queueItem.author,
                        coverUrl = queueItem.coverUrl,
                        durationSeconds = queueItem.durationSeconds,
                        durationText = queueItem.durationText,
                        pageUrl = queueItem.sourceInput,
                    };
                    DirectBilibiliMedia completedMedia = null;
                    string failure = null;
                    var lastProgress = -1f;
                    var lastProgressPublishedAt = -1f;
                    yield return directClient.ResolveAndCache(
                        catalogItem,
                        Path.Combine(Application.persistentDataPath, "DirectMediaCache"),
                        media => ApplyDirectResolvedMetadata(itemId, media),
                        progress =>
                        {
                            var now = Time.unscaledTime;
                            if (progress >= 1f ||
                                lastProgress < 0f ||
                                progress - lastProgress >= 0.01f ||
                                now - lastProgressPublishedAt >= 0.1f)
                            {
                                lastProgress = progress;
                                lastProgressPublishedAt = now;
                                UpdateDirectDownloadProgress(itemId, progress);
                            }
                        },
                        media => completedMedia = media,
                        error => failure = error);

                    if (!IsDirectService)
                    {
                        yield break;
                    }

                    queueItem = FindDirectQueueItem(itemId);
                    if (queueItem != null)
                    {
                        if (completedMedia != null)
                        {
                            CompleteDirectDownload(queueItem, completedMedia);
                        }
                        else if (!string.IsNullOrWhiteSpace(failure))
                        {
                            queueItem.status = PlaylistClient.StatusError;
                            queueItem.message = failure.Trim();
                            queueItem.progress = 0f;
                            PublishDirectState();
                        }
                    }

                    activeDirectDownloadItemId = string.Empty;
                    yield return null;
                }
            }
            finally
            {
                activeDirectDownloadItemId = string.Empty;
                directDownloadRoutine = null;
            }
        }

        private void ApplyDirectResolvedMetadata(string itemId, DirectBilibiliMedia media)
        {
            var item = FindDirectQueueItem(itemId);
            if (!IsDirectService || item == null || media == null)
            {
                return;
            }

            item.title = string.IsNullOrWhiteSpace(media.title) ? item.title : media.title;
            item.author = string.IsNullOrWhiteSpace(media.author) ? item.author : media.author;
            item.coverUrl = string.IsNullOrWhiteSpace(media.coverUrl) ? item.coverUrl : media.coverUrl;
            item.durationSeconds = Math.Max(0, media.durationSeconds);
            item.durationText = FormatDirectDuration(item.durationSeconds);
            item.message = $"{media.qualityLabel} · 正在下载到头显";
            PublishDirectState();
        }

        private void UpdateDirectDownloadProgress(string itemId, float progress)
        {
            var item = FindDirectQueueItem(itemId);
            if (!IsDirectService || item == null)
            {
                return;
            }

            item.progress = Mathf.Clamp01(progress);
            PublishDirectState();
        }

        private void CompleteDirectDownload(PlaylistItem item, DirectBilibiliMedia media)
        {
            item.title = string.IsNullOrWhiteSpace(media.title) ? item.title : media.title;
            item.author = string.IsNullOrWhiteSpace(media.author) ? item.author : media.author;
            item.coverUrl = string.IsNullOrWhiteSpace(media.coverUrl) ? item.coverUrl : media.coverUrl;
            item.durationSeconds = Math.Max(0, media.durationSeconds);
            item.durationText = FormatDirectDuration(item.durationSeconds);
            item.playableUrl = media.fileUrl;
            item.cacheUrl = media.fileUrl;
            item.streamUrl = string.Empty;
            item.status = PlaylistClient.StatusReady;
            item.message = $"{media.qualityLabel} · 已缓存到头显";
            item.progress = 1f;
            PublishDirectState();
        }

        private PlaylistItem FindDirectQueueItem(string itemId)
        {
            var queue = state?.queue;
            if (queue == null || string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            for (var index = 0; index < queue.Length; index += 1)
            {
                if (queue[index] != null && string.Equals(queue[index].id, itemId, StringComparison.Ordinal))
                {
                    return queue[index];
                }
            }
            return null;
        }

        private void CancelDirectDownloads()
        {
            directClient?.CancelDownload();
            if (directDownloadRoutine != null)
            {
                StopCoroutine(directDownloadRoutine);
                directDownloadRoutine = null;
            }
            directDownloadQueue.Clear();
            activeDirectDownloadItemId = string.Empty;
        }

        private void RestartDirectDownloadWorker()
        {
            CancelDirectDownloads();
            if (!IsDirectService || state?.queue == null)
            {
                return;
            }

            for (var index = 0; index < state.queue.Length; index += 1)
            {
                var item = state.queue[index];
                if (item != null &&
                    string.Equals(item.status, PlaylistClient.StatusDownloading, StringComparison.OrdinalIgnoreCase))
                {
                    item.progress = 0f;
                    directDownloadQueue.Enqueue(item.id);
                }
            }
            EnsureDirectDownloadWorker();
        }

        private bool HasPendingDirectDownloads()
        {
            var queue = state?.queue;
            if (queue == null)
            {
                return false;
            }

            for (var index = 0; index < queue.Length; index += 1)
            {
                if (queue[index] != null &&
                    string.Equals(queue[index].status, PlaylistClient.StatusDownloading, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void ApplyDirectControl(string action, string itemId)
        {
            EnsureDirectState();
            var queue = new List<PlaylistItem>(state.queue ?? Array.Empty<PlaylistItem>());
            var activeDownloadWasRemoved = false;

            switch (action)
            {
                case PlaylistClient.ControlPlay:
                    if (!string.IsNullOrWhiteSpace(itemId))
                    {
                        var selectedIndex = queue.FindIndex(item => item != null &&
                            string.Equals(item.id, itemId, StringComparison.Ordinal));
                        if (selectedIndex >= 0)
                        {
                            state.currentIndex = selectedIndex;
                        }
                    }
                    else if (state.currentIndex < 0 && queue.Count > 0)
                    {
                        state.currentIndex = 0;
                    }
                    state.playback = state.currentIndex >= 0 ? "playing" : "paused";
                    break;
                case PlaylistClient.ControlPause:
                    state.playback = "paused";
                    break;
                case PlaylistClient.ControlPrevious:
                    if (queue.Count > 0)
                    {
                        state.currentIndex = state.currentIndex <= 0 ? 0 : state.currentIndex - 1;
                    }
                    break;
                case PlaylistClient.ControlNext:
                    if (state.currentIndex >= 0 && state.currentIndex + 1 < queue.Count)
                    {
                        state.currentIndex += 1;
                    }
                    else if (state.currentIndex < 0 && queue.Count > 0)
                    {
                        state.currentIndex = 0;
                    }
                    else
                    {
                        state.playback = "paused";
                    }
                    break;
                case PlaylistClient.ControlClear:
                    if (state.currentIndex >= 0 && state.currentIndex + 1 < queue.Count)
                    {
                        activeDownloadWasRemoved = ContainsItemId(
                            queue,
                            state.currentIndex + 1,
                            queue.Count - state.currentIndex - 1,
                            activeDirectDownloadItemId);
                        queue.RemoveRange(state.currentIndex + 1, queue.Count - state.currentIndex - 1);
                    }
                    else if (state.currentIndex < 0)
                    {
                        activeDownloadWasRemoved = queue.Exists(item => item != null &&
                            string.Equals(item.id, activeDirectDownloadItemId, StringComparison.Ordinal));
                        queue.Clear();
                    }
                    break;
                case PlaylistClient.ControlClearPlayed:
                    if (state.currentIndex > 0)
                    {
                        activeDownloadWasRemoved = ContainsItemId(queue, 0, state.currentIndex, activeDirectDownloadItemId);
                        queue.RemoveRange(0, state.currentIndex);
                        state.currentIndex = 0;
                    }
                    break;
                case PlaylistClient.ControlClearExceptCurrent:
                    if (state.currentIndex >= 0 && state.currentIndex < queue.Count)
                    {
                        var current = queue[state.currentIndex];
                        activeDownloadWasRemoved = !string.IsNullOrEmpty(activeDirectDownloadItemId) &&
                                                   !string.Equals(current?.id, activeDirectDownloadItemId, StringComparison.Ordinal);
                        queue.Clear();
                        queue.Add(current);
                        state.currentIndex = 0;
                    }
                    else
                    {
                        activeDownloadWasRemoved = !string.IsNullOrEmpty(activeDirectDownloadItemId);
                        queue.Clear();
                        state.currentIndex = -1;
                        state.playback = "paused";
                    }
                    break;
                case PlaylistClient.ControlClearAll:
                    activeDownloadWasRemoved = !string.IsNullOrEmpty(activeDirectDownloadItemId);
                    queue.Clear();
                    state.currentIndex = -1;
                    state.playback = "paused";
                    break;
                case PlaylistClient.ControlRemove:
                    var removeIndex = queue.FindIndex(item => item != null &&
                        string.Equals(item.id, itemId, StringComparison.Ordinal));
                    if (removeIndex >= 0)
                    {
                        activeDownloadWasRemoved = string.Equals(
                            queue[removeIndex]?.id,
                            activeDirectDownloadItemId,
                            StringComparison.Ordinal);
                        queue.RemoveAt(removeIndex);
                        if (queue.Count == 0)
                        {
                            state.currentIndex = -1;
                            state.playback = "paused";
                        }
                        else if (removeIndex < state.currentIndex)
                        {
                            state.currentIndex -= 1;
                        }
                        else if (removeIndex == state.currentIndex)
                        {
                            state.currentIndex = Math.Min(removeIndex, queue.Count - 1);
                        }
                    }
                    break;
            }

            state.queue = queue.ToArray();
            state.currentIndex = queue.Count == 0 ? -1 : Mathf.Clamp(state.currentIndex, 0, queue.Count - 1);
            state.command = new PlaylistCommand
            {
                action = action,
                id = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId,
                issuedAt = NextDirectTimestamp(),
            };
            PublishDirectState();
            if (activeDownloadWasRemoved)
            {
                RestartDirectDownloadWorker();
            }
        }

        private void PublishDirectState()
        {
            if (!IsDirectService || state == null)
            {
                return;
            }

            state.Normalize();
            state.updatedAt = NextDirectTimestamp();
            isConnected = true;
            lastRequestFailed = false;
            lastError = string.Empty;
            pendingStatus = "Direct request is ready.";
            StateChanged?.Invoke(this, state);
            RefreshUi();
        }

        private long NextDirectTimestamp()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            directStateClock = Math.Max(now, directStateClock + 1L);
            return directStateClock;
        }

        private static bool ContainsItemId(List<PlaylistItem> queue, int index, int count, string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            var end = Math.Min(queue.Count, index + count);
            for (var itemIndex = Math.Max(0, index); itemIndex < end; itemIndex += 1)
            {
                if (queue[itemIndex] != null &&
                    string.Equals(queue[itemIndex].id, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ExtractBvid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var marker = value.IndexOf("BV", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
            {
                return string.Empty;
            }

            var end = marker + 2;
            while (end < value.Length && char.IsLetterOrDigit(value[end]))
            {
                end += 1;
            }
            return value.Substring(marker, end - marker);
        }

        private static string FormatDirectDuration(int seconds)
        {
            var safeSeconds = Math.Max(0, seconds);
            var hours = safeSeconds / 3600;
            var minutes = safeSeconds % 3600 / 60;
            var remainingSeconds = safeSeconds % 60;
            return hours > 0
                ? $"{hours}:{minutes:00}:{remainingSeconds:00}"
                : $"{minutes}:{remainingSeconds:00}";
        }

        private IEnumerator PollLoop()
        {
            while (enabled)
            {
                if (IsOnlineService && !hasValidatedDeviceCredential)
                {
                    isRequestInFlight = true;
                    pendingStatus = "Registering this device...";
                    DeviceEnrollmentResponse enrollment = null;
                    PlaylistRequestError enrollmentError = null;
                    yield return client.EnrollDevice(
                        value => enrollment = value,
                        value => enrollmentError = value);
                    isRequestInFlight = false;

                    if (enrollment == null)
                    {
                        OnRequestFailed(enrollmentError?.message ?? "Device enrollment failed.");
                        RefreshUi();
                        yield return new WaitForSeconds(Mathf.Max(MinimumRetryIntervalSeconds, retryIntervalSeconds));
                        continue;
                    }

                    deviceToken = enrollment.token;
                    SaveDeviceCredential();
                    client.SetDeviceToken(deviceToken);
                    hasValidatedDeviceCredential = true;
                }

                isRequestInFlight = true;
                yield return client.FetchState(OnStateReceived, OnRequestFailed);
                isRequestInFlight = false;
                RefreshUi();

                var waitSeconds = lastRequestFailed ? retryIntervalSeconds : pollIntervalSeconds;
                yield return new WaitForSeconds(Mathf.Max(MinimumPollIntervalSeconds, waitSeconds));
            }

            pollRoutine = null;
        }

        private void SendControl(string action)
        {
            SendControl(action, null);
        }

        private void SendControl(string action, string itemId)
        {
            if (IsDirectService)
            {
                ApplyDirectControl(action, itemId);
                return;
            }

            if (IsRequestInFlight)
            {
                pendingStatus = "Playlist request already in progress.";
                RefreshUi();
                return;
            }

            StartCoroutine(SendControlRoutine(action, itemId));
        }

        private IEnumerator SendControlRoutine(string action, string itemId)
        {
            isRequestInFlight = true;
            pendingStatus = $"Sending {FormatAction(action)}...";
            RefreshUi();

            yield return client.SendControl(action, OnStateReceived, OnRequestFailed, itemId);

            isRequestInFlight = false;
            RefreshUi();
        }

        private void OnStateReceived(PlaylistState nextState)
        {
            if (nextState == null)
            {
                return;
            }

            if (state != null &&
                state.updatedAt > 0L &&
                nextState.updatedAt > 0L &&
                nextState.updatedAt < state.updatedAt)
            {
                return;
            }

            state = nextState;
            isConnected = true;
            lastRequestFailed = false;
            lastError = string.Empty;
            pendingStatus = "Playlist sync connected.";
            StateChanged?.Invoke(this, state);

            // 应用启动后首次连上服务时清空队列，让每次启动都是干净状态。
            // 必须等连上之后才能发：断线时发送只会失败。
            if (!hasClearedQueueOnStartup)
            {
                hasClearedQueueOnStartup = true;
                if (clearQueueOnStartup)
                {
                    // 搜索结果是本地状态，直接清掉；搜索框启动时保持为空。
                    ClearSearchResults();
                    if (state.queue != null && state.queue.Length > 0)
                    {
                        // 启动时用 clearAll：连当前歌曲一起清掉，让每次启动都是干净状态。
                        // 这与「清空队列」按钮不同，后者刻意保留正在播放的那首。
                        //
                        // 本方法是在轮询请求内部被回调的，此刻 isRequestInFlight 仍为 true，
                        // 直接调用会被 SendControl 挡掉，所以延后一帧再发。
                        StartCoroutine(ClearQueueNextFrame());
                    }
                }
            }
        }

        private IEnumerator ClearQueueNextFrame()
        {
            yield return null;
            while (IsRequestInFlight)
            {
                yield return null;
            }
            SendControl(PlaylistClient.ControlClearAll);
        }

        private void OnRequestFailed(string error)
        {
            isConnected = false;
            lastRequestFailed = true;
            lastError = string.IsNullOrEmpty(error) ? "Playlist sync is not reachable." : error;
            pendingStatus = "Playlist sync disconnected. Retrying...";
        }

        private void WireUi()
        {
            playPauseButton?.onClick.AddListener(SendPlayPause);
            previousButton?.onClick.AddListener(SendPrevious);
            nextButton?.onClick.AddListener(SendNext);
            replayButton?.onClick.AddListener(SendReplay);
            applyHostButton?.onClick.AddListener(ApplyHelperHostFromUi);
            defaultHostButton?.onClick.AddListener(ApplyDefaultHelperHost);
            helperHostInput?.onEndEdit.AddListener(_ => ApplyHelperHostFromUi());
        }

        private void EnsureUiReferences()
        {
            if (connectionText != null &&
                currentSongText != null &&
                queueText != null &&
                playableUrlText != null &&
                playPauseButton != null &&
                previousButton != null &&
                nextButton != null &&
                replayButton != null &&
                helperHostInput != null &&
                applyHostButton != null &&
                defaultHostButton != null)
            {
                return;
            }

            var canvas = FindPrototypeCanvas();
            if (canvas == null)
            {
                return;
            }

            var panel = FindOrCreatePanel(canvas.transform);
            var helperRoot = ResolveHelperRoot(panel);
            var positionOffset = helperRoot == panel ? 300f : 0f;

            connectionText = connectionText != null
                ? connectionText
                : FindOrCreateText(helperRoot, "Helper Connection", "Helper: Connecting...", 14, FontStyle.Normal, new Vector2(positionOffset, 148f), new Vector2(520f, 58f));
            currentSongText = currentSongText != null
                ? currentSongText
                : FindOrCreateText(helperRoot, "Helper Current Song", "Current: no song selected.", 14, FontStyle.Normal, new Vector2(positionOffset, 66f), new Vector2(520f, 102f));
            queueText = queueText != null
                ? queueText
                : FindOrCreateText(helperRoot, "Helper Queue", "Queue 0 item(s)", 14, FontStyle.Normal, new Vector2(positionOffset, -32f), new Vector2(520f, 66f));
            playableUrlText = playableUrlText != null
                ? playableUrlText
                : FindOrCreateText(helperRoot, "Helper Playable URL", "Playable URL: none", 12, FontStyle.Normal, new Vector2(positionOffset, -106f), new Vector2(520f, 62f));

            var hostLabel = FindOrCreateText(helperRoot, "Helper Host Label", "PC IP", 12, FontStyle.Bold, new Vector2(positionOffset - 214f, -166f), new Vector2(64f, 32f));
            hostLabel.alignment = TextAnchor.MiddleLeft;
            helperHostInput = helperHostInput != null
                ? helperHostInput
                : FindOrCreateInputField(helperRoot, "Helper Host Input", new Vector2(positionOffset - 42f, -166f), new Vector2(260f, 34f));

            playPauseButton = playPauseButton != null
                ? playPauseButton
                : FindOrCreateButton(helperRoot, "Helper Play", "Play", new Vector2(positionOffset - 195f, -230f));
            previousButton = previousButton != null
                ? previousButton
                : FindOrCreateButton(helperRoot, "Helper Previous", "Prev", new Vector2(positionOffset - 65f, -230f));
            nextButton = nextButton != null
                ? nextButton
                : FindOrCreateButton(helperRoot, "Helper Next", "Next", new Vector2(positionOffset + 65f, -230f));
            replayButton = replayButton != null
                ? replayButton
                : FindOrCreateButton(helperRoot, "Helper Replay", "Replay", new Vector2(positionOffset + 195f, -230f));
            applyHostButton = applyHostButton != null
                ? applyHostButton
                : FindOrCreateButton(helperRoot, "Helper Apply Host", "Apply", new Vector2(positionOffset + 132f, -166f), new Vector2(86f, 34f), 13);
            defaultHostButton = defaultHostButton != null
                ? defaultHostButton
                : FindOrCreateButton(helperRoot, "Helper Default Host", "Use PC", new Vector2(positionOffset + 228f, -166f), new Vector2(86f, 34f), 13);
        }

        private void RefreshUi()
        {
            RefreshConnectionText();
            RefreshCurrentSongText();
            RefreshQueueText();
            RefreshPlayableUrlText();
            RefreshButtons();
        }

        private void RefreshConnectionText()
        {
            if (connectionText == null)
            {
                return;
            }

            connectionText.text = isConnected
                ? $"Service: Connected  {ServiceDisplayName}\nPlaylist {client?.PlaylistOrigin}  Downloads {client?.DownloadOrigin}"
                : $"Service: Disconnected  {ServiceDisplayName}  {pendingStatus}\n{lastError}";
        }

        private void RefreshCurrentSongText()
        {
            if (currentSongText == null)
            {
                return;
            }

            var item = state?.CurrentItem;
            songBuilder.Clear();
            if (item == null)
            {
                songBuilder.Append("Current: no song selected.");
            }
            else
            {
                songBuilder.Append("Current: ");
                songBuilder.Append(SafeText(item.title, "Untitled"));
                songBuilder.Append("\nSource ");
                songBuilder.Append(SafeText(item.sourceType, "unknown"));
                songBuilder.Append("  Status ");
                songBuilder.Append(FormatStatus(item.status));
                songBuilder.Append("  Playback ");
                songBuilder.Append(SafeText(state.playback, "paused"));

                if (!string.IsNullOrWhiteSpace(item.message))
                {
                    songBuilder.Append("\n");
                    songBuilder.Append(item.message.Trim());
                }
            }

            currentSongText.text = songBuilder.ToString();
        }

        private void RefreshQueueText()
        {
            if (queueText == null)
            {
                return;
            }

            queueBuilder.Clear();
            queueBuilder.Append("Queue ");
            queueBuilder.Append(state?.QueueCount ?? 0);
            queueBuilder.Append(" item(s)");

            if (state != null)
            {
                queueBuilder.Append("  Index ");
                queueBuilder.Append(state.currentIndex);
                queueBuilder.Append("  Updated ");
                queueBuilder.Append(state.updatedAt);
            }

            if (state?.command != null && !string.IsNullOrEmpty(state.command.action))
            {
                queueBuilder.Append("\nLast command ");
                queueBuilder.Append(state.command.action);
                queueBuilder.Append(" @ ");
                queueBuilder.Append(state.command.issuedAt);
            }

            queueText.text = queueBuilder.ToString();
        }

        private void RefreshPlayableUrlText()
        {
            if (playableUrlText == null)
            {
                return;
            }

            var item = state?.CurrentItem;
            if (item == null)
            {
                playableUrlText.text = "Playable URL: none";
                return;
            }

            if (item.IsReady)
            {
                playableUrlText.text = $"Playable URL: {client.ResolvePlayableUrl(item.playableUrl)}";
                return;
            }

            playableUrlText.text = $"Playable URL: waiting for {FormatStatus(item.status)}";
        }

        private void RefreshButtons()
        {
            var hasQueue = state != null && state.QueueCount > 0;
            var hasReadyItem = state?.CurrentItem != null && state.CurrentItem.IsReady;
            var canSendControl = CanSendControl;

            if (playPauseButton != null)
            {
                playPauseButton.interactable = canSendControl && hasReadyItem;
                SetButtonLabel(
                    playPauseButton,
                    string.Equals(state?.playback, "playing", StringComparison.OrdinalIgnoreCase) ? "Pause" : "Play");
            }

            if (previousButton != null)
            {
                previousButton.interactable = canSendControl && hasQueue;
            }

            if (nextButton != null)
            {
                nextButton.interactable = canSendControl && hasQueue;
            }

            if (replayButton != null)
            {
                replayButton.interactable = canSendControl && hasReadyItem;
            }

            if (applyHostButton != null)
            {
                applyHostButton.interactable = helperHostInput == null || !string.IsNullOrWhiteSpace(helperHostInput.text);
            }

            if (defaultHostButton != null)
            {
                defaultHostButton.interactable = true;
            }
        }

        public string ResolvePlayableUrl(string playableUrl)
        {
            if (client == null)
            {
                client = new PlaylistClient(playlistOrigin, downloadOrigin, deviceId, deviceToken);
            }

            return client.ResolvePlayableUrl(playableUrl);
        }

        private void EnsureClient()
        {
            if (client == null)
            {
                client = new PlaylistClient(playlistOrigin, downloadOrigin, deviceId, deviceToken);
            }
        }

        private void CancelCatalogRequests()
        {
            CancelSearch();
            CancelSuggestions();
            suggestResults = null;
            if (addItemRoutine != null)
            {
                StopCoroutine(addItemRoutine);
                addItemRoutine = null;
            }

            isAddingItem = false;
            pendingAddItem = null;
        }

        private void CancelSearch()
        {
            searchSequence += 1;
            directClient?.CancelSearch();
            if (searchRoutine != null)
            {
                StopCoroutine(searchRoutine);
                searchRoutine = null;
            }

            isSearching = false;
        }

        private void CancelSuggestions()
        {
            suggestSequence += 1;
            directClient?.CancelSuggestions();
            if (suggestRoutine != null)
            {
                StopCoroutine(suggestRoutine);
                suggestRoutine = null;
            }

            isFetchingSuggestions = false;
        }

        private void LoadServiceSettings()
        {
            helperHost = NormalizeHelperHost(PlayerPrefs.GetString(HelperHostPrefsKey, helperHost), DefaultHelperHostAddress);
            if (IsLoopbackHost(helperHost))
            {
                helperHost = DefaultHelperHostAddress;
                PlayerPrefs.SetString(HelperHostPrefsKey, helperHost);
                PlayerPrefs.Save();
            }

            var storedOnlineOrigin = NormalizeOnlineOrigin(
                PlayerPrefs.GetString(OnlineServiceOriginPrefsKey, onlineServiceOrigin),
                DefaultOnlineServiceOrigin);
            var settingsVersion = PlayerPrefs.GetInt(ServiceSettingsVersionPrefsKey, 0);
            if (settingsVersion < CurrentServiceSettingsVersion &&
                string.Equals(storedOnlineOrigin, LegacyOnlineServiceOrigin, StringComparison.OrdinalIgnoreCase))
            {
                storedOnlineOrigin = DefaultOnlineServiceOrigin;
            }
            onlineServiceOrigin = storedOnlineOrigin;
            var storedMode = PlayerPrefs.GetInt(ServiceModePrefsKey, (int)serviceMode);
            serviceMode = Enum.IsDefined(typeof(TsukiVoxServiceMode), storedMode)
                ? (TsukiVoxServiceMode)storedMode
                : TsukiVoxServiceMode.Direct;
            deviceId = PlayerPrefs.GetString(DeviceIdPrefsKey, string.Empty).Trim();
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(DeviceIdPrefsKey, deviceId);
            }

            PlayerPrefs.SetString(OnlineServiceOriginPrefsKey, onlineServiceOrigin);
            PlayerPrefs.SetInt(ServiceModePrefsKey, (int)serviceMode);
            PlayerPrefs.SetInt(ServiceSettingsVersionPrefsKey, CurrentServiceSettingsVersion);
            PlayerPrefs.Save();
        }

        private void ApplyHelperHost(string nextHelperHost, bool restartPolling)
        {
            helperHost = NormalizeHelperHost(nextHelperHost, DefaultHelperHostAddress);
            PlayerPrefs.SetString(HelperHostPrefsKey, helperHost);
            serviceMode = TsukiVoxServiceMode.Companion;
            PlayerPrefs.SetInt(ServiceModePrefsKey, (int)serviceMode);
            PlayerPrefs.Save();
            ApplyServiceConfiguration(restartPolling);
        }

        private void ApplyServiceMode(TsukiVoxServiceMode nextMode, bool restartPolling)
        {
            if (serviceMode == nextMode)
            {
                return;
            }

            serviceMode = nextMode;
            PlayerPrefs.SetInt(ServiceModePrefsKey, (int)serviceMode);
            PlayerPrefs.Save();
            ApplyServiceConfiguration(restartPolling);
        }

        private void ApplyServiceConfiguration(bool restartPolling)
        {
            if (restartPolling)
            {
                StopPolling();
            }

            CancelCatalogRequests();
            CancelDirectDownloads();
            ApplyOriginsFromService();
            LoadDeviceCredential();
            SyncHostInput();

            if (client == null)
            {
                client = new PlaylistClient(playlistOrigin, downloadOrigin, deviceId, deviceToken);
            }
            else
            {
                client.SetOrigins(playlistOrigin, downloadOrigin);
                client.SetDeviceId(deviceId);
                client.SetDeviceToken(deviceToken);
            }

            state = null;
            StateChanged?.Invoke(this, null);
            isConnected = false;
            lastRequestFailed = false;
            lastError = string.Empty;
            pendingStatus = "Connecting to playlist sync...";
            searchResults = null;
            lastSearchError = string.Empty;
            lastSuggestError = string.Empty;
            lastAddItemError = string.Empty;
            lastAddedItem = null;
            hasClearedQueueOnStartup = false;

            if (restartPolling)
            {
                StartPolling();
            }

            RefreshUi();
            SearchStateChanged?.Invoke(this);
            AddItemStateChanged?.Invoke(this);
            SuggestStateChanged?.Invoke(this);
        }

        private void ApplyOnlineServiceOrigin(string origin, bool restartPolling)
        {
            onlineServiceOrigin = NormalizeOnlineOrigin(origin, DefaultOnlineServiceOrigin);
            serviceMode = TsukiVoxServiceMode.Online;
            PlayerPrefs.SetString(OnlineServiceOriginPrefsKey, onlineServiceOrigin);
            PlayerPrefs.SetInt(ServiceModePrefsKey, (int)serviceMode);
            PlayerPrefs.Save();
            ApplyServiceConfiguration(restartPolling);
        }

        private void LoadDeviceCredential()
        {
            deviceToken = IsOnlineService
                ? PlayerPrefs.GetString(DeviceTokenPrefsKey(onlineServiceOrigin), string.Empty).Trim()
                : string.Empty;
            hasValidatedDeviceCredential = !IsOnlineService;
        }

        private void SaveDeviceCredential()
        {
            if (!IsOnlineService || string.IsNullOrWhiteSpace(deviceToken))
            {
                return;
            }
            PlayerPrefs.SetString(DeviceTokenPrefsKey(onlineServiceOrigin), deviceToken);
            PlayerPrefs.Save();
        }

        private static string DeviceTokenPrefsKey(string origin)
        {
            return $"{DeviceTokenPrefsPrefix}{Hash128.Compute(origin)}";
        }

        private void ApplyOriginsFromService()
        {
            if (IsDirectService)
            {
                playlistOrigin = BilibiliDirectClient.ApiOrigin;
                downloadOrigin = BilibiliDirectClient.ApiOrigin;
                return;
            }

            if (IsOnlineService)
            {
                playlistOrigin = onlineServiceOrigin;
                downloadOrigin = onlineServiceOrigin;
                return;
            }

            playlistOrigin = $"http://{helperHost}:{PlaylistPort}";
            downloadOrigin = $"http://{helperHost}:{DownloadPort}";
        }

        private void SyncHostInput()
        {
            helperHostInput?.SetTextWithoutNotify(ServiceAddress);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private static string NormalizeHelperHost(string input, string fallback)
        {
            var trimmed = string.IsNullOrWhiteSpace(input) ? fallback : input.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) && !string.IsNullOrWhiteSpace(absoluteUri.Host))
            {
                return absoluteUri.Host;
            }

            if (Uri.TryCreate($"http://{trimmed}", UriKind.Absolute, out var hostUri) && !string.IsNullOrWhiteSpace(hostUri.Host))
            {
                return hostUri.Host;
            }

            return fallback;
        }

        private static string NormalizeOnlineOrigin(string input, string fallback)
        {
            var trimmed = string.IsNullOrWhiteSpace(input) ? fallback : input.Trim();
            if (!trimmed.Contains("://", StringComparison.Ordinal))
            {
                var firstSegment = trimmed.Split(':')[0];
                var looksLikeLanAddress = string.Equals(firstSegment, "localhost", StringComparison.OrdinalIgnoreCase) ||
                                          firstSegment.StartsWith("127.", StringComparison.Ordinal) ||
                                          firstSegment.StartsWith("10.", StringComparison.Ordinal) ||
                                          firstSegment.StartsWith("192.168.", StringComparison.Ordinal) ||
                                          firstSegment.StartsWith("172.", StringComparison.Ordinal);
                trimmed = $"{(looksLikeLanAddress ? "http" : "https")}://{trimmed}";
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
                (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(uri.Host))
            {
                return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            }

            return fallback;
        }

        private static bool IsLoopbackHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase) ||
                   host.StartsWith("127.", StringComparison.Ordinal);
        }

        private static Canvas FindPrototypeCanvas()
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

        private static RectTransform FindOrCreatePanel(Transform canvasTransform)
        {
            var panel = canvasTransform.Find("Panel") as RectTransform;
            if (panel != null)
            {
                if (FindAnyObjectByType<QuestAppShellPrototype>() != null)
                {
                    panel.sizeDelta = new Vector2(Mathf.Max(panel.sizeDelta.x, 1240f), Mathf.Max(panel.sizeDelta.y, 690f));
                }
                else
                {
                    panel.sizeDelta = new Vector2(Mathf.Max(panel.sizeDelta.x, 1180f), Mathf.Max(panel.sizeDelta.y, 650f));
                    ReflowAudioPrototypePanel(panel);
                }

                return panel;
            }

            var panelObject = new GameObject("Panel");
            panelObject.transform.SetParent(canvasTransform, false);
            panel = panelObject.AddComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(1180f, 650f);
            var image = panelObject.AddComponent<Image>();
            image.color = new Color(0.04f, 0.055f, 0.062f, 0.92f);
            ReflowAudioPrototypePanel(panel);
            return panel;
        }

        private static void ReflowAudioPrototypePanel(RectTransform panel)
        {
            var title = FindChildText(panel, "TsukiVox Quest Audio Prototype") ?? FindChildText(panel, "TsukiVox Quest Prototype");
            if (title != null)
            {
                title.name = "TsukiVox Quest Prototype";
                title.text = "TsukiVox Quest Prototype";
                title.fontSize = 28;
                title.fontStyle = FontStyle.Bold;
                title.alignment = TextAnchor.MiddleCenter;
                title.rectTransform.anchoredPosition = new Vector2(0f, 288f);
                title.rectTransform.sizeDelta = new Vector2(740f, 64f);
            }

            var audioTitle = FindOrCreateText(panel, "V0.1 Audio", "V0.1 Audio", 20, FontStyle.Bold, new Vector2(-300f, 244f), new Vector2(500f, 34f));
            audioTitle.alignment = TextAnchor.MiddleCenter;

            SetChildRect(panel, "Ready.", new Vector2(-300f, 198f), new Vector2(520f, 62f), 16);
            SetChildRect(panel, "Preset", new Vector2(-300f, 146f), new Vector2(520f, 34f), 18);
            SetChildRect(panel, "Metrics", new Vector2(-300f, 57f), new Vector2(520f, 142f), 14);
            SetChildRect(panel, "Input Level", new Vector2(-430f, -70f), new Vector2(320f, 52f));
            SetChildRect(panel, "Output Level", new Vector2(-170f, -70f), new Vector2(320f, 52f));
            SetChildRect(panel, "Monitor Volume", new Vector2(-300f, -130f), new Vector2(320f, 52f));
            SetChildRect(panel, "Start Mic", new Vector2(-495f, -206f), new Vector2(116f, 48f));
            SetChildRect(panel, "Stop", new Vector2(-365f, -206f), new Vector2(116f, 48f));
            SetChildRect(panel, "Prev", new Vector2(-235f, -206f), new Vector2(116f, 48f));
            SetChildRect(panel, "Next", new Vector2(-105f, -206f), new Vector2(116f, 48f));
            SetChildRect(panel, "Monitor", new Vector2(-475f, -270f), new Vector2(170f, 38f));
            SetChildRect(panel, "Native", new Vector2(-300f, -270f), new Vector2(170f, 38f));
            SetChildRect(panel, "Safety", new Vector2(-125f, -270f), new Vector2(170f, 38f));
        }

        private static RectTransform ResolveHelperRoot(RectTransform panel)
        {
            var existingRuntimeGroup = panel.Find("V0.2 Runtime Helper") as RectTransform;
            if (existingRuntimeGroup != null)
            {
                return existingRuntimeGroup;
            }

            var hasGeneratedHelperUi =
                panel.Find("Helper: Connecting...") != null ||
                panel.Find("Current: no song selected.") != null ||
                panel.Find("Playable URL: none") != null;

            return hasGeneratedHelperUi ? panel : CreateRuntimeGroup(panel);
        }

        private static Text FindChildText(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            return child != null && child.TryGetComponent<Text>(out var text) ? text : null;
        }

        private static void SetChildRect(Transform parent, string childName, Vector2 position, Vector2 size, int fontSize = 0)
        {
            var child = parent.Find(childName);
            if (child == null || !child.TryGetComponent<RectTransform>(out var rect))
            {
                return;
            }

            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            if (fontSize > 0 && child.TryGetComponent<Text>(out var text))
            {
                text.fontSize = fontSize;
            }
        }

        private static RectTransform CreateRuntimeGroup(Transform parent)
        {
            var groupObject = new GameObject("V0.2 Runtime Helper");
            groupObject.transform.SetParent(parent, false);
            var group = groupObject.AddComponent<RectTransform>();
            group.anchorMin = new Vector2(0.5f, 0.5f);
            group.anchorMax = new Vector2(0.5f, 0.5f);
            group.anchoredPosition = new Vector2(300f, 52f);
            group.sizeDelta = new Vector2(540f, 560f);

            var title = FindOrCreateText(group, "Helper Title", "V0.2 PC Helper", 20, FontStyle.Bold, new Vector2(0f, 220f), new Vector2(520f, 34f));
            title.alignment = TextAnchor.MiddleCenter;
            return group;
        }

        private static Text FindOrCreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            Vector2 position,
            Vector2 size)
        {
            var existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent<Text>(out var existingText))
            {
                return existingText;
            }

            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = new Color(0.93f, 0.97f, 0.98f);
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            return text;
        }

        private static Button FindOrCreateButton(Transform parent, string name, string label, Vector2 position)
        {
            return FindOrCreateButton(parent, name, label, position, new Vector2(116f, 48f), 16);
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

            var text = FindOrCreateText(buttonObject.transform, "Label", label, fontSize, FontStyle.Bold, Vector2.zero, rect.sizeDelta);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static InputField FindOrCreateInputField(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent<InputField>(out var existingInput))
            {
                if (existing.TryGetComponent<RectTransform>(out var existingRect))
                {
                    existingRect.anchoredPosition = position;
                    existingRect.sizeDelta = size;
                }

                return existingInput;
            }

            var inputObject = new GameObject(name);
            inputObject.transform.SetParent(parent, false);
            var rect = inputObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = inputObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.12f, 0.14f, 0.96f);

            var text = FindOrCreateText(inputObject.transform, "Text", string.Empty, 14, FontStyle.Normal, Vector2.zero, new Vector2(size.x - 20f, size.y));
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            var placeholder = FindOrCreateText(inputObject.transform, "Placeholder", DefaultHelperHostAddress, 14, FontStyle.Italic, Vector2.zero, new Vector2(size.x - 20f, size.y));
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.5f, 0.6f, 0.62f, 0.8f);

            var input = inputObject.AddComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = InputField.ContentType.Standard;
            input.characterLimit = 80;
            return input;
        }

        private static string FormatAction(string action)
        {
            return action switch
            {
                PlaylistClient.ControlPlay => "play",
                PlaylistClient.ControlPause => "pause",
                PlaylistClient.ControlPrevious => "previous",
                PlaylistClient.ControlNext => "next",
                PlaylistClient.ControlReplay => "replay",
                _ => action,
            };
        }

        private static string FormatStatus(string status)
        {
            return status switch
            {
                PlaylistClient.StatusDownloading => "downloading",
                PlaylistClient.StatusReady => "ready",
                PlaylistClient.StatusError => "error",
                null => "unknown",
                "" => "unknown",
                _ => status,
            };
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
