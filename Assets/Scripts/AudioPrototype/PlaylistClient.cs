using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TsukiVox.AudioPrototype
{
    public sealed class PlaylistClient
    {
        public const string StatusDownloading = "downloading";
        public const string StatusReady = "ready";
        public const string StatusError = "error";

        public const string ControlPlay = "play";
        public const string ControlPause = "pause";
        public const string ControlPrevious = "prev";
        public const string ControlNext = "next";
        public const string ControlReplay = "replay";

        /// <summary>Clears future entries while preserving history and the current song.</summary>
        public const string ControlClear = "clear";

        /// <summary>Clears everything including the current song. Used at startup.</summary>
        public const string ControlClearAll = "clearAll";

        /// <summary>Clears entries before the current song while preserving current and future entries.</summary>
        public const string ControlClearPlayed = "clearPlayed";

        /// <summary>Clears every entry except the current song, if one exists.</summary>
        public const string ControlClearExceptCurrent = "clearExceptCurrent";

        public const string ControlRemove = "remove";

        private const string DefaultPlaylistOrigin = "http://127.0.0.1:5175";
        private const string DefaultDownloadOrigin = "http://127.0.0.1:5174";
        private const string StatePath = "/api/playlist/state";
        private const string ControlPath = "/api/playlist/control";
        private const string ItemsPath = "/api/playlist/items";
        private const string BilibiliSearchPath = "/api/bilibili/search";
        private const string BilibiliSuggestPath = "/api/bilibili/suggest";
        private const string DeviceEnrollmentPath = "/api/devices/enroll";
        private const string DeviceIdHeader = "X-TsukiVox-Device-Id";
        private const int RequestTimeoutSeconds = 6;
        private const int SearchRequestTimeoutSeconds = 25;

        private string playlistOrigin;
        private string downloadOrigin;
        private string deviceId;
        private string deviceToken;

        public PlaylistClient(
            string playlistOrigin,
            string downloadOrigin,
            string deviceId = "",
            string deviceToken = "")
        {
            SetOrigins(playlistOrigin, downloadOrigin);
            SetDeviceId(deviceId);
            SetDeviceToken(deviceToken);
        }

        public string PlaylistOrigin => playlistOrigin;

        public string DownloadOrigin => downloadOrigin;

        public void SetOrigins(string nextPlaylistOrigin, string nextDownloadOrigin)
        {
            playlistOrigin = NormalizeOrigin(nextPlaylistOrigin, DefaultPlaylistOrigin);
            downloadOrigin = NormalizeOrigin(nextDownloadOrigin, DefaultDownloadOrigin);
        }

        public void SetDeviceId(string nextDeviceId)
        {
            deviceId = string.IsNullOrWhiteSpace(nextDeviceId) ? string.Empty : nextDeviceId.Trim();
        }

        public void SetDeviceToken(string nextDeviceToken)
        {
            deviceToken = string.IsNullOrWhiteSpace(nextDeviceToken) ? string.Empty : nextDeviceToken.Trim();
        }

        public IEnumerator EnrollDevice(
            Action<DeviceEnrollmentResponse> onSuccess,
            Action<PlaylistRequestError> onFailure)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                onFailure?.Invoke(PlaylistRequestError.Local(
                    "DEVICE_ID_REQUIRED",
                    "设备标识尚未准备好。",
                    false));
                yield break;
            }

            using (var request = new UnityWebRequest(
                CombineUrl(playlistOrigin, DeviceEnrollmentPath),
                UnityWebRequest.kHttpVerbPOST))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                ConfigureRequest(request);
                yield return request.SendWebRequest();

                if (!IsRequestSuccessful(request))
                {
                    onFailure?.Invoke(CreateRequestFailure(request, "设备登记"));
                    yield break;
                }

                try
                {
                    var response = JsonUtility.FromJson<DeviceEnrollmentResponse>(request.downloadHandler.text);
                    if (response == null || string.IsNullOrWhiteSpace(response.token))
                    {
                        onFailure?.Invoke(PlaylistRequestError.Local(
                            "INVALID_RESPONSE",
                            "设备登记返回了无效凭证。",
                            false));
                        yield break;
                    }

                    response.Normalize();
                    SetDeviceToken(response.token);
                    onSuccess?.Invoke(response);
                }
                catch (Exception exception)
                {
                    onFailure?.Invoke(PlaylistRequestError.Local(
                        "INVALID_RESPONSE",
                        $"设备登记返回了无效数据：{exception.Message}",
                        false));
                }
            }
        }

        public IEnumerator FetchState(Action<PlaylistState> onSuccess, Action<string> onFailure)
        {
            using (var request = UnityWebRequest.Get(CombineUrl(playlistOrigin, StatePath)))
            {
                ConfigureRequest(request);
                yield return request.SendWebRequest();

                if (!IsRequestSuccessful(request))
                {
                    onFailure?.Invoke(CreateRequestError(request));
                    yield break;
                }

                if (TryParseState(request.downloadHandler.text, out var state, out var error))
                {
                    onSuccess?.Invoke(state);
                    yield break;
                }

                onFailure?.Invoke(error);
            }
        }

        public IEnumerator SendControl(
            string action,
            Action<PlaylistState> onSuccess,
            Action<string> onFailure,
            string itemId = null)
        {
            var requestBody = JsonUtility.ToJson(new PlaylistControlRequest
            {
                action = action,
                id = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim(),
            });

            using (var request = new UnityWebRequest(CombineUrl(playlistOrigin, ControlPath), UnityWebRequest.kHttpVerbPOST))
            {
                var payload = Encoding.UTF8.GetBytes(requestBody);
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                ConfigureRequest(request);

                yield return request.SendWebRequest();

                if (!IsRequestSuccessful(request))
                {
                    onFailure?.Invoke(CreateRequestError(request));
                    yield break;
                }

                if (TryParseState(request.downloadHandler.text, out var state, out var error))
                {
                    onSuccess?.Invoke(state);
                    yield break;
                }

                onFailure?.Invoke(error);
            }
        }

        public IEnumerator SearchBilibili(
            string query,
            int page,
            int pageSize,
            Action<BilibiliSearchResponse> onSuccess,
            Action<string> onFailure)
        {
            var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                onFailure?.Invoke("请输入搜索关键词或 BV 号。");
                yield break;
            }

            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Min(30, Math.Max(1, pageSize));
            var queryString =
                $"?query={UnityWebRequest.EscapeURL(normalizedQuery)}&page={normalizedPage}&pageSize={normalizedPageSize}";

            using (var request = UnityWebRequest.Get(CombineUrl(downloadOrigin, $"{BilibiliSearchPath}{queryString}")))
            {
                ConfigureRequest(request, SearchRequestTimeoutSeconds);
                yield return request.SendWebRequest();

                if (!IsRequestSuccessful(request))
                {
                    onFailure?.Invoke(CreateRequestError(request, "Bilibili search"));
                    yield break;
                }

                if (TryParseBilibiliSearchResponse(request.downloadHandler.text, out var response, out var error))
                {
                    onSuccess?.Invoke(response);
                    yield break;
                }

                onFailure?.Invoke(error);
            }
        }

        public IEnumerator FetchBilibiliSuggestions(
            string term,
            Action<BilibiliSuggestResponse> onSuccess,
            Action<string> onFailure)
        {
            var normalizedTerm = string.IsNullOrWhiteSpace(term) ? string.Empty : term.Trim();
            if (string.IsNullOrEmpty(normalizedTerm))
            {
                onSuccess?.Invoke(new BilibiliSuggestResponse { code = 0, result = new BilibiliSuggestResult { tag = Array.Empty<BilibiliSuggestItem>() } });
                yield break;
            }

            var queryString = $"?term={UnityWebRequest.EscapeURL(normalizedTerm)}";

            using (var request = UnityWebRequest.Get(CombineUrl(downloadOrigin, $"{BilibiliSuggestPath}{queryString}")))
            {
                ConfigureRequest(request, RequestTimeoutSeconds);
                yield return request.SendWebRequest();

                if (!IsRequestSuccessful(request))
                {
                    onFailure?.Invoke(CreateRequestError(request, "Bilibili suggest"));
                    yield break;
                }

                if (TryParseBilibiliSuggestResponse(request.downloadHandler.text, out var response, out var error))
                {
                    onSuccess?.Invoke(response);
                    yield break;
                }

                onFailure?.Invoke(error);
            }
        }

        public IEnumerator AddItem(
            BilibiliCatalogItem item,
            bool playNow,
            Action<PlaylistState> onSuccess,
            Action<string> onFailure)
        {
            if (item == null || !item.IsValid)
            {
                onFailure?.Invoke("请选择有效的 Bilibili 视频。");
                yield break;
            }

            var requestBody = JsonUtility.ToJson(new PlaylistAddRequest
            {
                input = item.bvid.Trim(),
                playNow = playNow,
                title = item.title,
                author = item.author,
                coverUrl = item.coverUrl,
                durationSeconds = Math.Max(0, item.durationSeconds),
                durationText = item.durationText,
                viewCount = Math.Max(0L, item.viewCount),
                pageUrl = item.pageUrl,
            });

            using (var request = CreateJsonPostRequest(CombineUrl(playlistOrigin, ItemsPath), requestBody))
            {
                ConfigureRequest(request);
                yield return request.SendWebRequest();

                if (!IsRequestSuccessful(request))
                {
                    onFailure?.Invoke(CreateRequestError(request, "Add song"));
                    yield break;
                }

                if (TryParseState(request.downloadHandler.text, out var state, out var error))
                {
                    onSuccess?.Invoke(state);
                    yield break;
                }

                onFailure?.Invoke(error);
            }
        }

        public string ResolvePlayableUrl(string playableUrl)
        {
            if (string.IsNullOrWhiteSpace(playableUrl))
            {
                return string.Empty;
            }

            var trimmed = playableUrl.Trim();
            if (!trimmed.StartsWith("/", StringComparison.Ordinal) &&
                Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) &&
                IsPlayableAbsoluteUri(absoluteUri))
            {
                return trimmed;
            }

            if (trimmed.StartsWith("/downloads/", StringComparison.OrdinalIgnoreCase))
            {
                return CombineUrl(downloadOrigin, trimmed);
            }

            if (IsDirectVideoPath(trimmed))
            {
                return CombineUrl(downloadOrigin, trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : $"/{trimmed}");
            }

            return CombineUrl(playlistOrigin, trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : $"/{trimmed}");
        }

        public string ResolveCatalogAssetUrl(string assetUrl)
        {
            if (string.IsNullOrWhiteSpace(assetUrl))
            {
                return string.Empty;
            }

            var trimmed = assetUrl.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) &&
                (string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return trimmed;
            }

            return CombineUrl(downloadOrigin, trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : $"/{trimmed}");
        }

        private static UnityWebRequest CreateJsonPostRequest(string url, string requestBody)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            return request;
        }

        private void ConfigureRequest(UnityWebRequest request, int timeoutSeconds = RequestTimeoutSeconds)
        {
            request.timeout = timeoutSeconds;
            if (!string.IsNullOrEmpty(deviceId))
            {
                request.SetRequestHeader(DeviceIdHeader, deviceId);
            }
            if (!string.IsNullOrEmpty(deviceToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {deviceToken}");
            }
        }

        private static bool IsRequestSuccessful(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success &&
                   request.responseCode >= 200 &&
                   request.responseCode < 300;
        }

        private static bool TryParseState(string json, out PlaylistState state, out string error)
        {
            try
            {
                state = JsonUtility.FromJson<PlaylistState>(json);
                if (state == null)
                {
                    error = "Playlist sync returned an empty state.";
                    return false;
                }

                state.Normalize();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                state = null;
                error = $"Playlist sync returned invalid JSON: {exception.Message}";
                return false;
            }
        }

        private static bool TryParseBilibiliSearchResponse(
            string json,
            out BilibiliSearchResponse response,
            out string error)
        {
            try
            {
                response = JsonUtility.FromJson<BilibiliSearchResponse>(json);
                if (response == null)
                {
                    error = "Bilibili search returned an empty response.";
                    return false;
                }

                response.Normalize();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                response = null;
                error = $"Bilibili search returned invalid JSON: {exception.Message}";
                return false;
            }
        }

        private static bool TryParseBilibiliSuggestResponse(
            string json,
            out BilibiliSuggestResponse response,
            out string error)
        {
            try
            {
                response = JsonUtility.FromJson<BilibiliSuggestResponse>(json);
                if (response == null)
                {
                    error = "Bilibili suggest returned an empty response.";
                    return false;
                }

                response.Normalize();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                response = null;
                error = $"Bilibili suggest returned invalid JSON: {exception.Message}";
                return false;
            }
        }

        private static string CreateRequestError(UnityWebRequest request, string operation = "Playlist sync")
        {
            var serverMessage = TryParseErrorMessage(request.downloadHandler?.text);
            if (!string.IsNullOrEmpty(serverMessage))
            {
                return serverMessage;
            }

            var transportError = string.IsNullOrEmpty(request.error) ? "request failed" : request.error;
            return $"{operation} {transportError} ({request.responseCode}).";
        }

        /// <summary>
        /// Builds a structured failure so callers can branch on the server error code
        /// instead of matching free-text messages.
        /// </summary>
        private static PlaylistRequestError CreateRequestFailure(UnityWebRequest request, string operation)
        {
            var parsed = TryParseErrorResponse(request.downloadHandler?.text);
            if (parsed != null && !string.IsNullOrEmpty(parsed.code))
            {
                return new PlaylistRequestError
                {
                    code = parsed.code,
                    message = string.IsNullOrEmpty(parsed.error) ? $"{operation}失败。" : parsed.error,
                    retryable = parsed.retryable,
                    responseCode = (int)request.responseCode,
                };
            }

            // No structured body: distinguish transport failures from HTTP errors so the
            // UI can say "network unreachable" instead of a generic failure.
            var isTransport = request.result == UnityWebRequest.Result.ConnectionError ||
                              request.responseCode == 0;
            return new PlaylistRequestError
            {
                code = isTransport ? "NETWORK_UNREACHABLE" : "SERVER_ERROR",
                message = !string.IsNullOrEmpty(parsed?.error)
                    ? parsed.error
                    : $"{operation} {(string.IsNullOrEmpty(request.error) ? "请求失败" : request.error)} ({request.responseCode})。",
                retryable = isTransport || request.responseCode >= 500,
                responseCode = (int)request.responseCode,
            };
        }

        private static string TryParseErrorMessage(string json)
        {
            return TryParseErrorResponse(json)?.error ?? string.Empty;
        }

        private static PlaylistErrorResponse TryParseErrorResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<PlaylistErrorResponse>(json);
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeOrigin(string origin, string fallback)
        {
            var normalized = string.IsNullOrWhiteSpace(origin) ? fallback : origin.Trim();
            return normalized.TrimEnd('/');
        }

        private static string CombineUrl(string origin, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return origin;
            }

            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                return $"{origin}{path}";
            }

            return $"{origin}/{path}";
        }

        private static bool IsDirectVideoPath(string path)
        {
            var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
            var pathWithoutQuery = queryIndex >= 0 ? path[..queryIndex] : path;
            return pathWithoutQuery.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                   pathWithoutQuery.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlayableAbsoluteUri(Uri uri)
        {
            return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
        }

        [Serializable]
        private sealed class PlaylistControlRequest
        {
            public string action;
            public string id;
        }

        [Serializable]
        private sealed class PlaylistAddRequest
        {
            public string input;
            public bool playNow;
            public string title;
            public string author;
            public string coverUrl;
            public int durationSeconds;
            public string durationText;
            public long viewCount;
            public string pageUrl;
        }

        [Serializable]
        private sealed class PlaylistErrorResponse
        {
            public string error;
            public string code;
            public bool retryable;
        }
    }

    [Serializable]
    public sealed class DeviceEnrollmentResponse
    {
        public string deviceId;
        public string token;
        public string tokenType;

        public void Normalize()
        {
            deviceId = string.IsNullOrWhiteSpace(deviceId) ? string.Empty : deviceId.Trim();
            token = string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim();
            tokenType = string.IsNullOrWhiteSpace(tokenType) ? "Bearer" : tokenType.Trim();
        }
    }

    /// <summary>
    /// Structured request failure. The server reports a machine-readable
    /// <c>code</c> alongside the human message for actionable client errors.
    /// </summary>
    [Serializable]
    public sealed class PlaylistRequestError
    {
        public string code;
        public string message;
        public bool retryable;
        public int responseCode;

        public static PlaylistRequestError Local(string code, string message, bool retryable)
        {
            return new PlaylistRequestError
            {
                code = code,
                message = message,
                retryable = retryable,
                responseCode = 0,
            };
        }

    }

    [Serializable]
    public sealed class BilibiliSearchResponse
    {
        public string query;
        public int page = 1;
        public int pageSize = 4;
        public int total;
        public bool hasMore;
        public BilibiliCatalogItem[] items = Array.Empty<BilibiliCatalogItem>();

        public int ItemCount => items == null ? 0 : items.Length;

        public void Normalize()
        {
            query ??= string.Empty;
            page = Math.Max(1, page);
            pageSize = Math.Max(1, pageSize);
            total = Math.Max(0, total);
            items ??= Array.Empty<BilibiliCatalogItem>();
            for (var index = 0; index < items.Length; index += 1)
            {
                items[index]?.Normalize();
            }
        }
    }

    [Serializable]
    public sealed class BilibiliSuggestResponse
    {
        public int code;
        public BilibiliSuggestResult result;

        public void Normalize()
        {
            result ??= new BilibiliSuggestResult();
            result.Normalize();
        }
    }

    [Serializable]
    public sealed class BilibiliSuggestResult
    {
        public BilibiliSuggestItem[] tag = Array.Empty<BilibiliSuggestItem>();

        public void Normalize()
        {
            tag ??= Array.Empty<BilibiliSuggestItem>();
            for (var index = 0; index < tag.Length; index += 1)
            {
                tag[index]?.Normalize();
            }
        }
    }

    [Serializable]
    public sealed class BilibiliSuggestItem
    {
        public string value;
        public string name;

        public void Normalize()
        {
            value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        }
    }

    [Serializable]
    public sealed class BilibiliCatalogItem
    {
        public string bvid;
        public string title;
        public string author;
        public string coverUrl;
        public int durationSeconds;
        public string durationText;
        public long viewCount;
        public string pageUrl;

        public bool IsValid => !string.IsNullOrWhiteSpace(bvid) &&
                               bvid.Trim().StartsWith("BV", StringComparison.OrdinalIgnoreCase);

        public void Normalize()
        {
            bvid = string.IsNullOrWhiteSpace(bvid) ? string.Empty : bvid.Trim();
            title = string.IsNullOrWhiteSpace(title) ? bvid : title.Trim();
            author = string.IsNullOrWhiteSpace(author) ? string.Empty : author.Trim();
            coverUrl = string.IsNullOrWhiteSpace(coverUrl) ? string.Empty : coverUrl.Trim();
            durationSeconds = Math.Max(0, durationSeconds);
            durationText = string.IsNullOrWhiteSpace(durationText) ? FormatDuration(durationSeconds) : durationText.Trim();
            viewCount = Math.Max(0L, viewCount);
            pageUrl = string.IsNullOrWhiteSpace(pageUrl)
                ? (string.IsNullOrEmpty(bvid) ? string.Empty : $"https://www.bilibili.com/video/{bvid}/")
                : pageUrl.Trim();
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
    }

    [Serializable]
    public sealed class PlaylistState
    {
        public PlaylistItem[] queue = Array.Empty<PlaylistItem>();
        public int currentIndex = -1;
        public string playback = "paused";
        public long updatedAt;
        public PlaylistCommand command;

        public int QueueCount => queue == null ? 0 : queue.Length;

        public PlaylistItem CurrentItem
        {
            get
            {
                if (queue == null || currentIndex < 0 || currentIndex >= queue.Length)
                {
                    return null;
                }

                return queue[currentIndex];
            }
        }

        public void Normalize()
        {
            queue ??= Array.Empty<PlaylistItem>();
            playback = string.IsNullOrEmpty(playback) ? "paused" : playback;
        }
    }

    [Serializable]
    public sealed class PlaylistItem
    {
        public string id;
        public string title;
        public string author;
        public string coverUrl;
        public int durationSeconds;
        public string durationText;
        public string sourceType;
        public string sourceInput;
        public string jobId;
        public string playableUrl;
        public string cacheUrl;
        public string streamUrl;
        public string status;
        public string message;
        public float progress;

        public bool IsReady => string.Equals(status, PlaylistClient.StatusReady, StringComparison.OrdinalIgnoreCase) &&
                               (!string.IsNullOrWhiteSpace(playableUrl) ||
                                !string.IsNullOrWhiteSpace(cacheUrl) ||
                                !string.IsNullOrWhiteSpace(streamUrl));
    }

    [Serializable]
    public sealed class PlaylistCommand
    {
        public string action;
        public string id;
        public long issuedAt;
    }
}
