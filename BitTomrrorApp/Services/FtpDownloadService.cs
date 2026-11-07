using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using BitTomrrorApp.Models;
using FluentFTP;

namespace BitTomrrorApp.Services;

public sealed class FtpDownloadService : IDownloadService
{
    private readonly ConcurrentDictionary<string, FtpJob> _jobs = new();

    public DownloadType Type => DownloadType.Ftp;

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
            task.Detail = "已暂停，.part 文件保留可续传";
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

        var job = new FtpJob(task);
        _jobs[task.Id] = job;
        task.Cancellation = job.Cts;

        task.Apply(() =>
        {
            task.Status = DownloadStatus.Preparing;
            task.ErrorCode = "";
            task.ErrorMessage = "";
            task.Detail = "正在连接 FTP 服务器...";
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
            var mapped = MapFtpError(ex);
            SetError(task, mapped.Code, mapped.Message);
        }
        finally
        {
            _jobs.TryRemove(task.Id, out _);
            task.Cancellation = null;
            task.Apply(() => task.RefreshDisplay());
        }
    }

    private async Task RunAsync(FtpJob job)
    {
        var task = job.Task;
        var token = job.Cts.Token;
        if (string.IsNullOrWhiteSpace(task.FtpHost))
            throw new DownloadErrorException("FTP_HOST_MISSING", "缺少 FTP 主机地址");
        if (string.IsNullOrWhiteSpace(task.FtpRemotePath))
            throw new DownloadErrorException("FTP_PATH_MISSING", "缺少 FTP 远程文件路径");

        var user = string.IsNullOrWhiteSpace(task.FtpUser) ? "anonymous" : task.FtpUser;
        var password = string.IsNullOrWhiteSpace(task.FtpUser) ? "anonymous" : task.FtpPassword;
        var client = new AsyncFtpClient(task.FtpHost, user, password, task.FtpPort);
        client.Config.ConnectTimeout = 15000;
        client.Config.DataConnectionConnectTimeout = 20000;
        client.Config.EncryptionMode = task.FtpUseTls ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None;
        client.Config.ValidateAnyCertificate = true;

        var remote = task.FtpRemotePath.Trim().Trim('/');
        var dir = AppPaths.CreateTaskTempDirectory(task);
        Directory.CreateDirectory(task.SavePath);
        var fileName = Path.GetFileName(remote);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "ftp_download.bin";
        var part = Path.Combine(dir, "download.part");
        var finalPath = Path.Combine(task.SavePath, fileName);

        try
        {
            await client.AutoConnect(token);

            long total;
            try { total = await client.GetFileSize(remote, -1, token); }
            catch { total = -1; }

            if (total > 0 && File.Exists(finalPath) && new FileInfo(finalPath).Length == total)
            {
                job.TotalBytes = total;
                job.DownloadedBytes = total;
                task.Apply(() =>
                {
                    task.Name = fileName;
                    task.TotalBytes = total;
                    task.DownloadedBytes = total;
                    task.Progress = 100;
                    task.Status = DownloadStatus.Completed;
                    task.Detail = "目标文件已存在且大小一致";
                    task.RefreshDisplay();
                });
                return;
            }

            job.TotalBytes = total > 0 ? total : 0;
            task.Apply(() =>
            {
                task.Name = fileName;
                task.TotalBytes = job.TotalBytes;
                task.Status = DownloadStatus.Downloading;
                task.Detail = total > 0
                    ? $"FTP {task.FtpHost}:{task.FtpPort} · {DownloadTask.FormatBytes(total)}"
                    : $"FTP {task.FtpHost}:{task.FtpPort} · 大小未知";
                task.RefreshDisplay();
            });

            var existsMode = File.Exists(part) ? FtpLocalExists.Resume : FtpLocalExists.Overwrite;
            var progress = new Progress<FtpProgress>(p =>
            {
                task.Apply(() =>
                {
                    if (job.TotalBytes > 0)
                    {
                        job.DownloadedBytes = (long)(job.TotalBytes * Math.Clamp(p.Progress, 0, 100) / 100);
                        task.DownloadedBytes = job.DownloadedBytes;
                        task.Progress = Math.Clamp(p.Progress, 0, 100);
                    }
                    else
                    {
                        job.DownloadedBytes = Math.Max(job.DownloadedBytes, p.TransferredBytes + job.ResumeBytes);
                        task.DownloadedBytes = job.DownloadedBytes;
                    }
                    task.Detail = $"FTP 下载中 · {p.TransferSpeed / 1024:F1} KB/s";
                    task.RefreshDisplay();
                });
            });

            job.ResumeBytes = File.Exists(part) ? new FileInfo(part).Length : 0;
            var status = await client.DownloadFile(part, remote, existsMode, FtpVerify.None, progress, token);
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            if (status == FtpStatus.Failed)
                throw new DownloadErrorException("FTP_TRANSFER_FAILED", "FTP 传输失败（服务器返回失败状态）");

            if (!File.Exists(part))
                throw new DownloadErrorException("FTP_TRANSFER_FAILED", "FTP 下载未生成本地文件");

            var length = new FileInfo(part).Length;
            if (job.TotalBytes > 0 && length != job.TotalBytes)
                throw new DownloadErrorException("FTP_SIZE_MISMATCH", $"文件大小不一致：期望 {job.TotalBytes} 字节，实际 {length} 字节");

            if (File.Exists(finalPath))
            {
                var stem = Path.GetFileNameWithoutExtension(finalPath);
                var ext = Path.GetExtension(finalPath);
                finalPath = Path.Combine(task.SavePath, $"{stem}_{DateTime.Now:HHmmss}{ext}");
            }
            File.Move(part, finalPath);

            var sha = await HttpDownloadEngine.ComputeSha256Async(finalPath);
            job.DownloadedBytes = length;
            task.Apply(() =>
            {
                task.Name = Path.GetFileName(finalPath);
                task.TotalBytes = length;
                task.DownloadedBytes = length;
                task.Progress = 100;
                task.Status = DownloadStatus.Completed;
                task.DownloadRate = 0;
                task.Detail = $"FTP 完成 · SHA256 {sha}";
                task.RefreshDisplay();
            });
        }
        catch (Exception ex) when (!(ex is OperationCanceledException) && !(ex is DownloadErrorException))
        {
            throw MapFtpError(ex);
        }
        finally
        {
            try { await client.Disconnect(token); } catch { }
            try { client.Dispose(); } catch { }
        }
    }

