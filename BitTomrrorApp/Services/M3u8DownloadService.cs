using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using BitTomrrorApp.Models;

namespace BitTomrrorApp.Services;

public sealed class M3u8DownloadService : IDownloadService
{
    private static readonly HttpClient Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private readonly ConcurrentDictionary<string, M3u8Job> _jobs = new();

    public DownloadType Type => DownloadType.M3u8;

    public Task StartAsync(DownloadTask task) => LaunchAsync(task);
    public Task ResumeAsync(DownloadTask task) => LaunchAsync(task);

    public Task PauseAsync(DownloadTask task)
    {
        var job = _jobs.TryGetValue(task.Id, out var existing) ? existing : null;
        if (job != null) job.Paused = true;
        try { job?.Cts.Cancel(); } catch { }
        task.Apply(() =>
        {
            task.Status = DownloadStatus.Paused;
            task.DownloadRate = 0;
            task.Detail = "已暂停，已下载分片保留";
            task.RefreshDisplay();
        });
        return Task.CompletedTask;
    }

    public Task RemoveAsync(DownloadTask task)
    {
        var job = _jobs.TryRemove(task.Id, out var existing) ? existing : null;
        try { job?.Cts.Cancel(); } catch { }
        var dir = Path.Combine(AppPaths.TempDirectory, task.Id);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        foreach (var job in _jobs.Values) { try { job.Cts.Cancel(); } catch { } }
        return Task.CompletedTask;
    }

    public void Tick()
    {
        foreach (var job in _jobs.Values)
        {
            var task = job.Task;
            if (task.Status != DownloadStatus.Downloading) continue;
            task.Apply(() =>
            {
                var now = Environment.TickCount64;
                job.UpdateSpeed(now);
                task.DownloadRate = job.LastSpeed;
                task.DownloadedBytes = job.SnapshotBytes();
                task.RefreshDisplay();
            });
        }
    }

