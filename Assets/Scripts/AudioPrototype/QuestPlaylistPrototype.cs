using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    [DisallowMultipleComponent]
    public sealed class QuestPlaylistPrototype : MonoBehaviour
    {
        private const string LegacyHelperHostPrefsKey = "TsukiVox.HelperHost";
        private const string LegacyServiceModePrefsKey = "TsukiVox.ServiceMode";
        private const string LegacyOnlineServiceOriginPrefsKey = "TsukiVox.OnlineServiceOrigin";
        private const string LegacyServiceSettingsVersionPrefsKey = "TsukiVox.ServiceSettingsVersion";
        private const string LegacyDeviceIdPrefsKey = "TsukiVox.DeviceId";
        private const string LegacyOnlineFullCachePrefsKey = "TsukiVox.OnlineFullMediaCache";
        private const string LegacyDeviceTokenPrefsPrefix = "TsukiVox.DeviceToken.";
        private const string LegacyPublicOrigin = "https://api.tsukivox.com";
        private const string LegacyDevelopmentOrigin = "http://192.168.50.41:8080";

        [Header("Legacy Debug UI")]
        [SerializeField] private Text connectionText;
        [SerializeField] private Text currentSongText;
        [SerializeField] private Text queueText;
        [SerializeField] private Text playableUrlText;
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button replayButton;

        private readonly StringBuilder songBuilder = new StringBuilder(512);
        private readonly StringBuilder queueBuilder = new StringBuilder(256);
        private readonly Queue<string> downloadQueue = new Queue<string>();

        private BilibiliDirectClient directClient;
        private PlaylistState state;
        private Coroutine downloadRoutine;
        private Coroutine searchRoutine;
        private Coroutine suggestRoutine;
        private string activeDownloadItemId = string.Empty;
        private long stateClock;
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

        public event Action<QuestPlaylistPrototype, PlaylistState> StateChanged;
        public event Action<QuestPlaylistPrototype> SearchStateChanged;
        public event Action<QuestPlaylistPrototype> AddItemStateChanged;
        public event Action<QuestPlaylistPrototype> SuggestStateChanged;

        public PlaylistState CurrentState => state;
        public bool IsConnected => state != null;
        public bool CanSendControl => state != null && !isAddingItem;
        public BilibiliSearchResponse SearchResults => searchResults;
        public BilibiliSuggestResponse SuggestResults => suggestResults;
        public bool IsSearching => isSearching;
        public bool IsFetchingSuggestions => isFetchingSuggestions;
        public string LastSearchError => lastSearchError;
        public string LastSuggestError => lastSuggestError;
        public bool IsAddingItem => isAddingItem;
        public bool CanAddItem => state != null && !isAddingItem;
        public BilibiliCatalogItem PendingAddItem => pendingAddItem;
        public BilibiliCatalogItem LastAddedItem => lastAddedItem;
        public string LastAddItemError => lastAddItemError;
        public bool HasSearchResults =>
            (searchResults != null && searchResults.ItemCount > 0) ||
            !string.IsNullOrEmpty(lastSearchError);

        public static QuestPlaylistPrototype EnsureScenePrototype()
        {
            var existing = FindAnyObjectByType<QuestPlaylistPrototype>();
            if (existing != null)
            {
                return existing;
            }

            var prototypeObject = new GameObject("Quest Playlist Prototype");
            return prototypeObject.AddComponent<QuestPlaylistPrototype>();
        }

        private void Awake()
        {
            RemoveLegacyServicePreferences();
            EnsureDirectClient();
            EnsureState();
            WireUi();
            RefreshUi();
        }

        private void OnEnable()
        {
            EnsureDirectClient();
            EnsureState();
            ResumePendingDownloads();
        }

        private void OnDisable()
        {
            CancelCatalogRequests();
            CancelDownloads();
        }

        public void SendPlayPause()
        {
            var action = string.Equals(state?.playback, "playing", StringComparison.OrdinalIgnoreCase)
                ? DirectPlaylist.ControlPause
                : DirectPlaylist.ControlPlay;
            ApplyControl(action, null);
        }

        public void SendPrevious()
        {
            ApplyControl(DirectPlaylist.ControlPrevious, null);
        }

        public void SendNext()
        {
            ApplyControl(DirectPlaylist.ControlNext, null);
        }

        public void SendReplay()
        {
            ApplyControl(DirectPlaylist.ControlReplay, null);
        }

        public void PlayQueueItem(string itemId)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                ApplyControl(DirectPlaylist.ControlPlay, itemId);
            }
        }

        public void ClearQueue()
        {
            ApplyControl(DirectPlaylist.ControlClear, null);
        }

        public MediaCacheClearResult ClearMediaCache()
        {
            CancelDownloads();
            ApplyControl(DirectPlaylist.ControlClearAll, null);

            var deletedFileCount = 0;
            var deletedBytes = 0L;
            var errors = new List<string>();
            ClearCacheDirectory(
                DirectPlaylist.MediaCacheDirectoryName,
                ref deletedFileCount,
                ref deletedBytes,
                errors);
            ClearCacheDirectory(
                DirectPlaylist.LegacyVideoCacheDirectoryName,
                ref deletedFileCount,
                ref deletedBytes,
                errors);

            var error = string.Join("；", errors);
            if (string.IsNullOrEmpty(error))
            {
                Debug.Log($"[TsukiVox Direct] Cleared {deletedFileCount} cached media files ({deletedBytes} bytes).");
            }
            else
            {
                Debug.LogWarning($"[TsukiVox Direct] Media cache was only partially cleared: {error}");
            }

            return new MediaCacheClearResult(deletedFileCount, deletedBytes, error);
        }

        public void ClearPlayedQueue()
        {
            ApplyControl(DirectPlaylist.ControlClearPlayed, null);
        }

        public void ClearQueueExceptCurrent()
        {
            ApplyControl(DirectPlaylist.ControlClearExceptCurrent, null);
        }

        public void RemoveQueueItem(string itemId)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                ApplyControl(DirectPlaylist.ControlRemove, itemId);
            }
        }

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

        public void SearchBilibili(string query, int page = 1, int pageSize = 4)
        {
            EnsureDirectClient();
            CancelSearch();

            var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Min(20, Math.Max(1, pageSize));
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
            searchRoutine = StartCoroutine(SearchRoutine(
                normalizedQuery,
                normalizedPage,
                normalizedPageSize,
                sequence));
        }

        public void FetchBilibiliSuggestions(string term)
        {
            EnsureDirectClient();
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
            suggestRoutine = StartCoroutine(SuggestRoutine(normalizedTerm, sequence));
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

            pendingAddItem = item;
            lastAddItemError = string.Empty;
            isAddingItem = true;
            AddItemStateChanged?.Invoke(this);
            AddDirectItem(item, playNow);
        }

        public string ResolveCatalogAssetUrl(string assetUrl)
        {
            if (string.IsNullOrWhiteSpace(assetUrl))
            {
                return string.Empty;
            }

            var trimmed = assetUrl.Trim();
            return trimmed.StartsWith("//", StringComparison.Ordinal) ? $"https:{trimmed}" : trimmed;
        }

        public string ResolvePlayableUrl(string playableUrl)
        {
            return string.IsNullOrWhiteSpace(playableUrl) ? string.Empty : playableUrl.Trim();
        }

        private IEnumerator SearchRoutine(string query, int page, int pageSize, int sequence)
        {
            BilibiliSearchResponse response = null;
            string error = null;
            yield return directClient.Search(
                query,
                page,
                pageSize,
                value => response = value,
                value => error = value);

            if (sequence != searchSequence)
            {
                yield break;
            }

            searchRoutine = null;
            isSearching = false;
            if (response != null)
            {
                response.Normalize();
                searchResults = response;
                lastSearchError = string.Empty;
            }
            else
            {
                lastSearchError = string.IsNullOrWhiteSpace(error)
                    ? "B 站搜索暂时不可用，请稍后重试。"
                    : error.Trim();
                Debug.LogWarning($"[TsukiVox Direct] Search failed: {lastSearchError}");
            }

            SearchStateChanged?.Invoke(this);
        }

        private IEnumerator SuggestRoutine(string term, int sequence)
        {
            BilibiliSuggestResponse response = null;
            string error = null;
            yield return directClient.FetchSuggestions(
                term,
                9,
                value => response = value,
                value => error = value);

            if (sequence != suggestSequence)
            {
                yield break;
            }

            suggestRoutine = null;
            isFetchingSuggestions = false;
            if (response != null)
            {
                response.Normalize();
                suggestResults = response;
                lastSuggestError = string.Empty;
            }
            else
            {
                suggestResults = new BilibiliSuggestResponse
                {
                    code = -1,
                    result = new BilibiliSuggestResult { tag = Array.Empty<BilibiliSuggestItem>() },
                };
                lastSuggestError = string.IsNullOrWhiteSpace(error)
                    ? "获取搜索建议失败，请稍后重试。"
                    : error.Trim();
                Debug.LogWarning($"[TsukiVox Direct] Suggestions failed: {lastSuggestError}");
            }

            SuggestStateChanged?.Invoke(this);
        }

        private void EnsureDirectClient()
        {
            directClient ??= new BilibiliDirectClient();
        }

        private void EnsureState()
        {
            if (state != null)
            {
                return;
            }

            state = new PlaylistState
            {
                queue = Array.Empty<PlaylistItem>(),
                currentIndex = -1,
                playback = "paused",
            };
            PublishState();
        }

        private void AddDirectItem(BilibiliCatalogItem item, bool playNow)
        {
            EnsureState();
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
                sourceType = "bilibili-direct",
                sourceInput = string.IsNullOrWhiteSpace(item.pageUrl) ? item.bvid : item.pageUrl,
                status = DirectPlaylist.StatusDownloading,
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
            downloadQueue.Enqueue(playlistItem.id);
            PublishState();
            AddItemStateChanged?.Invoke(this);
            EnsureDownloadWorker();
        }

        private void EnsureDownloadWorker()
        {
            if (downloadRoutine == null && downloadQueue.Count > 0)
            {
                downloadRoutine = StartCoroutine(DownloadWorker());
            }
        }

        private IEnumerator DownloadWorker()
        {
            EnsureDirectClient();
            try
            {
                while (downloadQueue.Count > 0)
                {
                    var itemId = downloadQueue.Dequeue();
                    var queueItem = FindQueueItem(itemId);
                    if (queueItem == null ||
                        !string.Equals(queueItem.status, DirectPlaylist.StatusDownloading, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    activeDownloadItemId = itemId;
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
                        Path.Combine(Application.persistentDataPath, DirectPlaylist.MediaCacheDirectoryName),
                        media => ApplyResolvedMetadata(itemId, media),
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
                                UpdateDownloadProgress(itemId, progress);
                            }
                        },
                        media => completedMedia = media,
                        error => failure = error);

                    queueItem = FindQueueItem(itemId);
                    if (queueItem != null)
                    {
                        if (completedMedia != null)
                        {
                            CompleteDownload(queueItem, completedMedia);
                        }
                        else if (!string.IsNullOrWhiteSpace(failure))
                        {
                            queueItem.status = DirectPlaylist.StatusError;
                            queueItem.message = failure.Trim();
                            queueItem.progress = 0f;
                            PublishState();
                        }
                    }

                    activeDownloadItemId = string.Empty;
                    yield return null;
                }
            }
            finally
            {
                activeDownloadItemId = string.Empty;
                downloadRoutine = null;
            }
        }

        private void ApplyResolvedMetadata(string itemId, DirectBilibiliMedia media)
        {
            var item = FindQueueItem(itemId);
            if (item == null || media == null)
            {
                return;
            }

            item.title = string.IsNullOrWhiteSpace(media.title) ? item.title : media.title;
            item.author = string.IsNullOrWhiteSpace(media.author) ? item.author : media.author;
            item.coverUrl = string.IsNullOrWhiteSpace(media.coverUrl) ? item.coverUrl : media.coverUrl;
            item.durationSeconds = Math.Max(0, media.durationSeconds);
            item.durationText = FormatDuration(item.durationSeconds);
            item.message = $"{media.qualityLabel} · 正在下载到头显";
            PublishState();
        }

        private void UpdateDownloadProgress(string itemId, float progress)
        {
            var item = FindQueueItem(itemId);
            if (item == null)
            {
                return;
            }

            item.progress = Mathf.Clamp01(progress);
            PublishState();
        }

        private void CompleteDownload(PlaylistItem item, DirectBilibiliMedia media)
        {
            item.title = string.IsNullOrWhiteSpace(media.title) ? item.title : media.title;
            item.author = string.IsNullOrWhiteSpace(media.author) ? item.author : media.author;
            item.coverUrl = string.IsNullOrWhiteSpace(media.coverUrl) ? item.coverUrl : media.coverUrl;
            item.durationSeconds = Math.Max(0, media.durationSeconds);
            item.durationText = FormatDuration(item.durationSeconds);
            item.playableUrl = media.fileUrl;
            item.cacheUrl = media.fileUrl;
            item.streamUrl = string.Empty;
            item.status = DirectPlaylist.StatusReady;
            item.message = $"{media.qualityLabel} · 已缓存到头显";
            item.progress = 1f;
            PublishState();
        }

        private PlaylistItem FindQueueItem(string itemId)
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

        private void CancelDownloads()
        {
            directClient?.CancelDownload();
            if (downloadRoutine != null)
            {
                StopCoroutine(downloadRoutine);
                downloadRoutine = null;
            }
            downloadQueue.Clear();
            activeDownloadItemId = string.Empty;
        }

        private void RestartDownloadWorker()
        {
            CancelDownloads();
            ResumePendingDownloads();
        }

        private void ResumePendingDownloads()
        {
            if (state?.queue == null)
            {
                return;
            }

            for (var index = 0; index < state.queue.Length; index += 1)
            {
                var item = state.queue[index];
                if (item != null &&
                    string.Equals(item.status, DirectPlaylist.StatusDownloading, StringComparison.OrdinalIgnoreCase))
                {
                    item.progress = 0f;
                    downloadQueue.Enqueue(item.id);
                }
            }
            EnsureDownloadWorker();
        }

        private void ApplyControl(string action, string itemId)
        {
            EnsureState();
            var queue = new List<PlaylistItem>(state.queue ?? Array.Empty<PlaylistItem>());
            var activeDownloadWasRemoved = false;

            switch (action)
            {
                case DirectPlaylist.ControlPlay:
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
                case DirectPlaylist.ControlPause:
                    state.playback = "paused";
                    break;
                case DirectPlaylist.ControlReplay:
                    if (state.currentIndex >= 0)
                    {
                        state.playback = "playing";
                    }
                    break;
                case DirectPlaylist.ControlPrevious:
                    if (queue.Count > 0)
                    {
                        state.currentIndex = state.currentIndex <= 0 ? 0 : state.currentIndex - 1;
                        state.playback = "playing";
                    }
                    break;
                case DirectPlaylist.ControlNext:
                    if (state.currentIndex >= 0 && state.currentIndex + 1 < queue.Count)
                    {
                        state.currentIndex += 1;
                        state.playback = "playing";
                    }
                    else if (state.currentIndex < 0 && queue.Count > 0)
                    {
                        state.currentIndex = 0;
                        state.playback = "playing";
                    }
                    else
                    {
                        state.playback = "paused";
                    }
                    break;
                case DirectPlaylist.ControlClear:
                    if (state.currentIndex >= 0 && state.currentIndex + 1 < queue.Count)
                    {
                        activeDownloadWasRemoved = ContainsItemId(
                            queue,
                            state.currentIndex + 1,
                            queue.Count - state.currentIndex - 1,
                            activeDownloadItemId);
                        queue.RemoveRange(state.currentIndex + 1, queue.Count - state.currentIndex - 1);
                    }
                    else if (state.currentIndex < 0)
                    {
                        activeDownloadWasRemoved = queue.Exists(item => item != null &&
                            string.Equals(item.id, activeDownloadItemId, StringComparison.Ordinal));
                        queue.Clear();
                    }
                    break;
                case DirectPlaylist.ControlClearPlayed:
                    if (state.currentIndex > 0)
                    {
                        activeDownloadWasRemoved = ContainsItemId(queue, 0, state.currentIndex, activeDownloadItemId);
                        queue.RemoveRange(0, state.currentIndex);
                        state.currentIndex = 0;
                    }
                    break;
                case DirectPlaylist.ControlClearExceptCurrent:
                    if (state.currentIndex >= 0 && state.currentIndex < queue.Count)
                    {
                        var current = queue[state.currentIndex];
                        activeDownloadWasRemoved = !string.IsNullOrEmpty(activeDownloadItemId) &&
                                                   !string.Equals(current?.id, activeDownloadItemId, StringComparison.Ordinal);
                        queue.Clear();
                        queue.Add(current);
                        state.currentIndex = 0;
                    }
                    else
                    {
                        activeDownloadWasRemoved = !string.IsNullOrEmpty(activeDownloadItemId);
                        queue.Clear();
                        state.currentIndex = -1;
                        state.playback = "paused";
                    }
                    break;
                case DirectPlaylist.ControlClearAll:
                    activeDownloadWasRemoved = !string.IsNullOrEmpty(activeDownloadItemId);
                    queue.Clear();
                    state.currentIndex = -1;
                    state.playback = "paused";
                    break;
                case DirectPlaylist.ControlRemove:
                    var removeIndex = queue.FindIndex(item => item != null &&
                        string.Equals(item.id, itemId, StringComparison.Ordinal));
                    if (removeIndex >= 0)
                    {
                        activeDownloadWasRemoved = string.Equals(
                            queue[removeIndex]?.id,
                            activeDownloadItemId,
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
                issuedAt = NextTimestamp(),
            };
            PublishState();
            if (activeDownloadWasRemoved)
            {
                RestartDownloadWorker();
            }
        }

        private void PublishState()
        {
            if (state == null)
            {
                return;
            }

            state.Normalize();
            state.updatedAt = NextTimestamp();
            StateChanged?.Invoke(this, state);
            RefreshUi();
        }

        private long NextTimestamp()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            stateClock = Math.Max(now, stateClock + 1L);
            return stateClock;
        }

        private static bool ContainsItemId(List<PlaylistItem> queue, int index, int count, string itemId)
        {
            if (queue == null || string.IsNullOrEmpty(itemId) || count <= 0)
            {
                return false;
            }

            var end = Math.Min(queue.Count, index + count);
            for (var itemIndex = Math.Max(0, index); itemIndex < end; itemIndex += 1)
            {
                if (string.Equals(queue[itemIndex]?.id, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ClearCacheDirectory(
            string directoryName,
            ref int deletedFileCount,
            ref long deletedBytes,
            List<string> errors)
        {
            var directoryPath = Path.Combine(Application.persistentDataPath, directoryName);
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            }
            catch (Exception exception)
            {
                errors.Add($"{directoryName} 无法读取：{exception.Message}");
                return;
            }

            for (var index = 0; index < files.Length; index += 1)
            {
                var filePath = files[index];
                try
                {
                    var length = new FileInfo(filePath).Length;
                    File.Delete(filePath);
                    deletedFileCount += 1;
                    deletedBytes += Math.Max(0L, length);
                }
                catch (Exception exception)
                {
                    errors.Add($"{directoryName}/{Path.GetFileName(filePath)} 删除失败：{exception.Message}");
                }
            }

            try
            {
                if (Directory.Exists(directoryPath) && Directory.GetFileSystemEntries(directoryPath).Length == 0)
                {
                    Directory.Delete(directoryPath);
                }
            }
            catch (Exception exception)
            {
                errors.Add($"{directoryName} 无法收尾：{exception.Message}");
            }
        }

        private static string ExtractBvid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            for (var index = 0; index + 12 <= trimmed.Length; index += 1)
            {
                if ((trimmed[index] == 'B' || trimmed[index] == 'b') &&
                    (trimmed[index + 1] == 'V' || trimmed[index + 1] == 'v'))
                {
                    return $"BV{trimmed.Substring(index + 2, 10)}";
                }
            }
            return string.Empty;
        }

        private static string FormatDuration(int seconds)
        {
            var safeSeconds = Math.Max(0, seconds);
            var hours = safeSeconds / 3600;
            var minutes = safeSeconds % 3600 / 60;
            var remainingSeconds = safeSeconds % 60;
            return hours > 0
                ? $"{hours}:{minutes:00}:{remainingSeconds:00}"
                : $"{minutes}:{remainingSeconds:00}";
        }

        private void WireUi()
        {
            playPauseButton?.onClick.AddListener(SendPlayPause);
            previousButton?.onClick.AddListener(SendPrevious);
            nextButton?.onClick.AddListener(SendNext);
            replayButton?.onClick.AddListener(SendReplay);
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
            if (connectionText != null)
            {
                connectionText.text = "Direct request ready\nBilibili anonymous access";
            }
        }

        private void RefreshCurrentSongText()
        {
            if (currentSongText == null)
            {
                return;
            }

            songBuilder.Clear();
            var item = state?.CurrentItem;
            if (item == null)
            {
                currentSongText.text = "Current: no song selected.";
                return;
            }

            songBuilder.Append("Current: ").Append(SafeText(item.title, "Untitled"));
            if (!string.IsNullOrWhiteSpace(item.author))
            {
                songBuilder.Append("\nAuthor: ").Append(item.author.Trim());
            }
            songBuilder.Append("\nStatus: ").Append(FormatStatus(item.status));
            if (!string.IsNullOrWhiteSpace(item.message))
            {
                songBuilder.Append("  ").Append(item.message.Trim());
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
            var queue = state?.queue ?? Array.Empty<PlaylistItem>();
            queueBuilder.Append("Queue ").Append(queue.Length).Append(" item(s)");
            for (var index = 0; index < queue.Length && index < 3; index += 1)
            {
                var marker = index == state.currentIndex ? ">" : "-";
                queueBuilder.Append("\n").Append(marker).Append(" ")
                    .Append(SafeText(queue[index]?.title, "Untitled"));
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
            playableUrlText.text = item == null || !item.IsReady
                ? "Playable URL: none"
                : $"Playable URL: {ResolvePlayableUrl(item.playableUrl)}";
        }

        private void RefreshButtons()
        {
            var queue = state?.queue ?? Array.Empty<PlaylistItem>();
            var hasQueue = queue.Length > 0;
            var hasReadyItem = state?.CurrentItem?.IsReady == true;
            if (playPauseButton != null)
            {
                playPauseButton.interactable = hasReadyItem;
                SetButtonLabel(playPauseButton,
                    string.Equals(state?.playback, "playing", StringComparison.OrdinalIgnoreCase) ? "Pause" : "Play");
            }
            if (previousButton != null)
            {
                previousButton.interactable = hasQueue;
            }
            if (nextButton != null)
            {
                nextButton.interactable = hasQueue;
            }
            if (replayButton != null)
            {
                replayButton.interactable = hasReadyItem;
            }
        }

        private void CancelCatalogRequests()
        {
            CancelSearch();
            CancelSuggestions();
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

        private static void RemoveLegacyServicePreferences()
        {
            var keys = new[]
            {
                LegacyHelperHostPrefsKey,
                LegacyServiceModePrefsKey,
                LegacyOnlineServiceOriginPrefsKey,
                LegacyServiceSettingsVersionPrefsKey,
                LegacyDeviceIdPrefsKey,
                LegacyOnlineFullCachePrefsKey,
                $"{LegacyDeviceTokenPrefsPrefix}{Hash128.Compute(LegacyPublicOrigin)}",
                $"{LegacyDeviceTokenPrefsPrefix}{Hash128.Compute(LegacyDevelopmentOrigin)}",
            };
            var changed = false;
            for (var index = 0; index < keys.Length; index += 1)
            {
                if (!PlayerPrefs.HasKey(keys[index]))
                {
                    continue;
                }

                PlayerPrefs.DeleteKey(keys[index]);
                changed = true;
            }
            if (changed)
            {
                PlayerPrefs.Save();
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button != null ? button.GetComponentInChildren<Text>() : null;
            if (text != null)
            {
                text.text = label;
            }
        }

        private static string FormatStatus(string status)
        {
            return status switch
            {
                DirectPlaylist.StatusDownloading => "downloading",
                DirectPlaylist.StatusReady => "ready",
                DirectPlaylist.StatusError => "error",
                _ => string.IsNullOrWhiteSpace(status) ? "unknown" : status.Trim(),
            };
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
