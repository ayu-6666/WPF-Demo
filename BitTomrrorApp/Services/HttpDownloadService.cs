using System.Collections.Concurrent;
using System.IO;
using BitTomrrorApp.Models;

namespace BitTomrrorApp.Services;

public class HttpDownloadService : IDownloadService
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobs = new();
    private readonly ConcurrentDictionary<string, Sample> _samples = new();
    private readonly ConcurrentDictionary<string, bool> _pausing = new();

    public DownloadType Type => DownloadType.Http;

    public Task StartAsync(DownloadTask task) => LaunchAsync(task);

    public Task ResumeAsync(DownloadTask task) => LaunchAsync(task);

    public Task PauseAsync(DownloadTask task)
    {
        _pausing[task.Id] = true;
        Cancel(task);
        task.Apply(() =>
        {
            task.Status = DownloadStatus.Paused;
            task.DownloadRate = 0;
            task.UploadRate = 0;
            task.Detail = "已暂停，可继续（保留已下载数据）";
            task.RefreshDisplay();
        });
        return Task.CompletedTask;
    }

    public Task RemoveAsync(DownloadTask task)
    {
        Cancel(task);
        _active.TryRemove(task.Id, out _);
        _samples.TryRemove(task.Id, out _);
        var dir = Path.Combine(AppPaths.TempDirectory, task.Id);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        foreach (var cts in _jobs.Values) cts.Cancel();
        return Task.CompletedTask;
    }

    public void Tick()
    {
        foreach (var item in _active.Values)
        {
            if (item.Status != DownloadStatus.Downloading) continue;
            var sample = _samples.GetOrAdd(item.Id, _ => new Sample());
            item.Apply(() =>
            {
                var now = Environment.TickCount64;
                var bytes = item.DownloadedBytes;
                if (sample.LastTick != 0 && now > sample.LastTick && bytes >= sample.LastBytes)
                {
                    var delta = bytes - sample.LastBytes;
                    var rate = delta * 1000 / Math.Max(1, now - sample.LastTick);
                    item.DownloadRate = Math.Max(0, rate);
                }
                sample.LastBytes = bytes;
                sample.LastTick = now;
                item.RefreshDisplay();
            });
        }
    }

    private readonly ConcurrentDictionary<string, DownloadTask> _active = new();

    private async Task LaunchAsync(DownloadTask task)
    {
        Cancel(task);
        _pausing.TryRemove(task.Id, out _);
        var cts = new CancellationTokenSource();
        _jobs[task.Id] = cts;
        _active[task.Id] = task;
        _samples[task.Id] = new Sample();
        task.Cancellation = cts;

        task.Apply(() =>
        {
            task.Status = DownloadStatus.Preparing;
            task.ErrorCode = "";
            task.ErrorMessage = "";
            task.Detail = "正在解析服务器信息...";
            task.DownloadRate = 0;
            task.RefreshDisplay();
        });

        try
        {
            var info = await HttpDownloadEngine.ResolveAsync(task.Source, cts.Token);
            var dir = AppPaths.CreateTaskTempDirectory(task);
            var finalPath = Path.Combine(task.SavePath, info.FileName);
            Directory.CreateDirectory(task.SavePath);

            task.Apply(() =>
            {
                task.Name = info.FileName;
                task.TotalBytes = info.TotalBytes;
                task.Detail = info.SupportsRanges ? "支持断点续传" : "服务器不支持 Range，无法断点续传";
                task.Status = DownloadStatus.Downloading;
                task.RefreshDisplay();
            });

            var job = new HttpDownloadJob
            {
                Info = info,
                Directory = dir,
                FinalPath = finalPath,
                Connections = Math.Max(1, task.HttpConnections),
                RateLimitBytesPerSec = task.DownloadLimitBytesPerSec,
                Progress = (downloaded, total, detail) => task.Apply(() =>
                {
                    task.DownloadedBytes = downloaded;
                    if (total > 0) task.TotalBytes = total;
                    if (task.TotalBytes > 0)
                        task.Progress = Math.Min(100, downloaded * 100.0 / task.TotalBytes);
                    task.Detail = detail;
                    task.RefreshDisplay();
                })
            };

            await HttpDownloadEngine.DownloadAsync(job, cts.Token);

            task.Apply(() =>
            {
                task.DownloadedBytes = task.TotalBytes > 0 ? task.TotalBytes : new FileInfo(job.FinalPath).Length;
                task.TotalBytes = task.DownloadedBytes;
                task.Progress = 100;
                task.Status = DownloadStatus.Completed;
                task.DownloadRate = 0;
                task.UploadRate = 0;
                task.Detail = $"已完成 · {job.Info.FileName}";
                task.RefreshDisplay();
            });
        }
        catch (OperationCanceledException)
        {
            if (!_pausing.ContainsKey(task.Id))
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
            var mapped = DownloadErrors.FromHttp(ex);
            SetError(task, mapped.Code, mapped.Message);
        }
        finally
        {
            _jobs.TryRemove(task.Id, out _);
            _pausing.TryRemove(task.Id, out _);
            _active.TryRemove(task.Id, out _);
            task.Cancellation = null;
            task.Apply(() => task.RefreshDisplay());
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
            task.UploadRate = 0;
            task.Detail = message;
            task.RefreshDisplay();
        });
    }

    private void Cancel(DownloadTask task)
    {
        var cts = _jobs.TryRemove(task.Id, out var existing) ? existing : null;
        if (cts == null) return;
        try { cts.Cancel(); } catch { }
    }

    private sealed class Sample
    {
        public long LastBytes;
        public long LastTick;
    }
}