    private async Task LaunchAsync(DownloadTask task)
    {
        var old = _jobs.TryRemove(task.Id, out var existing) ? existing : null;
        try { old?.Cts.Cancel(); } catch { }

        var job = new M3u8Job(task);
        _jobs[task.Id] = job;
        task.Cancellation = job.Cts;

        task.Apply(() =>
        {
            task.Status = DownloadStatus.Preparing;
            task.ErrorCode = "";
            task.ErrorMessage = "";
            task.Detail = "正在解析 M3U8 播放列表...";
            task.DownloadRate = 0;
            task.RefreshDisplay();
        });

        try
        {
            await RunAsync(job);
        }
        catch (OperationCanceledException)
        {
            if (!job.Paused)
            {
                task.Apply(() =>
                {
                    task.Status = DownloadStatus.Stopped;
                    task.Detail = "已停止";
                    task.DownloadRate = 0;
                    task.RefreshDisplay();
                });
            }
        }
        catch (DownloadErrorException ex)
        {
            SetError(task, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            SetError(task, "M3U8_FAILED", ex.Message);
        }
        finally
        {
            _jobs.TryRemove(task.Id, out _);
            task.Cancellation = null;
            task.Apply(() => task.RefreshDisplay());
        }
    }

    private async Task RunAsync(M3u8Job job)
    {
        var task = job.Task;
        var token = job.Cts.Token;
        var playlistUrl = task.Source;

        var content = await GetStringAsync(playlistUrl, token, "M3U8_PLAYLIST_FAILED");
        var playlistUri = new Uri(playlistUrl);
        var parsed = HlsParser.Parse(content, playlistUri);

        if (parsed.Variants.Count > 0)
        {
            var variant = SelectVariant(parsed.Variants, task.M3u8QualityKbps);
            job.DetailPrefix = $"码率 {variant.Bandwidth / 1000} kbps · {variant.Resolution}";
            task.Apply(() =>
            {
                task.Detail = "已选择 " + job.DetailPrefix;
                task.RefreshDisplay();
            });
            content = await GetStringAsync(variant.Uri.ToString(), token, "M3U8_VARIANT_FAILED");
            playlistUri = variant.Uri;
            parsed = HlsParser.Parse(content, playlistUri);
        }

        if (parsed.Segments.Count == 0)
            throw new DownloadErrorException("M3U8_EMPTY", "播放列表中没有可下载的分片");
        if (!parsed.EndList)
            throw new DownloadErrorException("M3U8_LIVE_UNSUPPORTED", "这是直播/滚动播放列表（缺少 EXT-X-ENDLIST），当前只支持完整点播列表");

        job.Segments = parsed.Segments;
        job.InitUri = parsed.MapUri;
        job.TotalSegments = parsed.Segments.Count;

        var name = HlsParser.GuessName(playlistUri);
        var dir = AppPaths.CreateTaskTempDirectory(task);
        job.WorkDir = Path.Combine(dir, "segments");
        Directory.CreateDirectory(job.WorkDir);

        var snapshot = Path.Combine(job.WorkDir, "playlist.m3u8");
        File.WriteAllText(snapshot, content, Encoding.UTF8);

        var initPath = Path.Combine(job.WorkDir, "init.mp4");
        if (job.InitUri != null && !File.Exists(initPath))
        {
            await DownloadToFileAsync(job.InitUri, initPath, token, "初始化段 (EXT-X-MAP)");
        }

        // 恢复：统计已存在的完整分片
        var existingBytes = 0L;
        var existingCount = 0;
        for (var i = 0; i < job.Segments.Count; i++)
        {
            var p = SegmentPath(job, i);
            if (File.Exists(p) && new FileInfo(p).Length > 0)
            {
                existingBytes += new FileInfo(p).Length;
                existingCount++;
            }
        }
        job.DownloadedBytes = existingBytes;
        job.ProcessedSegments = existingCount;
        job.LastBytes = existingBytes;
        job.LastTick = Environment.TickCount64;

        task.Apply(() =>
        {
            task.Name = name + ".mp4";
            task.TotalBytes = 0;
            task.DownloadedBytes = existingBytes;
            task.Progress = existingCount * 100.0 / job.TotalSegments;
            task.Status = DownloadStatus.Downloading;
            task.Detail = existingCount > 0
                ? $"续传：已完成 {existingCount}/{job.TotalSegments} 分片"
                : $"共 {job.TotalSegments} 个分片，并发下载中";
            task.RefreshDisplay();
        });

        var gate = new SemaphoreSlim(8);
        var tasks = new List<Task>();
        for (var i = 0; i < job.Segments.Count; i++)
        {
            var index = i;
            if (File.Exists(SegmentPath(job, index)) && new FileInfo(SegmentPath(job, index)).Length > 0)
                continue;

            await gate.WaitAsync(token);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await DownloadSegmentAsync(job, index, token);
                }
                finally
                {
                    gate.Release();
                }
            }, token));
        }

        await Task.WhenAll(tasks);
        token.ThrowIfCancellationRequested();

        task.Apply(() =>
        {
            task.Status = DownloadStatus.Preparing;
            task.Detail = "分片下载完成，正在合并为 MP4...";
            task.DownloadRate = 0;
            task.Progress = 100;
            task.RefreshDisplay();
        });

        var outputPath = Path.Combine(task.SavePath, name + ".mp4");
        Directory.CreateDirectory(task.SavePath);
        if (File.Exists(outputPath))
            outputPath = Path.Combine(task.SavePath, $"{name}_{DateTime.Now:HHmmss}.mp4");

        var ffmpegConfigured = task.FfmpegPath;
        var ffmpeg = new FfmpegLocator();
        await ffmpeg.CheckAsync(ffmpegConfigured);
        var mergedByFfmpeg = false;
        if (ffmpeg.Available)
        {
            mergedByFfmpeg = await TryMergeWithFfmpegAsync(ffmpeg.Path, job, outputPath, token);
        }

        if (!mergedByFfmpeg)
        {
            await ConcatBuiltInAsync(job, outputPath, token);
        }

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            throw new DownloadErrorException("M3U8_MERGE_FAILED", "分片合并失败，输出文件为空");

        var finalLength = new FileInfo(outputPath).Length;
        var sha = await HttpDownloadEngine.ComputeSha256Async(outputPath);
        task.Apply(() =>
        {
            task.Name = Path.GetFileName(outputPath);
            task.TotalBytes = finalLength;
            task.DownloadedBytes = finalLength;
            task.Progress = 100;
            task.Status = DownloadStatus.Completed;
            task.Detail = mergedByFfmpeg
                ? $"已用 FFmpeg 无损转封装 · {job.TotalSegments} 分片 · SHA256 {sha}"
                : $"FFmpeg 不可用/失败，已用内置拼接 · {job.TotalSegments} 分片 · SHA256 {sha}"
                  + (string.IsNullOrWhiteSpace(job.FfmpegError) ? "" : $" · FFmpeg 错误：{job.FfmpegError}");
            task.RefreshDisplay();
        });

        try { Directory.Delete(job.WorkDir, true); } catch { }
    }

    private static HlsVariant SelectVariant(List<HlsVariant> variants, int preferredKbps)
    {
        if (preferredKbps <= 0)
            return variants.OrderByDescending(v => v.Bandwidth).First();

        return variants
            .OrderBy(v => Math.Abs(v.Bandwidth / 1000 - preferredKbps))
            .First();
    }

    private async Task DownloadSegmentAsync(M3u8Job job, int index, CancellationToken token)
    {
        Exception lastError = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var tmp = SegmentPath(job, index) + ".part";
            long attemptBytes = 0;
            try
            {
                var segment = job.Segments[index];
                var bytes = await FetchBytesAsync(segment.Uri, token, $"M3U8_SEGMENT_FAILED",
                    delta => { attemptBytes += delta; job.AddBytes(delta); });

                if (segment.Key != null)
                {
                    try
                    {
                        bytes = await DecryptAsync(job.Segments, index, bytes, token);
                    }
                    catch (Exception ex)
                    {
                        throw new DownloadErrorException("M3U8_DECRYPT_FAILED",
                            $"第 {index + 1} 个分片 AES-128 解密失败：{ex.Message}", ex);
                    }
                }

                await File.WriteAllBytesAsync(tmp, bytes, token);
                File.Move(tmp, SegmentPath(job, index), true);
                job.AddBytes(bytes.Length - attemptBytes);
                job.ProcessedSegments++;
                job.Task.Apply(() =>
                {
                    job.Task.Progress = job.ProcessedSegments * 100.0 / job.TotalSegments;
                    job.Task.DownloadedBytes = job.SnapshotBytes();
                    job.Task.Detail = $"{job.DetailPrefix} · 分片 {job.ProcessedSegments}/{job.TotalSegments}";
                    job.Task.RefreshDisplay();
                });
                return;
            }
            catch (OperationCanceledException)
            {
                job.AddBytes(-attemptBytes);
                throw;
            }
            catch (DownloadErrorException ex)
            {
                job.AddBytes(-attemptBytes);
                lastError = ex;
                HttpDownloadEngine.TryDelete(tmp);
            }
            catch (Exception ex)
            {
                job.AddBytes(-attemptBytes);
                lastError = ex;
                HttpDownloadEngine.TryDelete(tmp);
            }

            if (attempt < 3)
                await Task.Delay(500 * attempt, token);
        }

        throw new DownloadErrorException("M3U8_SEGMENT_FAILED",
            $"第 {index + 1}/{job.TotalSegments} 个分片重试 3 次仍失败：{lastError?.Message}", lastError);
    }

    private static async Task<byte[]> DecryptAsync(List<HlsSegment> segments, int index, byte[] data, CancellationToken token)
    {
        var segment = segments[index];
        var key = await GetKeyAsync(segment.Key, token);
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = segment.Key.Iv ?? HlsParser.MakeSequenceIv(segment.Sequence);
        using var decryptor = aes.CreateDecryptor();
        using var input = new MemoryStream(data);
        using var crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read);
        using var output = new MemoryStream();
        await crypto.CopyToAsync(output, token);
        return output.ToArray();
    }

    private static readonly ConcurrentDictionary<string, byte[]> KeyCache = new();

    private static async Task<byte[]> GetKeyAsync(HlsKey key, CancellationToken token)
    {
        if (KeyCache.TryGetValue(key.Uri.ToString(), out var cached)) return cached;
        try
        {
            var bytes = await FetchBytesAsync(key.Uri, token, "M3U8_KEY_FAILED");
            if (bytes.Length != 16)
                throw new DownloadErrorException("M3U8_KEY_INVALID", $"AES 密钥长度应为 16 字节，实际 {bytes.Length} 字节");
            KeyCache[key.Uri.ToString()] = bytes;
            return bytes;
        }
        catch (DownloadErrorException) { throw; }
        catch (Exception ex)
        {
            throw new DownloadErrorException("M3U8_KEY_FAILED", $"密钥下载失败：{key.Uri}（{ex.Message}）", ex);
        }
    }

    private static async Task<byte[]> FetchBytesAsync(Uri uri, CancellationToken token, string errorCode, Action<long> onBytes = null)
    {
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode)
            throw new DownloadErrorException($"HTTP_{(int)response.StatusCode}", $"{uri}：{DownloadErrors.HttpMessage(response.StatusCode)}");
        var length = response.Content.Headers.ContentLength ?? 0;
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var ms = new MemoryStream(length > 0 ? (int)Math.Min(length, int.MaxValue) : 0);
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, token);
            if (read <= 0) break;
            ms.Write(buffer, 0, read);
            onBytes?.Invoke(read);
        }
        if (ms.Length == 0)
            throw new DownloadErrorException(errorCode, $"{uri} 返回空内容");
        return ms.ToArray();
    }

    private static async Task<string> GetStringAsync(string url, CancellationToken token, string errorCode)
    {
        try
        {
            using var response = await Client.GetAsync(url, token);
            if (!response.IsSuccessStatusCode)
                throw new DownloadErrorException($"HTTP_{(int)response.StatusCode}", $"{url}：{DownloadErrors.HttpMessage(response.StatusCode)}");
            return await response.Content.ReadAsStringAsync(token);
        }
        catch (DownloadErrorException) { throw; }
        catch (Exception ex)
        {
            var mapped = DownloadErrors.FromHttp(ex, "M3U8");
            throw new DownloadErrorException(errorCode, mapped.Message, ex);
        }
    }

    private static async Task DownloadToFileAsync(Uri uri, string path, CancellationToken token, string what)
    {
        try
        {
            var bytes = await FetchBytesAsync(uri, token, "M3U8_MAP_FAILED");
            await File.WriteAllBytesAsync(path, bytes, token);
        }
        catch (DownloadErrorException ex)
        {
            throw new DownloadErrorException("M3U8_MAP_FAILED", $"{what}下载失败：{ex.Message}", ex);
        }
    }

    private static async Task<bool> TryMergeWithFfmpegAsync(string ffmpegPath, M3u8Job job, string outputPath, CancellationToken token)
    {
        try
        {
            // 用本地 HLS 播放列表让 ffmpeg 自行识别 TS / fMP4 容器并转封装，
            // 直接 concat 分片对 fMP4（moof+mdat）会产出损坏文件。
            var playlistPath = Path.Combine(job.WorkDir, "local.m3u8");
            var init = Path.Combine(job.WorkDir, "init.mp4");
            var lines = new List<string>
            {
                "#EXTM3U",
                "#EXT-X-VERSION:7",
                "#EXT-X-PLAYLIST-TYPE:VOD",
                $"#EXT-X-TARGETDURATION:{(int)Math.Ceiling(job.Segments.Count > 0 ? job.Segments.Max(s => s.Duration) : 0)}"
            };
            if (File.Exists(init)) lines.Add("#EXT-X-MAP:URI=\"init.mp4\"");
            for (var i = 0; i < job.TotalSegments; i++)
            {
                var duration = job.Segments[i].Duration;
                lines.Add("#EXTINF:" + (duration > 0 ? duration.ToString("F6") : "0") + ",");
                lines.Add(Path.GetFileName(SegmentPath(job, i)));
            }
            lines.Add("#EXT-X-ENDLIST");
            // 必须无 BOM
            await File.WriteAllLinesAsync(playlistPath, lines, new UTF8Encoding(false), token);

            var args = $"-hide_banner -y -i \"{playlistPath}\" -c copy -movflags +faststart \"{outputPath}\"";
            var psi = new ProcessStartInfo(ffmpegPath, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            var stdout = process.StandardOutput.ReadToEndAsync(token);
            var stderr = process.StandardError.ReadToEndAsync(token);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                throw;
            }
            await stdout;
            var error = await stderr;
            if (process.ExitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                return true;
            job.FfmpegError = error.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? "FFmpeg 未输出错误信息";
            return false;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            job.FfmpegError = ex.Message;
            return false;
        }
    }

    private static async Task ConcatBuiltInAsync(M3u8Job job, string outputPath, CancellationToken token)
    {
        await using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var init = Path.Combine(job.WorkDir, "init.mp4");
        if (File.Exists(init))
        {
            await using var input = new FileStream(init, FileMode.Open, FileAccess.Read, FileShare.Read);
            await input.CopyToAsync(output, token);
        }
        for (var i = 0; i < job.TotalSegments; i++)
        {
            var path = SegmentPath(job, i);
            if (!File.Exists(path))
                throw new DownloadErrorException("M3U8_MERGE_FAILED", $"合并时缺少第 {i + 1} 个分片");
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            await input.CopyToAsync(output, token);
        }
    }

    private static readonly HashSet<string> AllowedSegmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ts", ".m4s", ".m4f", ".mp4", ".aac", ".m4a", ".mp3", ".vtt"
    };

    private static string SegmentPath(M3u8Job job, int index)
    {
        // ffmpeg 的 HLS 解复用器只接受白名单扩展名，因此不能统一存成 .bin
        var ext = Path.GetExtension(job.Segments[index].Uri.AbsolutePath);
        if (!AllowedSegmentExtensions.Contains(ext)) ext = ".ts";
        return Path.Combine(job.WorkDir, $"seg_{index:D5}{ext}");
    }

    private static void SetError(DownloadTask task, string code, string message)
    {
        task.Apply(() =>
        {
            task.Status = DownloadStatus.Error;
            task.ErrorCode = code;
            task.ErrorMessage = message;
            task.DownloadRate = 0;
            task.Detail = message;
            task.RefreshDisplay();
        });
    }

    private sealed class M3u8Job
    {
        public DownloadTask Task { get; }
        public CancellationTokenSource Cts { get; } = new();
        public bool Paused { get; set; }
        public List<HlsSegment> Segments { get; set; } = new();
        public Uri InitUri { get; set; }
        public int TotalSegments { get; set; }
        public int ProcessedSegments { get; set; }
        public long DownloadedBytes { get; set; }
        public long LastBytes { get; set; }
        public long LastTick { get; set; }
        public long LastSpeed { get; private set; }
        public string WorkDir { get; set; } = "";
        public string DetailPrefix { get; set; } = "";
        public string FfmpegError { get; set; } = "";

        private readonly object _bytesLock = new();

        public M3u8Job(DownloadTask task) => Task = task;

        public void AddBytes(long delta)
        {
            lock (_bytesLock)
                DownloadedBytes = Math.Max(0, DownloadedBytes + delta);
        }

        public long SnapshotBytes()
        {
            lock (_bytesLock)
                return DownloadedBytes;
        }

        public void UpdateSpeed(long now)
        {
            var current = SnapshotBytes();
            if (LastTick != 0 && now > LastTick)
                LastSpeed = Math.Max(0, (current - LastBytes) * 1000 / (now - LastTick));
            LastBytes = current;
            LastTick = now;
        }
    }
}

