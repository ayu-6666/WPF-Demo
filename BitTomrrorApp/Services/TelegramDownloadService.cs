using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using BitTomrrorApp.Models;

namespace BitTomrrorApp.Services;

public sealed class TelegramDownloadService : IDownloadService
{
    private static readonly HttpClient Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    // API 地址可用环境变量覆盖（默认官方地址），用于本地模拟 Bot API 验证或私有化部署
    private static string ApiBase =>
        Environment.GetEnvironmentVariable("TELEGRAM_API_BASE") is { Length: > 0 } custom
            ? custom.TrimEnd('/')
            : "https://api.telegram.org";

    private readonly ConcurrentDictionary<string, TelegramJob> _jobs = new();

    public DownloadType Type => DownloadType.Telegram;

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
            task.Detail = "已暂停（Bot API 断点续传取决于文件服务器 Range 支持）";
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
                task.RefreshDisplay();
            });
        }
    }

    private async Task LaunchAsync(DownloadTask task)
    {
        var old = _jobs.TryRemove(task.Id, out var existing) ? existing : null;
        try { old?.Cts.Cancel(); } catch { }

        var job = new TelegramJob(task);
        _jobs[task.Id] = job;
        task.Cancellation = job.Cts;

        task.Apply(() =>
        {
            task.Status = DownloadStatus.Preparing;
            task.ErrorCode = "";
            task.ErrorMessage = "";
            task.Detail = "正在调用 Telegram Bot API...";
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
            SetError(task, "TELEGRAM_FAILED", ex.Message);
        }
        finally
        {
            _jobs.TryRemove(task.Id, out _);
            task.Cancellation = null;
            task.Apply(() => task.RefreshDisplay());
        }
    }

    private async Task RunAsync(TelegramJob job)
    {
        var task = job.Task;
        var token = job.Cts.Token;
        if (string.IsNullOrWhiteSpace(task.TelegramToken))
            throw new DownloadErrorException("TG_TOKEN_MISSING", "缺少 Telegram Bot Token，请在高级添加中填写");

        var me = await CallAsync(task.TelegramToken, "getMe", null, token);
        if (!me.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
        {
            var desc = me.TryGetProperty("description", out var d) ? d.GetString() : "Token 无效";
            throw new DownloadErrorException("TG_TOKEN_INVALID", $"Bot Token 校验失败：{desc}");
        }

        string fileId;
        string hintName = "";
        if (!string.IsNullOrWhiteSpace(task.TelegramFileId))
        {
            fileId = task.TelegramFileId.Trim();
        }
        else
        {
            var resolved = await FindMessageFileAsync(task, token);
            fileId = resolved.FileId;
            hintName = resolved.FileName;
        }

        var getFile = await CallAsync(task.TelegramToken, "getFile", $"file_id={Uri.EscapeDataString(fileId)}", token);
        if (!getFile.TryGetProperty("ok", out var fileOk) || !fileOk.GetBoolean())
        {
            var desc = getFile.TryGetProperty("description", out var d) ? d.GetString() : "getFile 失败";
            throw new DownloadErrorException("TG_GETFILE_FAILED", $"无法获取文件信息：{desc}");
        }

        var result = getFile.GetProperty("result");
        var filePath = result.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? "" : "";
        var fileSize = result.TryGetProperty("file_size", out var fs) ? fs.GetInt64() : 0;
        if (string.IsNullOrWhiteSpace(filePath))
            throw new DownloadErrorException("TG_FILE_PATH_MISSING", "Bot API 未返回 file_path，文件可能已过期或超过大小限制");

        const long BotApiLimit = 20L * 1024 * 1024;
        if (fileSize > BotApiLimit)
            throw new DownloadErrorException("TG_FILE_TOO_LARGE",
                $"文件大小 {DownloadTask.FormatBytes(fileSize)} 超过 Bot API 约 20 MB 下载上限；如需大文件请使用本地 Bot API 服务或 MTProto（当前未集成）");

        var fileName = hintName;
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = fileId + ".bin";

        var fileUrl = $"{ApiBase}/file/bot{task.TelegramToken}/{filePath}";
        var info = await HttpDownloadEngine.ResolveAsync(fileUrl, token);
        info.FileName = fileName;

        var dir = AppPaths.CreateTaskTempDirectory(task);
        Directory.CreateDirectory(task.SavePath);
        var finalPath = Path.Combine(task.SavePath, fileName);
        job.TotalBytes = fileSize > 0 ? fileSize : info.TotalBytes;

        task.Apply(() =>
        {
            task.Name = fileName;
            task.TotalBytes = job.TotalBytes;
            task.Status = DownloadStatus.Downloading;
            task.Detail = $"Telegram Bot API 下载（单文件上限约 20 MB）";
            task.RefreshDisplay();
        });

        var downloadJob = new HttpDownloadJob
        {
            Info = info,
            Directory = dir,
            FinalPath = finalPath,
            Connections = 1,
            RateLimitBytesPerSec = task.DownloadLimitBytesPerSec,
            Progress = (downloaded, total, detail) => task.Apply(() =>
            {
                task.DownloadedBytes = downloaded;
                job.DownloadedBytes = downloaded;
                if (total > 0)
                {
                    task.TotalBytes = total;
                    job.TotalBytes = total;
                }
                if (task.TotalBytes > 0)
                    task.Progress = Math.Min(100, downloaded * 100.0 / task.TotalBytes);
                task.Detail = detail;
                task.RefreshDisplay();
            })
        };

        await HttpDownloadEngine.DownloadAsync(downloadJob, token);

        task.Apply(() =>
        {
            task.TotalBytes = job.TotalBytes > 0 ? job.TotalBytes : task.DownloadedBytes;
            task.DownloadedBytes = task.TotalBytes;
            task.Progress = 100;
            task.Status = DownloadStatus.Completed;
            task.DownloadRate = 0;
            task.Detail = $"Telegram 下载完成 · {fileName}";
            task.RefreshDisplay();
        });
    }

    private static async Task<MessageFile> FindMessageFileAsync(DownloadTask task, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(task.TelegramChatId) || string.IsNullOrWhiteSpace(task.TelegramMessageId))
            throw new DownloadErrorException("TG_INPUT_MISSING", "请填写 file_id，或同时填写 chat_id 与 message_id");

        var updates = await CallAsync(task.TelegramToken, "getUpdates", "limit=100", token);
        if (!updates.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            throw new DownloadErrorException("TG_UPDATES_FAILED", "getUpdates 调用失败，无法按 chat/message 定位文件");

        var array = updates.GetProperty("result");
        foreach (var update in array.EnumerateArray())
        {
            if (!update.TryGetProperty("message", out var message)) continue;
            if (!message.TryGetProperty("message_id", out var mid)) continue;
            if (mid.ToString() != task.TelegramMessageId.Trim()) continue;
            if (!message.TryGetProperty("chat", out var chat)) continue;
            if (chat.TryGetProperty("id", out var cid) && cid.ToString() != task.TelegramChatId.Trim()) continue;

            foreach (var kind in new[] { "document", "video", "audio", "animation", "voice" })
            {
                if (!message.TryGetProperty(kind, out var media)) continue;
                if (!media.TryGetProperty("file_id", out var fid)) continue;
                var name = media.TryGetProperty("file_name", out var fn) ? fn.GetString() ?? "" : "";
                return new MessageFile { FileId = fid.GetString() ?? "", FileName = name };
            }

            if (message.TryGetProperty("photo", out var photos) && photos.GetArrayLength() > 0)
            {
                var last = photos[photos.GetArrayLength() - 1];
                if (last.TryGetProperty("file_id", out var fid))
                    return new MessageFile { FileId = fid.GetString() ?? "", FileName = "telegram_photo.jpg" };
            }
        }

        throw new DownloadErrorException("TG_MESSAGE_NOT_FOUND",
            "getUpdates 最近 100 条更新中找不到该 chat/message（Bot 需已收到该消息，且消息在 24 小时内）");
    }

    private static async Task<JsonElement> CallAsync(string token, string method, string query, CancellationToken ct)
    {
        var url = $"{ApiBase}/bot{token}/{method}" + (query != null ? "?" + query : "");
        using var response = await Client.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new DownloadErrorException($"TG_HTTP_{(int)response.StatusCode}", $"Telegram API {method} 返回 HTTP {response.StatusCode}");
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch (JsonException ex)
        {
            throw new DownloadErrorException("TG_BAD_RESPONSE", $"Telegram API {method} 返回了无法解析的内容", ex);
        }
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

    private sealed class MessageFile
    {
        public string FileId { get; set; } = "";
        public string FileName { get; set; } = "";
    }

    private sealed class TelegramJob
    {
        public DownloadTask Task { get; }
        public CancellationTokenSource Cts { get; } = new();
        public bool Paused { get; set; }
        public long TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }
        public long LastBytes { get; set; }
        public long LastTick { get; set; }
        public long LastSpeed { get; private set; }

        public TelegramJob(DownloadTask task) => Task = task;

        public void UpdateSpeed(long now)
        {
            if (LastTick != 0 && now > LastTick)
                LastSpeed = Math.Max(0, (DownloadedBytes - LastBytes) * 1000 / (now - LastTick));
            LastBytes = DownloadedBytes;
            LastTick = now;
        }
    }
}

