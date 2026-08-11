using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace TsukiVox.AudioPrototype
{
    /// <summary>
    /// Anonymous Bilibili access used by the on-headset Direct Request mode.
    /// It intentionally has no TsukiVox device identity or service credentials.
    /// </summary>
    public sealed class BilibiliDirectClient
    {
        public const string ApiOrigin = "https://api.bilibili.com";
        public const string SuggestOrigin = "https://s.search.bilibili.com";

        private const string BilibiliReferer = "https://www.bilibili.com/";
        private const string SearchReferer = "https://search.bilibili.com/";
        private const string SearchOrigin = "https://search.bilibili.com";
        private const string DesktopUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0 Safari/537.36";
        private const int RequestTimeoutSeconds = 30;
        private const int MediaDownloadTimeoutSeconds = 900;
        private const int MaximumRedirectHops = 6;

        private static readonly Regex BvidPattern = new Regex(
            @"(?i)(?<![0-9a-z])(BV[0-9a-z]{10})(?![0-9a-z])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex HtmlTagPattern = new Regex(
            @"<[^>]+>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private UnityWebRequest activeSearchRequest;
        private UnityWebRequest activeSuggestRequest;
        private UnityWebRequest activeDownloadRequest;
        private string anonymousSearchCookie = string.Empty;
        private int searchCancellationVersion;
        private int suggestCancellationVersion;
        private int downloadCancellationVersion;

        public IEnumerator Search(
            string query,
            int page,
            int pageSize,
            Action<BilibiliSearchResponse> onSuccess,
            Action<string> onFailure)
        {
            var cancellationVersion = searchCancellationVersion;
            var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                onFailure?.Invoke("请输入搜索关键词、BV 号或 B 站链接。");
                yield break;
            }

            var normalizedPage = Mathf.Clamp(page, 1, 100);
            var normalizedPageSize = Mathf.Clamp(pageSize, 1, 20);
            if (TryClassifyDirectInput(normalizedQuery, out var input, out var isShortLink))
            {
                if (isShortLink)
                {
                    DirectBilibiliInput resolvedInput = null;
                    string redirectError = null;
                    yield return ResolveShortLink(
                        normalizedQuery,
                        RequestChannel.Search,
                        value => resolvedInput = value,
                        value => redirectError = value);
                    if (cancellationVersion != searchCancellationVersion)
                    {
                        yield break;
                    }
                    if (resolvedInput == null)
                    {
                        onFailure?.Invoke(redirectError ?? "短链接没有指向可识别的 B 站视频。");
                        yield break;
                    }
                    input = resolvedInput;
                }

                BilibiliViewData view = null;
                string viewError = null;
                yield return FetchView(
                    input,
                    RequestChannel.Search,
                    value => view = value,
                    value => viewError = value);
                if (cancellationVersion != searchCancellationVersion)
                {
                    yield break;
                }
                if (view == null)
                {
                    onFailure?.Invoke(viewError ?? "没有找到这个视频。");
                    yield break;
                }

                var item = CatalogItemFromView(view, input.pageNumber);
                onSuccess?.Invoke(new BilibiliSearchResponse
                {
                    query = normalizedQuery,
                    page = 1,
                    pageSize = 1,
                    total = 1,
                    hasMore = false,
                    items = new[] { item },
                });
                yield break;
            }

            yield return EnsureAnonymousSearchSession();
            if (cancellationVersion != searchCancellationVersion)
            {
                yield break;
            }

            var searchUrl =
                $"{ApiOrigin}/x/web-interface/search/type" +
                $"?search_type=video&keyword={UnityWebRequest.EscapeURL(normalizedQuery)}" +
                $"&page={normalizedPage}&page_size={normalizedPageSize}";
            string responseBody = null;
            string requestError = null;
            yield return RequestJson(
                searchUrl,
                SearchReferer,
                anonymousSearchCookie,
                RequestChannel.Search,
                2,
                value => responseBody = value,
                value => requestError = value);
            if (cancellationVersion != searchCancellationVersion)
            {
                yield break;
            }
            if (string.IsNullOrEmpty(responseBody))
            {
                onFailure?.Invoke(requestError ?? "B 站搜索暂时不可用。");
                yield break;
            }

            BilibiliSearchEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<BilibiliSearchEnvelope>(responseBody);
            }
            catch (Exception exception)
            {
                onFailure?.Invoke($"B 站搜索返回了无法解析的数据：{exception.Message}");
                yield break;
            }

            if (envelope == null || envelope.code != 0 || envelope.data == null)
            {
                onFailure?.Invoke(DescribeApiFailure(envelope?.code ?? int.MinValue, envelope?.message, "B 站搜索暂时不可用。"));
                yield break;
            }

            var rawItems = envelope.data.result ?? Array.Empty<BilibiliSearchItem>();
            var items = new List<BilibiliCatalogItem>(rawItems.Length);
            for (var index = 0; index < rawItems.Length; index += 1)
            {
                var item = CatalogItemFromSearch(rawItems[index]);
                if (item != null && item.IsValid)
                {
                    items.Add(item);
                }
            }

            var total = Math.Max(0, envelope.data.numResults > 0
                ? envelope.data.numResults
                : envelope.data.num_results);
            onSuccess?.Invoke(new BilibiliSearchResponse
            {
                query = normalizedQuery,
                page = normalizedPage,
                pageSize = normalizedPageSize,
                total = total,
                hasMore = (long)normalizedPage * normalizedPageSize < total,
                items = items.ToArray(),
            });
        }

        public IEnumerator FetchSuggestions(
            string term,
            int limit,
            Action<BilibiliSuggestResponse> onSuccess,
            Action<string> onFailure)
        {
            var cancellationVersion = suggestCancellationVersion;
            var normalizedLimit = Mathf.Clamp(limit, 1, 20);
            var normalizedTerm = string.IsNullOrWhiteSpace(term) ? string.Empty : term.Trim();
            if (string.IsNullOrEmpty(normalizedTerm))
            {
                onSuccess?.Invoke(EmptySuggestions());
                yield break;
            }

            var url =
                $"{SuggestOrigin}/main/suggest" +
                $"?term={UnityWebRequest.EscapeURL(normalizedTerm)}&main_ver=v1";
            string responseBody = null;
            string requestError = null;
            yield return RequestJson(
                url,
                BilibiliReferer,
                string.Empty,
                RequestChannel.Suggest,
                1,
                value => responseBody = value,
                value => requestError = value);
            if (cancellationVersion != suggestCancellationVersion)
            {
                yield break;
            }
            if (string.IsNullOrEmpty(responseBody))
            {
                onFailure?.Invoke(requestError ?? "B 站搜索建议暂时不可用。");
                yield break;
            }

            BilibiliSuggestResponse response;
            try
            {
                response = JsonUtility.FromJson<BilibiliSuggestResponse>(responseBody);
            }
            catch (Exception exception)
            {
                onFailure?.Invoke($"B 站搜索建议返回了无法解析的数据：{exception.Message}");
                yield break;
            }

            if (response == null || response.code != 0)
            {
                onFailure?.Invoke("B 站搜索建议暂时不可用。");
                yield break;
            }

            response.Normalize();
            var tags = response.result.tag ?? Array.Empty<BilibiliSuggestItem>();
            var unique = new List<BilibiliSuggestItem>(Math.Min(normalizedLimit, tags.Length));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < tags.Length && unique.Count < normalizedLimit; index += 1)
            {
                var candidate = tags[index];
                if (candidate == null)
                {
                    continue;
                }

                candidate.Normalize();
                candidate.value = CleanText(candidate.value);
                candidate.name = CleanText(candidate.name);
                if (!string.IsNullOrEmpty(candidate.value) && seen.Add(candidate.value))
                {
                    if (string.IsNullOrEmpty(candidate.name))
                    {
                        candidate.name = candidate.value;
                    }
                    unique.Add(candidate);
                }
            }

            response.result.tag = unique.ToArray();
            onSuccess?.Invoke(response);
        }

        public IEnumerator ResolveAndCache(
            BilibiliCatalogItem catalogItem,
            string cacheDirectory,
            Action<DirectBilibiliMedia> onResolved,
            Action<float> onProgress,
            Action<DirectBilibiliMedia> onCompleted,
            Action<string> onFailure)
        {
            var cancellationVersion = downloadCancellationVersion;
            var sourceInput = !string.IsNullOrWhiteSpace(catalogItem?.pageUrl)
                ? catalogItem.pageUrl
                : catalogItem?.bvid;
            if (!TryParseBilibiliInput(sourceInput, out var input))
            {
                onFailure?.Invoke("点播内容没有有效的 BV 号。");
                yield break;
            }

            BilibiliViewData view = null;
            string viewError = null;
            yield return FetchView(
                input,
                RequestChannel.Download,
                value => view = value,
                value => viewError = value);
            if (cancellationVersion != downloadCancellationVersion)
            {
                yield break;
            }
            if (view == null)
            {
                onFailure?.Invoke(viewError ?? "没有找到这个视频。");
                yield break;
            }

            var pages = view.pages ?? Array.Empty<BilibiliPageData>();
            if (input.pageNumber < 1 || input.pageNumber > pages.Length)
            {
                onFailure?.Invoke($"这个视频没有第 {input.pageNumber} 个分P。");
                yield break;
            }

            var selectedPage = pages[input.pageNumber - 1];
            if (selectedPage == null || selectedPage.cid <= 0L)
            {
                onFailure?.Invoke("B 站返回的视频分P信息不完整。");
                yield break;
            }

            var bvid = string.IsNullOrWhiteSpace(view.bvid) ? input.bvid : view.bvid.Trim();
            var referer = $"https://www.bilibili.com/video/{bvid}/?p={input.pageNumber}";
            var playUrl =
                $"{ApiOrigin}/x/player/playurl" +
                $"?bvid={UnityWebRequest.EscapeURL(bvid)}&cid={selectedPage.cid}" +
                "&qn=32&fnver=0&fnval=0&fourk=0";
            string playbackBody = null;
            string playbackError = null;
            yield return RequestJson(
                playUrl,
                referer,
                string.Empty,
                RequestChannel.Download,
                1,
                value => playbackBody = value,
                value => playbackError = value);
            if (cancellationVersion != downloadCancellationVersion)
            {
                yield break;
            }
            if (string.IsNullOrEmpty(playbackBody))
            {
                onFailure?.Invoke(playbackError ?? "这个视频暂时无法匿名下载。");
                yield break;
            }

            BilibiliPlayEnvelope playback;
            try
            {
                playback = JsonUtility.FromJson<BilibiliPlayEnvelope>(playbackBody);
            }
            catch (Exception exception)
            {
                onFailure?.Invoke($"B 站播放信息无法解析：{exception.Message}");
                yield break;
            }

            if (playback == null || playback.code != 0 || playback.data == null)
            {
                onFailure?.Invoke(DescribeApiFailure(
                    playback?.code ?? int.MinValue,
                    playback?.message,
                    "这个视频暂时无法匿名下载。"));
                yield break;
            }

            var streams = playback.data.durl ?? Array.Empty<BilibiliDurlData>();
            if (streams.Length != 1 || streams[0] == null || string.IsNullOrWhiteSpace(streams[0].url))
            {
                onFailure?.Invoke("这个视频不是可直接播放的单文件 MP4，当前直接请求不支持合并分段。");
                yield break;
            }
            if (!LooksLikeMp4(playback.data.format, streams[0].url))
            {
                onFailure?.Invoke("这个视频没有返回 Quest 可直接播放的 MP4 文件。");
                yield break;
            }
            if (!IsHttpUrl(streams[0].url))
            {
                onFailure?.Invoke("B 站返回了不受支持的视频下载地址。");
                yield break;
            }

            var actualHeight = HeightForQuality(playback.data.quality);
            var media = new DirectBilibiliMedia
            {
                bvid = bvid,
                title = CleanText(string.IsNullOrWhiteSpace(view.title) ? bvid : view.title),
                author = CleanText(view.owner?.name),
                coverUrl = NormalizeImageUrl(view.pic),
                pageNumber = input.pageNumber,
                partTitle = CleanText(selectedPage.part),
                durationSeconds = Math.Max(0, selectedPage.duration > 0 ? selectedPage.duration : view.duration),
                actualHeight = actualHeight,
                qualityLabel = FindQualityLabel(playback.data, playback.data.quality, actualHeight),
                estimatedBytes = streams[0].size,
                referer = referer,
            };

            string cachePath;
            try
            {
                Directory.CreateDirectory(cacheDirectory);
                cachePath = Path.Combine(
                    cacheDirectory,
                    $"{SanitizeFileSegment(bvid)}-p{input.pageNumber}-{actualHeight}p.mp4");
                media.filePath = cachePath;
                media.fileUrl = ToFileUrl(cachePath);
            }
            catch (Exception exception)
            {
                onFailure?.Invoke($"无法创建头显视频缓存：{exception.Message}");
                yield break;
            }
            onResolved?.Invoke(media);

            if (!TryGetFileLength(cachePath, out var cachedLength, out var cacheReadError))
            {
                onFailure?.Invoke(cacheReadError);
                yield break;
            }
            if (cachedLength > 0L)
            {
                media.bytesWritten = cachedLength;
                onProgress?.Invoke(1f);
                onCompleted?.Invoke(media);
                yield break;
            }

            var downloadUrls = new List<string> { streams[0].url.Trim() };
            var backups = streams[0].backup_url ?? Array.Empty<string>();
            for (var index = 0; index < backups.Length; index += 1)
            {
                if (!string.IsNullOrWhiteSpace(backups[index]) &&
                    IsHttpUrl(backups[index]) &&
                    !downloadUrls.Contains(backups[index].Trim()))
                {
                    downloadUrls.Add(backups[index].Trim());
                }
            }

            string lastDownloadError = null;
            for (var index = 0; index < downloadUrls.Count; index += 1)
            {
                if (cancellationVersion != downloadCancellationVersion)
                {
                    yield break;
                }

                var temporaryPath = $"{cachePath}.download";
                TryDeleteFile(temporaryPath);
                using (var request = new UnityWebRequest(downloadUrls[index], UnityWebRequest.kHttpVerbGET))
                {
                    DownloadHandlerFile handler;
                    try
                    {
                        handler = new DownloadHandlerFile(temporaryPath)
                        {
                            removeFileOnAbort = true,
                        };
                    }
                    catch (Exception exception)
                    {
                        onFailure?.Invoke($"无法打开头显视频缓存：{exception.Message}");
                        yield break;
                    }
                    request.downloadHandler = handler;
                    ConfigureBilibiliRequest(
                        request,
                        referer,
                        string.Empty,
                        MediaDownloadTimeoutSeconds);
                    SetActiveRequest(RequestChannel.Download, request);
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        if (cancellationVersion != downloadCancellationVersion)
                        {
                            request.Abort();
                            break;
                        }

                        var progress = request.downloadProgress;
                        if (progress < 0f && media.estimatedBytes > 0L)
                        {
                            progress = Mathf.Clamp01((float)request.downloadedBytes / media.estimatedBytes);
                        }
                        onProgress?.Invoke(Mathf.Clamp01(progress));
                        yield return null;
                    }
                    ClearActiveRequest(RequestChannel.Download, request);

                    if (cancellationVersion != downloadCancellationVersion)
                    {
                        TryDeleteFile(temporaryPath);
                        yield break;
                    }

                    if (!IsRequestSuccessful(request))
                    {
                        lastDownloadError = request.responseCode > 0
                            ? $"视频文件请求失败（HTTP {request.responseCode}）。"
                            : $"视频文件下载失败：{request.error}";
                        TryDeleteFile(temporaryPath);
                        continue;
                    }
                }

                if (!TryGetFileLength(temporaryPath, out var downloadedLength, out var downloadReadError))
                {
                    lastDownloadError = downloadReadError;
                    TryDeleteFile(temporaryPath);
                    continue;
                }
                if (downloadedLength <= 0L)
                {
                    lastDownloadError = "B 站没有返回可播放的视频文件。";
                    TryDeleteFile(temporaryPath);
                    continue;
                }

                try
                {
                    if (File.Exists(cachePath))
                    {
                        File.Delete(cachePath);
                    }
                    File.Move(temporaryPath, cachePath);
                    media.bytesWritten = new FileInfo(cachePath).Length;
                }
                catch (Exception exception)
                {
                    TryDeleteFile(temporaryPath);
                    onFailure?.Invoke($"无法保存头显视频缓存：{exception.Message}");
                    yield break;
                }
                onProgress?.Invoke(1f);
                onCompleted?.Invoke(media);
                yield break;
            }

            onFailure?.Invoke(lastDownloadError ?? "视频文件下载失败，播放地址可能已过期，请重新点播。");
        }

        public void CancelSearch()
        {
            searchCancellationVersion += 1;
            AbortRequest(ref activeSearchRequest);
        }

        public void CancelSuggestions()
        {
            suggestCancellationVersion += 1;
            AbortRequest(ref activeSuggestRequest);
        }

        public void CancelDownload()
        {
            downloadCancellationVersion += 1;
            AbortRequest(ref activeDownloadRequest);
        }

        public void CancelAll()
        {
            CancelSearch();
            CancelSuggestions();
            CancelDownload();
        }

        private IEnumerator EnsureAnonymousSearchSession()
        {
            if (!string.IsNullOrEmpty(anonymousSearchCookie))
            {
                yield break;
            }

            string responseBody = null;
            yield return RequestJson(
                $"{ApiOrigin}/x/frontend/finger/spi",
                BilibiliReferer,
                string.Empty,
                RequestChannel.Search,
                0,
                value => responseBody = value,
                _ => { });
            if (string.IsNullOrEmpty(responseBody))
            {
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<BilibiliFingerprintEnvelope>(responseBody);
                if (response?.code == 0 &&
                    !string.IsNullOrWhiteSpace(response.data?.b_3) &&
                    !string.IsNullOrWhiteSpace(response.data?.b_4))
                {
                    anonymousSearchCookie =
                        $"buvid3={response.data.b_3.Trim()}; " +
                        $"buvid4={response.data.b_4.Trim()}; CURRENT_FNVAL=4048";
                }
            }
            catch (Exception)
            {
                // Search can still succeed without the anonymous fingerprint.
            }
        }

        private IEnumerator FetchView(
            DirectBilibiliInput input,
            RequestChannel channel,
            Action<BilibiliViewData> onSuccess,
            Action<string> onFailure)
        {
            var cancellationVersion = GetCancellationVersion(channel);
            var url = $"{ApiOrigin}/x/web-interface/view?bvid={UnityWebRequest.EscapeURL(input.bvid)}";
            string responseBody = null;
            string requestError = null;
            yield return RequestJson(
                url,
                BilibiliReferer,
                string.Empty,
                channel,
                2,
                value => responseBody = value,
                value => requestError = value);
            if (cancellationVersion != GetCancellationVersion(channel))
            {
                yield break;
            }
            if (string.IsNullOrEmpty(responseBody))
            {
                onFailure?.Invoke(requestError ?? "没有找到这个视频。");
                yield break;
            }

            BilibiliViewEnvelope response;
            try
            {
                response = JsonUtility.FromJson<BilibiliViewEnvelope>(responseBody);
            }
            catch (Exception exception)
            {
                onFailure?.Invoke($"B 站视频信息无法解析：{exception.Message}");
                yield break;
            }

            if (response == null || response.code != 0 || response.data == null)
            {
                onFailure?.Invoke(DescribeApiFailure(
                    response?.code ?? int.MinValue,
                    response?.message,
                    "没有找到这个视频。"));
                yield break;
            }

            onSuccess?.Invoke(response.data);
        }

        private IEnumerator ResolveShortLink(
            string input,
            RequestChannel channel,
            Action<DirectBilibiliInput> onSuccess,
            Action<string> onFailure)
        {
            var cancellationVersion = GetCancellationVersion(channel);
            var currentUrl = input.Trim();
            for (var hop = 0; hop < MaximumRedirectHops; hop += 1)
            {
                if (cancellationVersion != GetCancellationVersion(channel))
                {
                    yield break;
                }

                using (var request = UnityWebRequest.Get(currentUrl))
                {
                    request.redirectLimit = 0;
                    ConfigureBilibiliRequest(request, BilibiliReferer, string.Empty);
                    SetActiveRequest(channel, request);
                    yield return request.SendWebRequest();
                    ClearActiveRequest(channel, request);
                    if (cancellationVersion != GetCancellationVersion(channel))
                    {
                        yield break;
                    }

                    if (request.responseCode >= 300L && request.responseCode < 400L)
                    {
                        var location = request.GetResponseHeader("Location");
                        if (string.IsNullOrWhiteSpace(location))
                        {
                            onFailure?.Invoke("B 站短链接没有返回跳转地址。");
                            yield break;
                        }

                        try
                        {
                            var redirectedUri = new Uri(new Uri(currentUrl), location.Trim());
                            if (!IsAllowedBilibiliRedirect(redirectedUri))
                            {
                                onFailure?.Invoke("B 站短链接跳转到了不受支持的地址。");
                                yield break;
                            }
                            currentUrl = redirectedUri.AbsoluteUri;
                        }
                        catch (Exception)
                        {
                            onFailure?.Invoke("B 站短链接返回了无效跳转地址。");
                            yield break;
                        }

                        if (TryParseBilibiliInput(currentUrl, out var redirectedInput))
                        {
                            onSuccess?.Invoke(redirectedInput);
                            yield break;
                        }
                        continue;
                    }

                    if (!IsRequestSuccessful(request))
                    {
                        onFailure?.Invoke(request.responseCode > 0
                            ? $"B 站短链接请求失败（HTTP {request.responseCode}）。"
                            : $"无法打开 B 站短链接：{request.error}");
                        yield break;
                    }

                    if (TryParseBilibiliInput(currentUrl, out var finalInput) ||
                        TryParseBilibiliInput(request.url, out finalInput))
                    {
                        onSuccess?.Invoke(finalInput);
                        yield break;
                    }
                }
            }

            onFailure?.Invoke("B 站短链接跳转次数过多。");
        }

        private IEnumerator RequestJson(
            string url,
            string referer,
            string cookie,
            RequestChannel channel,
            int retries,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            var cancellationVersion = GetCancellationVersion(channel);
            for (var attempt = 0; attempt <= Math.Max(0, retries); attempt += 1)
            {
                if (cancellationVersion != GetCancellationVersion(channel))
                {
                    yield break;
                }

                bool shouldRetry;
                string error;
                using (var request = UnityWebRequest.Get(url))
                {
                    ConfigureBilibiliRequest(request, referer, cookie);
                    SetActiveRequest(channel, request);
                    yield return request.SendWebRequest();
                    ClearActiveRequest(channel, request);
                    if (cancellationVersion != GetCancellationVersion(channel))
                    {
                        yield break;
                    }

                    var body = request.downloadHandler?.text;
                    var apiCode = ReadApiCode(body);
                    var cancelled = IsCancelledRequest(request);
                    var transientFailure = !cancelled &&
                                           (request.result == UnityWebRequest.Result.ConnectionError ||
                                            request.responseCode == 408L ||
                                            request.responseCode == 429L ||
                                            request.responseCode >= 500L);
                    shouldRetry = !cancelled &&
                                  (request.responseCode == 412L ||
                                   apiCode == -412 ||
                                   apiCode == -509 ||
                                   transientFailure);
                    if (IsRequestSuccessful(request) && !shouldRetry)
                    {
                        onSuccess?.Invoke(body ?? string.Empty);
                        yield break;
                    }

                    error = shouldRetry
                        ? "B 站暂时限制了请求，请稍后再试。"
                        : request.responseCode > 0
                            ? $"B 站请求失败（HTTP {request.responseCode}）。"
                            : $"无法连接 B 站：{request.error}";
                }

                if (!shouldRetry || attempt >= retries)
                {
                    onFailure?.Invoke(error);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.5f * (1 << attempt));
                if (cancellationVersion != GetCancellationVersion(channel))
                {
                    yield break;
                }
            }
        }

        private int GetCancellationVersion(RequestChannel channel)
        {
            return channel switch
            {
                RequestChannel.Search => searchCancellationVersion,
                RequestChannel.Suggest => suggestCancellationVersion,
                RequestChannel.Download => downloadCancellationVersion,
                _ => 0,
            };
        }

        private void ConfigureBilibiliRequest(
            UnityWebRequest request,
            string referer,
            string cookie,
            int timeoutSeconds = RequestTimeoutSeconds)
        {
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Accept", "application/json,video/mp4,*/*");
            request.SetRequestHeader("Referer", string.IsNullOrWhiteSpace(referer) ? BilibiliReferer : referer);
            request.SetRequestHeader("User-Agent", DesktopUserAgent);
            if (!string.IsNullOrWhiteSpace(cookie))
            {
                request.SetRequestHeader("Cookie", cookie);
                request.SetRequestHeader("Origin", SearchOrigin);
            }
        }

        private void SetActiveRequest(RequestChannel channel, UnityWebRequest request)
        {
            switch (channel)
            {
                case RequestChannel.Search:
                    activeSearchRequest = request;
                    break;
                case RequestChannel.Suggest:
                    activeSuggestRequest = request;
                    break;
                case RequestChannel.Download:
                    activeDownloadRequest = request;
                    break;
            }
        }

        private void ClearActiveRequest(RequestChannel channel, UnityWebRequest request)
        {
            switch (channel)
            {
                case RequestChannel.Search when ReferenceEquals(activeSearchRequest, request):
                    activeSearchRequest = null;
                    break;
                case RequestChannel.Suggest when ReferenceEquals(activeSuggestRequest, request):
                    activeSuggestRequest = null;
                    break;
                case RequestChannel.Download when ReferenceEquals(activeDownloadRequest, request):
                    activeDownloadRequest = null;
                    break;
            }
        }

        private static void AbortRequest(ref UnityWebRequest request)
        {
            if (request == null)
            {
                return;
            }

            request.Abort();
            request = null;
        }

        private static bool TryClassifyDirectInput(
            string value,
            out DirectBilibiliInput input,
            out bool isShortLink)
        {
            input = null;
            isShortLink = false;
            if (TryParseBilibiliInput(value, out var parsed))
            {
                if (string.Equals(value.Trim(), parsed.bvid, StringComparison.OrdinalIgnoreCase))
                {
                    input = parsed;
                    return true;
                }

                if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || IsAllowedBilibiliVideoUri(uri))
                {
                    input = parsed;
                    return true;
                }
                return false;
            }

            if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var shortUri) &&
                IsHttpUri(shortUri) &&
                IsSupportedShortHost(shortUri.Host))
            {
                isShortLink = true;
                return true;
            }
            return false;
        }

        private static bool TryParseBilibiliInput(string value, out DirectBilibiliInput input)
        {
            input = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var match = BvidPattern.Match(value.Trim());
            if (!match.Success)
            {
                return false;
            }

            input = new DirectBilibiliInput
            {
                bvid = $"BV{match.Groups[1].Value.Substring(2)}",
                pageNumber = ParsePageNumber(value),
            };
            return true;
        }

        private static int ParsePageNumber(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Query))
            {
                return 1;
            }

            var pairs = uri.Query.TrimStart('?').Split('&');
            for (var index = 0; index < pairs.Length; index += 1)
            {
                var pair = pairs[index].Split(new[] { '=' }, 2);
                if (pair.Length == 2 &&
                    string.Equals(pair[0], "p", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(Uri.UnescapeDataString(pair[1]), out var pageNumber))
                {
                    return Math.Max(1, pageNumber);
                }
            }
            return 1;
        }

        private static bool IsSupportedVideoHost(string host)
        {
            return string.Equals(host, "bilibili.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "www.bilibili.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "m.bilibili.com", StringComparison.OrdinalIgnoreCase) ||
                   IsSupportedShortHost(host);
        }

        private static bool IsAllowedBilibiliVideoUri(Uri uri)
        {
            return uri != null && IsHttpUri(uri) && IsSupportedVideoHost(uri.Host);
        }

        private static bool IsAllowedBilibiliRedirect(Uri uri)
        {
            return IsAllowedBilibiliVideoUri(uri) ||
                   uri != null && IsHttpUri(uri) && IsSupportedShortHost(uri.Host);
        }

        private static bool IsHttpUri(Uri uri)
        {
            return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsHttpUri(uri);
        }

        private static bool IsSupportedShortHost(string host)
        {
            return string.Equals(host, "b23.tv", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "www.b23.tv", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "bili2233.cn", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "www.bili2233.cn", StringComparison.OrdinalIgnoreCase);
        }

        private static BilibiliCatalogItem CatalogItemFromView(BilibiliViewData view, int pageNumber)
        {
            var bvid = string.IsNullOrWhiteSpace(view.bvid) ? string.Empty : view.bvid.Trim();
            var item = new BilibiliCatalogItem
            {
                bvid = bvid,
                title = CleanText(view.title),
                author = CleanText(view.owner?.name),
                coverUrl = NormalizeImageUrl(view.pic),
                durationSeconds = Math.Max(0, view.duration),
                viewCount = Math.Max(0L, view.stat?.view ?? 0L),
                pageUrl = string.IsNullOrEmpty(bvid)
                    ? string.Empty
                    : $"https://www.bilibili.com/video/{bvid}/?p={Math.Max(1, pageNumber)}",
            };
            item.Normalize();
            return item;
        }

        private static BilibiliCatalogItem CatalogItemFromSearch(BilibiliSearchItem raw)
        {
            if (raw == null || !TryParseBilibiliInput(raw.bvid, out var input))
            {
                return null;
            }

            var item = new BilibiliCatalogItem
            {
                bvid = input.bvid,
                title = CleanText(raw.title),
                author = CleanText(string.IsNullOrWhiteSpace(raw.author) ? raw.up_name : raw.author),
                coverUrl = NormalizeImageUrl(raw.pic),
                durationSeconds = ParseDuration(raw.duration),
                viewCount = Math.Max(0L, raw.play),
                pageUrl = $"https://www.bilibili.com/video/{input.bvid}/",
            };
            item.Normalize();
            return item;
        }

        private static BilibiliSuggestResponse EmptySuggestions()
        {
            return new BilibiliSuggestResponse
            {
                code = 0,
                result = new BilibiliSuggestResult { tag = Array.Empty<BilibiliSuggestItem>() },
            };
        }

        private static int ReadApiCode(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return int.MinValue;
            }

            try
            {
                return JsonUtility.FromJson<BilibiliResponseBase>(json)?.code ?? int.MinValue;
            }
            catch (Exception)
            {
                return int.MinValue;
            }
        }

        private static string DescribeApiFailure(int code, string message, string fallback)
        {
            if (code == -412 || code == -509)
            {
                return "B 站暂时限制了请求，请稍后再试。";
            }
            return string.IsNullOrWhiteSpace(message) ? fallback : CleanText(message);
        }

        private static int HeightForQuality(int quality)
        {
            return quality switch
            {
                16 => 360,
                32 => 480,
                64 => 720,
                74 => 720,
                80 => 1080,
                _ => quality > 0 ? quality : 360,
            };
        }

        private static string FindQualityLabel(BilibiliPlayData data, int quality, int height)
        {
            var qualities = data.accept_quality ?? Array.Empty<int>();
            var descriptions = data.accept_description ?? Array.Empty<string>();
            for (var index = 0; index < qualities.Length && index < descriptions.Length; index += 1)
            {
                if (qualities[index] == quality && !string.IsNullOrWhiteSpace(descriptions[index]))
                {
                    return CleanText(descriptions[index]);
                }
            }
            return height > 0 ? $"{height}P" : $"清晰度 {quality}";
        }

        private static bool LooksLikeMp4(string format, string url)
        {
            if (!string.IsNullOrWhiteSpace(format) && format.IndexOf("mp4", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                   uri.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeImageUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            return trimmed.StartsWith("//", StringComparison.Ordinal) ? $"https:{trimmed}" : trimmed;
        }

        private static string CleanText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return HtmlTagPattern.Replace(value, string.Empty)
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&nbsp;", " ")
                .Trim();
        }

        private static int ParseDuration(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var parts = value.Trim().Split(':');
            if (parts.Length < 1 || parts.Length > 3)
            {
                return 0;
            }

            var total = 0;
            for (var index = 0; index < parts.Length; index += 1)
            {
                if (!int.TryParse(parts[index], out var part) || part < 0)
                {
                    return 0;
                }
                total = total * 60 + part;
            }
            return Math.Max(0, total);
        }

        private static string SanitizeFileSegment(string value)
        {
            return Regex.Replace(value ?? string.Empty, @"[^0-9A-Za-z_-]", "_");
        }

        private static string ToFileUrl(string path)
        {
            return new Uri(Path.GetFullPath(path)).AbsoluteUri;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // A later request will report a concrete file-system error if needed.
            }
        }

        private static bool TryGetFileLength(string path, out long length, out string error)
        {
            length = 0L;
            error = null;
            try
            {
                if (File.Exists(path))
                {
                    length = new FileInfo(path).Length;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = $"无法读取头显视频缓存：{exception.Message}";
                return false;
            }
        }

        private static bool IsRequestSuccessful(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success &&
                   request.responseCode >= 200L &&
                   request.responseCode < 300L;
        }

        private static bool IsCancelledRequest(UnityWebRequest request)
        {
            var error = request.error ?? string.Empty;
            return error.IndexOf("abort", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private enum RequestChannel
        {
            Search,
            Suggest,
            Download,
        }

        private sealed class DirectBilibiliInput
        {
            public string bvid;
            public int pageNumber = 1;
        }

        [Serializable]
        private class BilibiliResponseBase
        {
            public int code;
            public string message;
        }

        [Serializable]
        private sealed class BilibiliFingerprintEnvelope : BilibiliResponseBase
        {
            public BilibiliFingerprintData data;
        }

        [Serializable]
        private sealed class BilibiliFingerprintData
        {
            public string b_3;
            public string b_4;
        }

        [Serializable]
        private sealed class BilibiliSearchEnvelope : BilibiliResponseBase
        {
            public BilibiliSearchData data;
        }

        [Serializable]
        private sealed class BilibiliSearchData
        {
            public int numResults;
            public int num_results;
            public BilibiliSearchItem[] result = Array.Empty<BilibiliSearchItem>();
        }

        [Serializable]
        private sealed class BilibiliSearchItem
        {
            public string bvid;
            public string title;
            public string author;
            public string up_name;
            public string pic;
            public string duration;
            public long play;
        }

        [Serializable]
        private sealed class BilibiliViewEnvelope : BilibiliResponseBase
        {
            public BilibiliViewData data;
        }

        [Serializable]
        private sealed class BilibiliViewData
        {
            public string bvid;
            public string title;
            public string pic;
            public int duration;
            public BilibiliOwnerData owner;
            public BilibiliStatData stat;
            public BilibiliPageData[] pages = Array.Empty<BilibiliPageData>();
        }

        [Serializable]
        private sealed class BilibiliOwnerData
        {
            public string name;
        }

        [Serializable]
        private sealed class BilibiliStatData
        {
            public long view;
        }

        [Serializable]
        private sealed class BilibiliPageData
        {
            public long cid;
            public string part;
            public int duration;
        }

        [Serializable]
        private sealed class BilibiliPlayEnvelope : BilibiliResponseBase
        {
            public BilibiliPlayData data;
        }

        [Serializable]
        private sealed class BilibiliPlayData
        {
            public int quality;
            public string format;
            public int[] accept_quality = Array.Empty<int>();
            public string[] accept_description = Array.Empty<string>();
            public BilibiliDurlData[] durl = Array.Empty<BilibiliDurlData>();
        }

        [Serializable]
        private sealed class BilibiliDurlData
        {
            public string url;
            public string[] backup_url = Array.Empty<string>();
            public long size;
        }
    }

    public sealed class DirectBilibiliMedia
    {
        public string bvid;
        public string title;
        public string author;
        public string coverUrl;
        public int pageNumber;
        public string partTitle;
        public int durationSeconds;
        public int actualHeight;
        public string qualityLabel;
        public long estimatedBytes;
        public long bytesWritten;
        public string referer;
        public string filePath;
        public string fileUrl;
    }
}