public sealed class HlsVariant
{
    public Uri Uri { get; set; }
    public long Bandwidth { get; set; }
    public string Resolution { get; set; } = "";
}

public sealed class HlsKey
{
    public string Method { get; set; } = "NONE";
    public Uri Uri { get; set; }
    public byte[] Iv { get; set; }
}

public sealed class HlsSegment
{
    public Uri Uri { get; set; }
    public double Duration { get; set; }
    public long Sequence { get; set; }
    public HlsKey Key { get; set; }
}

public sealed class HlsPlaylist
{
    public List<HlsVariant> Variants { get; } = new();
    public List<HlsSegment> Segments { get; } = new();
    public bool EndList { get; set; }
    public Uri MapUri { get; set; }
}

public static class HlsParser
{
    public static HlsPlaylist Parse(string content, Uri baseUri)
    {
        var result = new HlsPlaylist();
        var lines = content.Replace("\r", "").Split('\n');
        HlsKey currentKey = null;
        long mediaSequence = 0;
        double pendingDuration = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].Trim();
            if (raw.Length == 0) continue;

            if (raw.StartsWith("#EXT-X-MEDIA-SEQUENCE:"))
            {
                long.TryParse(raw["#EXT-X-MEDIA-SEQUENCE:".Length..].Trim(), out mediaSequence);
            }
            else if (raw.StartsWith("#EXT-X-STREAM-INF:"))
            {
                var attrs = ParseAttributes(raw["#EXT-X-STREAM-INF:".Length..]);
                var next = NextNonEmpty(lines, i + 1);
                if (next == null) continue;
                long bandwidth = 0;
                long.TryParse(attrs.GetValueOrDefault("BANDWIDTH", "0"), out bandwidth);
                result.Variants.Add(new HlsVariant
                {
                    Uri = new Uri(baseUri, next),
                    Bandwidth = bandwidth,
                    Resolution = attrs.GetValueOrDefault("RESOLUTION", "")
                });
                i = SkipToLine(lines, i + 1, next);
            }
            else if (raw.StartsWith("#EXT-X-KEY:"))
            {
                var attrs = ParseAttributes(raw["#EXT-X-KEY:".Length..]);
                var method = attrs.GetValueOrDefault("METHOD", "NONE");
                if (method.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                {
                    currentKey = null;
                }
                else if (method.Equals("AES-128", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = attrs.GetValueOrDefault("URI", "");
                    currentKey = new HlsKey
                    {
                        Method = method,
                        Uri = new Uri(baseUri, uri.Trim('"')),
                        Iv = ParseIv(attrs.GetValueOrDefault("IV", ""))
                    };
                }
                else
                {
                    throw new DownloadErrorException("M3U8_UNSUPPORTED_ENCRYPTION", $"不支持的加密方式：{method}（仅支持 AES-128）");
                }
            }
            else if (raw.StartsWith("#EXT-X-MAP:"))
            {
                var attrs = ParseAttributes(raw["#EXT-X-MAP:".Length..]);
                var uri = attrs.GetValueOrDefault("URI", "").Trim('"');
                if (!string.IsNullOrEmpty(uri)) result.MapUri = new Uri(baseUri, uri);
            }
            else if (raw.StartsWith("#EXTINF:"))
            {
                var value = raw["#EXTINF:".Length..];
                var comma = value.IndexOf(',');
                double.TryParse(comma >= 0 ? value[..comma] : value, out pendingDuration);
            }
            else if (raw.StartsWith("#EXT-X-ENDLIST"))
            {
                result.EndList = true;
            }
            else if (!raw.StartsWith('#'))
            {
                var uri = new Uri(baseUri, raw);
                var index = result.Segments.Count;
                result.Segments.Add(new HlsSegment
                {
                    Uri = uri,
                    Duration = pendingDuration,
                    Sequence = mediaSequence + index,
                    Key = currentKey
                });
                pendingDuration = 0;
            }
        }

        return result;
    }