    private static DownloadErrorException MapFtpError(Exception ex)
    {
        var typeName = ex.GetType().Name;
        if (typeName.Contains("Authentication") || ex.Message.Contains("530"))
            return new DownloadErrorException("FTP_AUTH_FAILED", "FTP 登录失败（用户名/密码错误或服务器拒绝匿名登录）", ex);
        if (ex.Message.Contains("550"))
            return new DownloadErrorException("FTP_FILE_NOT_FOUND", "FTP 服务器返回 550：文件不存在或无访问权限", ex);
        if (ex is SocketException se)
        {
            return se.SocketErrorCode switch
            {
                SocketError.HostNotFound => new DownloadErrorException("DNS_FAILED", "无法解析 FTP 主机域名", ex),
                SocketError.ConnectionRefused => new DownloadErrorException("FTP_CONNECT_REFUSED", "FTP 服务器拒绝连接（端口未开放）", ex),
                SocketError.TimedOut => new DownloadErrorException("FTP_TIMEOUT", "FTP 连接超时", ex),
                _ => new DownloadErrorException("FTP_NETWORK_FAILED", $"FTP 网络错误：{se.Message}", ex)
            };
        }
        if (ex is TimeoutException || ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            return new DownloadErrorException("FTP_TIMEOUT", "FTP 操作超时", ex);
        if (ex.Message.Contains("SSL") || ex.Message.Contains("TLS"))
            return new DownloadErrorException("FTP_TLS_FAILED", $"FTPS 握手失败：{ex.Message}", ex);
        return new DownloadErrorException("FTP_FAILED", ex.Message, ex);
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

    private sealed class FtpJob
    {
        public DownloadTask Task { get; }
        public CancellationTokenSource Cts { get; } = new();
        public bool Paused { get; set; }
        public long TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }
        public long ResumeBytes { get; set; }
        public long LastBytes { get; set; }
        public long LastTick { get; set; }
        public long LastSpeed { get; private set; }

        public FtpJob(DownloadTask task) => Task = task;

        public void UpdateSpeed(long now)
        {
            if (LastTick != 0 && now > LastTick)
                LastSpeed = Math.Max(0, (DownloadedBytes - LastBytes) * 1000 / (now - LastTick));
            LastBytes = DownloadedBytes;
            LastTick = now;
        }
    }
}
