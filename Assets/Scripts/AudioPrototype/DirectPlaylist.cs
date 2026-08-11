using System;

namespace TsukiVox.AudioPrototype
{
    public static class DirectPlaylist
    {
        public const string StatusDownloading = "downloading";
        public const string StatusReady = "ready";
        public const string StatusError = "error";

        public const string ControlPlay = "play";
        public const string ControlPause = "pause";
        public const string ControlPrevious = "prev";
        public const string ControlNext = "next";
        public const string ControlReplay = "replay";
        public const string ControlClear = "clear";
        public const string ControlClearPlayed = "clearPlayed";
        public const string ControlClearExceptCurrent = "clearExceptCurrent";
        public const string ControlClearAll = "clearAll";
        public const string ControlRemove = "remove";
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
            durationText = string.IsNullOrWhiteSpace(durationText)
                ? FormatDuration(durationSeconds)
                : durationText.Trim();
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
            playback = string.Equals(playback, "playing", StringComparison.OrdinalIgnoreCase)
                ? "playing"
                : "paused";
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

        public bool IsReady => string.Equals(status, DirectPlaylist.StatusReady, StringComparison.OrdinalIgnoreCase) &&
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
