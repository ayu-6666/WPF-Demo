using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BitTomrrorApp.Services;

public sealed class HttpResourceInfo
{
    public string OriginalUrl { get; set; } = "";
    public string FinalUrl { get; set; } = "";
    public string FileName { get; set; } = "";
    public long TotalBytes { get; set; }
    public bool SupportsRanges { get; set; }
    public string ETag { get; set; } = "";
    public string LastModified { get; set; } = "";
}

public sealed class HttpDownloadJob
{
    public HttpResourceInfo Info { get; set; }
    public string Directory { get; set; }
    public string FinalPath { get; set; }
    public int Connections { get; set; } = 1;
    public long RateLimitBytesPerSec { get; set; }
    public Action<long, long, string> Progress { get; set; }
}

internal sealed class HttpDownloadState
{
    public string Url { get; set; } = "";
    public string FinalUrl { get; set; } = "";
    public long Total { get; set; }
    public bool Ranges { get; set; }
    public string ETag { get; set; } = "";
    public string LastModified { get; set; } = "";
}

public static class HttpDownloadEngine
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public static async Task<HttpResourceInfo> ResolveAsync(string url, CancellationToken token)
    {
        var info = new HttpResourceInfo { OriginalUrl = url, FinalUrl = url };
        using var head = new HttpRequestMessage(HttpMethod.Head, url);
        HttpResponseMessage response = null;
        try
        {
            response = await Client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, token);
        }
        catch (HttpRequestException)
        {
            response?.Dispose();
            response = null;
        }

        if (response != null && !response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            if (status is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
                response = null;
            else
                throw new DownloadErrorException($"HTTP_{(int)status}", DownloadErrors.HttpMessage(status));
        }

        if (response != null)
        {
            FillInfo(info, response);
            response.Dispose();
        }

        if (info.TotalBytes <= 0 || !info.SupportsRanges)
        {
            using var range = new HttpRequestMessage(HttpMethod.Get, url);
            range.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            using var ranged = await Client.SendAsync(range, HttpCompletionOption.ResponseHeadersRead, token);
            if (ranged.StatusCode == HttpStatusCode.PartialContent)
            {
                info.SupportsRanges = true;
                var rangeHeader = ranged.Content.Headers.ContentRange;
                if (rangeHeader?.Length > 0) info.TotalBytes = rangeHeader.Length.Value;
            }
            else if (!ranged.IsSuccessStatusCode)
            {
                throw new DownloadErrorException($"HTTP_{(int)ranged.StatusCode}", DownloadErrors.HttpMessage(ranged.StatusCode));
            }
            else if (info.TotalBytes <= 0 && ranged.Content.Headers.ContentLength > 0)
            {
                info.TotalBytes = ranged.Content.Headers.ContentLength.Value;
            }

            FillInfo(info, ranged, keepFile: false);
        }

        if (string.IsNullOrWhiteSpace(info.FileName))
            info.FileName = GuessFileName(info.FinalUrl);
        return info;
    }

    private static void FillInfo(HttpResourceInfo info, HttpResponseMessage response, bool keepFile = true)
    {
        if (response.RequestMessage?.RequestUri != null)
            info.FinalUrl = response.RequestMessage.RequestUri.ToString();
        if (response.Content.Headers.ContentLength > 0)
            info.TotalBytes = response.Content.Headers.ContentLength.Value;

        var acceptRanges = response.Headers.AcceptRanges;
        info.SupportsRanges = acceptRanges.Any(v => v.Equals("bytes", StringComparison.OrdinalIgnoreCase));

        info.ETag = response.Headers.ETag?.Tag ?? "";
        info.LastModified = response.Content.Headers.LastModified?.ToString("R") ?? "";

        if (keepFile && !string.IsNullOrWhiteSpace(GetContentDispositionFileName(response)))
            info.FileName = GetContentDispositionFileName(response);
    }

    private static string GetContentDispositionFileName(HttpResponseMessage response)
    {
        var cd = response.Content.Headers.ContentDisposition;
        if (cd == null) return "";
        if (!string.IsNullOrWhiteSpace(cd.FileNameStar)) return cd.FileNameStar.Trim('"');
        if (!string.IsNullOrWhiteSpace(cd.FileName)) return cd.FileName.Trim('"');
        return "";
    }

    public static string GuessFileName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var name = Uri.UnescapeDataString(uri.AbsolutePath.Split('/').LastOrDefault() ?? "");
            if (string.IsNullOrWhiteSpace(name) || name == "/") name = "download.bin";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
        catch
        {
            return "download.bin";
        }
    }

    public static async Task DownloadAsync(HttpDownloadJob job, CancellationToken token)
    {
        var info = job.Info;
        if (info == null) throw new DownloadErrorException("HTTP_NO_INFO", "无法获取服务器资源信息");
        System.IO.Directory.CreateDirectory(job.Directory);

        var statePath = System.IO.Path.Combine(job.Directory, "http-state.json");
        var state = LoadState(statePath);
        var finalUrl = info.FinalUrl;
        var total = info.TotalBytes;
        if (state != null && state.Total != total && state.Total > 0)
            ResetParts(job.Directory);

        SaveState(statePath, new HttpDownloadState
        {
            Url = info.OriginalUrl,
            FinalUrl = finalUrl,
            Total = total,
            Ranges = info.SupportsRanges,
            ETag = info.ETag,
            LastModified = info.LastModified
        });

        if (File.Exists(job.FinalPath) && total > 0 && new FileInfo(job.FinalPath).Length == total)
        {
            job.Progress?.Invoke(total, total, "目标文件已存在且大小一致");
            return;
        }

        var multipart = info.SupportsRanges && total > 0 && job.Connections > 1;
        if (!multipart)
        {
            await DownloadSingleAsync(job, total, token);
            return;
        }

        var segmentCount = Math.Max(1, Math.Min(job.Connections, 16));
        var segmentLength = (total + segmentCount - 1) / segmentCount;
        var parts = new List<string>();
        var gates = new RateGate(job.RateLimitBytesPerSec);
        long initialBytes = 0;
        var partLengths = new long[segmentCount];
        for (var i = 0; i < segmentCount; i++)
        {
            var part = System.IO.Path.Combine(job.Directory, $"data.part.{i}");
            parts.Add(part);
            var len = File.Exists(part) ? new FileInfo(part).Length : 0;
            partLengths[i] = Math.Min(len, Math.Min(segmentLength, total - i * segmentLength));
            initialBytes += partLengths[i];
        }

        var downloaded = initialBytes;
        var sw = Stopwatch.StartNew();
        var lastReport = DateTime.UtcNow;
        job.Progress?.Invoke(downloaded, total, $"并发分片 {segmentCount}");

        var tasks = new List<Task>();
        for (var i = 0; i < segmentCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var start = index * segmentLength;
                var end = Math.Min(total - 1, start + segmentLength - 1);
                var part = parts[index];
                long have;
                lock (partLengths) have = partLengths[index];
                if (have >= end - start + 1) return;

                var request = new HttpRequestMessage(HttpMethod.Get, finalUrl);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start + have, end);
                using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // 服务器忽略 Range：从头覆盖该分片
                    have = 0;
                    lock (partLengths) partLengths[index] = 0;
                    downloaded = Math.Max(0, downloaded - (File.Exists(part) ? new FileInfo(part).Length : 0));
                    if (File.Exists(part)) File.Delete(part);
                }
                else if (response.StatusCode != HttpStatusCode.PartialContent)
                {
                    throw new DownloadErrorException($"HTTP_{(int)response.StatusCode}", DownloadErrors.HttpMessage(response.StatusCode));
                }

                var remaining = end - start + 1 - have;
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                await using var file = new FileStream(part, FileMode.Append, FileAccess.Write, FileShare.Read, 81920);
                var buffer = new byte[81920];
                long written = 0;
                while (written < remaining && !token.IsCancellationRequested)
                {
                    var read = await stream.ReadAsync(buffer, token);
                    if (read <= 0) break;
                    var need = Math.Min(read, remaining - written);
                    await gates.WaitAsync(need, token);
                    await file.WriteAsync(buffer, 0, (int)need, token);
                    written += need;
                    downloaded += need;
                    if ((DateTime.UtcNow - lastReport).TotalMilliseconds >= 300)
                    {
                        lastReport = DateTime.UtcNow;
                        job.Progress?.Invoke(downloaded, total, $"并发分片 {segmentCount}");
                    }
                }

                if (written < remaining)
                    throw new DownloadErrorException("HTTP_INCOMPLETE", $"分片 {index} 提前结束：需要 {remaining} 字节，实际 {written} 字节");
            }, token));
        }

        await Task.WhenAll(tasks);
        if (token.IsCancellationRequested) throw new OperationCanceledException(token);

        var mergedPart = System.IO.Path.Combine(job.Directory, "data.part");
        await using (var output = new FileStream(mergedPart, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            foreach (var part in parts)
            {
                if (!File.Exists(part)) continue;
                await using var input = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read);
                await input.CopyToAsync(output, token);
            }
        }

        var mergedLength = new FileInfo(mergedPart).Length;
        if (total > 0 && mergedLength != total)
            throw new DownloadErrorException("HTTP_SIZE_MISMATCH", $"合并后大小不一致：期望 {total} 字节，实际 {mergedLength} 字节");

        await FinalizeAsync(job, mergedPart, total);
        foreach (var part in parts) TryDelete(part);
        TryDelete(statePath);
    }

    private static async Task DownloadSingleAsync(HttpDownloadJob job, long total, CancellationToken token)
    {
        var part = System.IO.Path.Combine(job.Directory, "data.part");
        var have = File.Exists(part) ? new FileInfo(part).Length : 0;
        if (total > 0 && have > total)
        {
            TryDelete(part);
            have = 0;
        }

        if (total > 0 && have == total)
        {
            job.Progress?.Invoke(total, total, "分片已完整，准备合并");
            await FinalizeAsync(job, part, total);
            return;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, job.Info.FinalUrl);
        if (have > 0)
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(have, null);
            if (!string.IsNullOrEmpty(job.Info.ETag))
                request.Headers.IfRange = new System.Net.Http.Headers.RangeConditionHeaderValue(System.Net.Http.Headers.EntityTagHeaderValue.Parse(job.Info.ETag));
            else if (!string.IsNullOrEmpty(job.Info.LastModified))
                request.Headers.IfRange = new System.Net.Http.Headers.RangeConditionHeaderValue(DateTimeOffset.Parse(job.Info.LastModified));
        }

        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            TryDelete(part);
            throw new DownloadErrorException("HTTP_RESUME_FAILED", "服务器拒绝断点续传请求，请重新开始任务");
        }

        if (!response.IsSuccessStatusCode)
            throw new DownloadErrorException($"HTTP_{(int)response.StatusCode}", DownloadErrors.HttpMessage(response.StatusCode));

        if (response.StatusCode == HttpStatusCode.OK)
        {
            have = 0;
            if (File.Exists(part)) File.Delete(part);
        }

        if (total <= 0 && response.Content.Headers.ContentLength > 0)
            total = response.Content.Headers.ContentLength.Value;

        var gate = new RateGate(job.RateLimitBytesPerSec);
        var downloaded = have;
        var lastReport = DateTime.UtcNow;
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        await using var file = new FileStream(part, FileMode.Append, FileAccess.Write, FileShare.Read, 81920);
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, token);
            if (read <= 0) break;
            await gate.WaitAsync(read, token);
            await file.WriteAsync(buffer, 0, read, token);
            downloaded += read;
            if ((DateTime.UtcNow - lastReport).TotalMilliseconds >= 300)
            {
                lastReport = DateTime.UtcNow;
                job.Progress?.Invoke(downloaded, total, total > 0 ? "断点续传中" : "长度未知，顺序下载");
            }
        }

        if (total > 0 && downloaded != total)
            throw new DownloadErrorException("HTTP_SIZE_MISMATCH", $"下载大小不一致：期望 {total} 字节，实际 {downloaded} 字节");

        // 必须先关闭写入句柄，否则 Windows 下无法移动文件
        await file.FlushAsync(token);
        await file.DisposeAsync();

        job.Progress?.Invoke(downloaded, total, "下载完成，正在校验");
        await FinalizeAsync(job, part, total);
        TryDelete(System.IO.Path.Combine(job.Directory, "http-state.json"));
    }

    private static async Task FinalizeAsync(HttpDownloadJob job, string part, long total)
    {
        if (File.Exists(job.FinalPath))
        {
            // 同名文件存在：保留原文件并生成不冲突的名称
            var dir = System.IO.Path.GetDirectoryName(job.FinalPath) ?? job.Directory;
            var name = System.IO.Path.GetFileNameWithoutExtension(job.FinalPath);
            var ext = System.IO.Path.GetExtension(job.FinalPath);
            var candidate = System.IO.Path.Combine(dir, $"{name}_{DateTime.Now:HHmmss}{ext}");
            File.Move(part, candidate);
            job.FinalPath = candidate;
        }
        else
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(job.FinalPath) ?? job.Directory);
            File.Move(part, job.FinalPath);
        }

        var length = new FileInfo(job.FinalPath).Length;
        var sha = await ComputeSha256Async(job.FinalPath);
        job.Progress?.Invoke(length, total > 0 ? total : length, $"SHA256 {sha}");
    }

    public static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static HttpDownloadState LoadState(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<HttpDownloadState>(File.ReadAllText(path));
        }
        catch { }
        return null;
    }

    private static void SaveState(string path, HttpDownloadState state)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(state), Encoding.UTF8);
        }
        catch { }
    }

    private static void ResetParts(string dir)
    {
        foreach (var file in System.IO.Directory.GetFiles(dir, "data.part*")) TryDelete(file);
    }

    public static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private sealed class RateGate
    {
        private readonly long _limit;
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private readonly object _lock = new();
        private long _bytes;

        public RateGate(long limitBytesPerSecond) => _limit = limitBytesPerSecond;

        public async Task WaitAsync(long bytes, CancellationToken token)
        {
            if (_limit <= 0) return;
            long targetMs;
            lock (_lock)
            {
                _bytes += bytes;
                targetMs = _bytes * 1000 / _limit;
            }
            var elapsed = (long)_watch.ElapsedMilliseconds;
            var delay = targetMs - elapsed;
            if (delay > 0)
                await Task.Delay((int)Math.Min(delay, 1000), token);
        }
    }
}

internal static class HttpMessage
{
    public static string Safe(HttpStatusCode status) => DownloadErrors.HttpMessage(status);
}
