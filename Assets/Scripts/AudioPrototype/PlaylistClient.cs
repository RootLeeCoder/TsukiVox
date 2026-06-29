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

        private const string DefaultPlaylistOrigin = "http://127.0.0.1:5175";
        private const string DefaultDownloadOrigin = "http://127.0.0.1:5174";
        private const string StatePath = "/api/playlist/state";
        private const string ControlPath = "/api/playlist/control";
        private const int RequestTimeoutSeconds = 6;

        private string playlistOrigin;
        private string downloadOrigin;

        public PlaylistClient(string playlistOrigin, string downloadOrigin)
        {
            SetOrigins(playlistOrigin, downloadOrigin);
        }

        public string PlaylistOrigin => playlistOrigin;

        public string DownloadOrigin => downloadOrigin;

        public void SetOrigins(string nextPlaylistOrigin, string nextDownloadOrigin)
        {
            playlistOrigin = NormalizeOrigin(nextPlaylistOrigin, DefaultPlaylistOrigin);
            downloadOrigin = NormalizeOrigin(nextDownloadOrigin, DefaultDownloadOrigin);
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

        public IEnumerator SendControl(string action, Action<PlaylistState> onSuccess, Action<string> onFailure)
        {
            var requestBody = JsonUtility.ToJson(new PlaylistControlRequest
            {
                action = action,
                id = string.Empty,
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

        private static void ConfigureRequest(UnityWebRequest request)
        {
            request.timeout = RequestTimeoutSeconds;
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

        private static string CreateRequestError(UnityWebRequest request)
        {
            var serverMessage = TryParseErrorMessage(request.downloadHandler?.text);
            if (!string.IsNullOrEmpty(serverMessage))
            {
                return serverMessage;
            }

            var transportError = string.IsNullOrEmpty(request.error) ? "request failed" : request.error;
            return $"Playlist sync {transportError} ({request.responseCode}).";
        }

        private static string TryParseErrorMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                var response = JsonUtility.FromJson<PlaylistErrorResponse>(json);
                return response == null ? string.Empty : response.error;
            }
            catch
            {
                return string.Empty;
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
        private sealed class PlaylistErrorResponse
        {
            public string error;
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
        public string sourceType;
        public string sourceInput;
        public string playableUrl;
        public string status;
        public string message;

        public bool IsReady => string.Equals(status, PlaylistClient.StatusReady, StringComparison.OrdinalIgnoreCase) &&
                               !string.IsNullOrWhiteSpace(playableUrl);
    }

    [Serializable]
    public sealed class PlaylistCommand
    {
        public string action;
        public string id;
        public long issuedAt;
    }
}