    private static string NextNonEmpty(string[] lines, int start)
    {
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length > 0 && !line.StartsWith('#')) return line;
        }
        return null;
    }

    private static int SkipToLine(string[] lines, int start, string target)
    {
        for (var i = start; i < lines.Length; i++)
            if (lines[i].Trim() == target) return i;
        return start;
    }

    private static Dictionary<string, string> ParseAttributes(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;
        foreach (var c in text)
        {
            if (c == '"') inQuote = !inQuote;
            if (c == ',' && !inQuote)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else current.Append(c);
        }
        if (current.Length > 0) parts.Add(current.ToString());

        foreach (var part in parts)
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;
            result[part[..idx].Trim()] = part[(idx + 1)..].Trim();
        }
        return result;
    }

    private static byte[] ParseIv(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var hex = value.Trim().Trim('"');
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex[2..];
        if (hex.Length != 32) return null;
        try
        {
            var bytes = new byte[16];
            for (var i = 0; i < 16; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    public static byte[] MakeSequenceIv(long sequence)
    {
        var iv = new byte[16];
        for (var i = 15; i >= 0 && sequence > 0; i--)
        {
            iv[i] = (byte)(sequence & 0xFF);
            sequence >>= 8;
        }
        return iv;
    }

    public static string GuessName(Uri uri)
    {
        var name = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(name) || name == "index") name = "hls_video";
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }
}
