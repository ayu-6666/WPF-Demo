using System.Collections.Concurrent;
using System.IO;
using System.Net;
using BitTomrrorApp.Models;
using MonoTorrent;
using MonoTorrent.Client;

namespace BitTomrrorApp.Services;

public sealed class BitTorrentDownloadService : IDownloadService
{
    private static readonly string[] DefaultTrackers =
    {
        "udp://tracker.opentrackr.org:1337/announce",
        "udp://open.stealth.si:80/announce",
        "udp://tracker.torrent.eu.org:451/announce",
        "udp://explodie.org:6969/announce",
        "udp://tracker.tiny-vps.com:6969/announce",
        "http://tracker.openbittorrent.com:80/announce"
    };

    private readonly ConcurrentDictionary<string, BtJob> _jobs = new();
    private ClientEngine _engine;
    private readonly SemaphoreSlim _engineLock = new(1, 1);

    public DownloadType Type => DownloadType.BitTorrent;

    public async Task InitializeAsync()
    {
        AppPaths.Ensure();
        await EnsureEngineAsync();
    }

    private async Task EnsureEngineAsync()
    {
        if (_engine != null) return;
        await _engineLock.WaitAsync();
        try
        {
            if (_engine != null) return;
            var builder = new EngineSettingsBuilder
            {
                AllowLocalPeerDiscovery = true,
                AllowPortForwarding = false,
                AutoSaveLoadDhtCache = true,
                AutoSaveLoadFastResume = true,
                AutoSaveLoadMagnetLinkMetadata = true,
                CacheDirectory = AppPaths.BtCacheDirectory,
                FastResumeMode = FastResumeMode.Accurate,
                MaximumConnections = 200,
                MaximumHalfOpenConnections = 20,
                DhtEndPoint = new IPEndPoint(IPAddress.Any, 0),
                ListenEndPoints = new Dictionary<string, IPEndPoint>
                {
                    ["IPv4"] = new IPEndPoint(IPAddress.Any, 0)
                }
            };
            _engine = new ClientEngine(builder.ToSettings());
        }
        finally
        {
            _engineLock.Release();
        }
    }

    public async Task StartAsync(DownloadTask task)
    {
        await LaunchAsync(task);
    }

    public async Task ResumeAsync(DownloadTask task)
    {
        // 同一进程内暂停后继续：直接复用引擎中已注册的 TorrentManager，
        // 避免重复 AddAsync 触发 "already added/duplicate"，同时保留事件统计。
        var job = _jobs.TryGetValue(task.Id, out var existing) ? existing : null;
        if (job?.Manager != null)
        {
            task.Apply(() =>
            {
                task.Status = DownloadStatus.Preparing;
                task.ErrorCode = "";
                task.ErrorMessage = "";
                task.Detail = "正在恢复连接...";
                task.DownloadRate = 0;
                task.RefreshDisplay();
            });
            try
            {
                await job.Manager.StartAsync();
            }
            catch (Exception ex)
            {
                SetError(task, "BT_RESUME_FAILED", $"恢复下载失败：{ex.Message}");
            }
            return;
        }
        await LaunchAsync(task);
    }

    private async Task LaunchAsync(DownloadTask task)
    {
        var old = _jobs.TryRemove(task.Id, out var existing) ? existing : null;
        if (old != null)
        {
            try { await old.Manager.StopAsync(); } catch { }
        }

        await EnsureEngineAsync();

        var job = new BtJob(task);
        _jobs[task.Id] = job;

        task.Apply(() =>
        {
            task.Status = DownloadStatus.Preparing;
            task.ErrorCode = "";
            task.ErrorMessage = "";
            task.Detail = "正在加载种子/磁力链接...";
            task.DownloadRate = 0;
            task.RefreshDisplay();
        });

        try
        {
            Directory.CreateDirectory(task.SavePath);
            Torrent torrent = null;
            MagnetLink magnet = null;

            var source = task.TorrentPath;
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                source = task.Source;

            if (File.Exists(source))
            {
                var copyPath = Path.Combine(AppPaths.TorrentsDirectory, task.Id + ".torrent");
                if (!File.Exists(copyPath))
                {
                    Directory.CreateDirectory(AppPaths.TorrentsDirectory);
                    File.Copy(source, copyPath, true);
                }
                task.TorrentPath = copyPath;
                try
                {
                    torrent = await Torrent.LoadAsync(copyPath);
                }
                catch (Exception ex)
                {
                    throw new DownloadErrorException("BT_TORRENT_INVALID", $"无法解析种子文件：{ex.Message}", ex);
                }
            }
            else if (source.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    magnet = MagnetLink.Parse(source);
                }
                catch (Exception ex)
                {
                    throw new DownloadErrorException("BT_MAGNET_INVALID", $"磁力链接格式无效：{ex.Message}", ex);
                }
                if (magnet.AnnounceUrls.Count == 0)
                    magnet = new MagnetLink(magnet.InfoHashes, magnet.Name, DefaultTrackers, null, magnet.Size);
            }
            else
            {
                throw new DownloadErrorException("BT_SOURCE_INVALID", "请提供 .torrent 文件或 magnet:?xt=urn:btih:... 磁力链接");
            }

            var settings = new TorrentSettingsBuilder
            {
                AllowDht = true,
                AllowPeerExchange = true,
                CreateContainingDirectory = true,
                MaximumConnections = 80,
                MaximumDownloadRate = (int)Math.Min(int.MaxValue, task.DownloadLimitBytesPerSec),
                MaximumUploadRate = (int)Math.Min(int.MaxValue, task.UploadLimitBytesPerSec)
            }.ToSettings();

            TorrentManager manager;
            try
            {
                manager = torrent != null
                    ? await _engine.AddAsync(torrent, task.SavePath, settings)
                    : await _engine.AddAsync(magnet, task.SavePath, settings);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (message.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                    throw new DownloadErrorException("BT_DUPLICATE", "该种子已在下载引擎中（请不要重复添加同一 info hash）", ex);
                throw new DownloadErrorException("BT_ADD_FAILED", $"加入 BT 引擎失败：{message}", ex);
            }

            job.Manager = manager;
            WireEvents(job);
            await manager.StartAsync();

            // 磁力链接需要等待元数据；后台继续，不阻塞 UI
            if (torrent == null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await manager.WaitForMetadataAsync(job.Cts.Token);
                        UpdateMetadata(job);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        job.MetadataError = ex.Message;
                    }
                });
            }
            else
            {
                UpdateMetadata(job);
            }

            task.Apply(() =>
            {
                task.Status = DownloadStatus.Preparing;
                task.Detail = torrent != null ? "种子已加载，正在连接 peers..." : "正在通过 DHT/tracker 获取磁力元数据...";
                task.RefreshDisplay();
            });
        }
        catch (DownloadErrorException ex)
        {
            await StopJobAsync(job);
            SetError(task, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            await StopJobAsync(job);
            SetError(task, "BT_FAILED", ex.Message);
        }
    }

    private void UpdateMetadata(BtJob job)
    {
        var manager = job.Manager;
        if (manager?.Torrent == null) return;
        job.Task.Apply(() =>
        {
            job.Task.Name = manager.Torrent.Name;
            job.Task.TotalBytes = manager.Torrent.Size;
            job.Task.Detail = $"元数据已获取 · {manager.Torrent.PieceCount} 个分片";
            job.Task.RefreshDisplay();
        });
    }

    private void WireEvents(BtJob job)
    {
        var manager = job.Manager;
        manager.PieceHashed += (_, e) =>
        {
            if (e.HashPassed)
                Interlocked.Increment(ref job.HashPassed);
            else
                Interlocked.Increment(ref job.HashFailed);
        };

        manager.ConnectionAttemptFailed += (_, e) =>
        {
            Interlocked.Increment(ref job.ConnectionFailures);
            job.LastFailure = e.Reason.ToString();
        };

        manager.PeerConnected += (_, e) =>
        {
            Interlocked.Increment(ref job.PeerConnects);
        };

        manager.TorrentStateChanged += (_, e) =>
        {
            // 真正开始下载后重新计数，避免把启动时对空文件的预校验当成“坏块重下”
            if (e.NewState == TorrentState.Downloading && !job.DownloadStarted)
            {
                job.DownloadStarted = true;
                Interlocked.Exchange(ref job.HashPassed, 0);
                Interlocked.Exchange(ref job.HashFailed, 0);
            }
            if (e.NewState == TorrentState.Seeding && !job.VerifyStarted)
            {
                job.VerifyStarted = true;
                _ = Task.Run(() => VerifyAsync(job));
            }
            if (e.NewState == TorrentState.Error)
            {
                job.StateError = manager.Error?.Exception?.Message ?? manager.Error?.Reason.ToString() ?? "BT 引擎状态错误";
            }
        };
    }

    private async Task VerifyAsync(BtJob job)
    {
        var manager = job.Manager;
        var task = job.Task;
        try
        {
            job.Verifying = true;
            // 重置统计，避免把启动时对空文件的预校验计入“坏块”
            Interlocked.Exchange(ref job.HashPassed, 0);
            Interlocked.Exchange(ref job.HashFailed, 0);
            task.Apply(() =>
            {
                task.Status = DownloadStatus.Preparing;
                task.Detail = "下载完成，正在进行全量分片哈希校验...";
                task.DownloadRate = 0;
                task.RefreshDisplay();
            });

            await manager.StopAsync();
            await manager.HashCheckAsync(false);

            if (manager.Complete)
            {
                job.Verified = true;
                var wantsSeeding = task.SeedRatioLimit > 0 || task.SeedTimeLimitMinutes > 0;
                if (wantsSeeding)
                {
                    await manager.StartAsync();
                    task.Apply(() =>
                    {
                        task.Progress = 100;
                        task.DownloadedBytes = task.TotalBytes;
                        task.Status = DownloadStatus.Seeding;
                        task.Detail = $"完整性校验通过：{job.HashPassed} 个分片哈希有效 · 开始做种";
                        task.RefreshDisplay();
                    });
                }
                else
                {
                    task.Apply(() =>
                    {
                        job.Completed = true;
                        task.Progress = 100;
                        task.DownloadedBytes = task.TotalBytes;
                        task.Status = DownloadStatus.Completed;
                        task.Detail = $"完整性校验通过：{job.HashPassed} 个分片哈希有效 · 已完成";
                        task.RefreshDisplay();
                    });
                }
            }
            else
            {
                job.VerifyStarted = false;
                task.Apply(() =>
                {
                    task.Detail = $"校验发现 {job.HashFailed} 个坏块，正在重新下载...";
                    task.RefreshDisplay();
                });
                await manager.StartAsync();
            }
        }
        catch (Exception ex)
        {
            job.Verifying = false;
            task.Apply(() =>
            {
                task.Detail = $"完整性校验异常：{ex.Message}";
                task.RefreshDisplay();
            });
        }
        finally
        {
            job.Verifying = false;
        }
    }

    public async Task PauseAsync(DownloadTask task)
    {
        var job = _jobs.TryGetValue(task.Id, out var existing) ? existing : null;
        if (job?.Manager == null) return;
        try { await job.Manager.PauseAsync(); } catch { }
        task.Apply(() =>
        {
            task.Status = DownloadStatus.Paused;
            task.DownloadRate = 0;
            task.UploadRate = 0;
            task.Detail = "已暂停，重启应用后可继续（MonoTorrent FastResume）";
            task.RefreshDisplay();
        });
    }

    public async Task RemoveAsync(DownloadTask task)
    {
        var job = _jobs.TryRemove(task.Id, out var existing) ? existing : null;
        if (job == null) return;
        await StopJobAsync(job);
        try { job.Cts.Cancel(); } catch { }
    }

    private async Task StopJobAsync(BtJob job)
    {
        try { if (job.Manager != null) await job.Manager.StopAsync(); } catch { }
        try { job.Cts.Cancel(); } catch { }
    }

    public async Task ShutdownAsync()
    {
        foreach (var job in _jobs.Values)
        {
            try { if (job.Manager != null) await job.Manager.StopAsync(TimeSpan.FromSeconds(5)); } catch { }
        }
        _jobs.Clear();
    }

    public void Tick()
    {
        foreach (var job in _jobs.Values)
        {
            var manager = job.Manager;
            var task = job.Task;
            if (manager == null) continue;

            task.Apply(() =>
            {
                if (job.Verifying)
                {
                    task.Status = DownloadStatus.Preparing;
                    task.RefreshDisplay();
                    return;
                }

                if (job.Completed)
                {
                    task.Status = DownloadStatus.Completed;
                    task.DownloadRate = 0;
                    task.RefreshDisplay();
                    return;
                }

                if (manager.Torrent != null)
                {
                    task.TotalBytes = manager.Torrent.Size;
                    task.DownloadedBytes = (long)(manager.Torrent.Size * manager.Progress / 100.0);
                }
                task.Progress = Math.Clamp(manager.Progress, 0, 100);
                if (task.TotalBytes > 0)
                    task.DownloadedBytes = Math.Max(task.DownloadedBytes, (long)(task.TotalBytes * task.Progress / 100.0));

                task.DownloadRate = manager.Monitor.DownloadRate;
                task.UploadRate = manager.Monitor.UploadRate;
                task.Peers = manager.OpenConnections;
                task.Seeds = manager.Peers.Seeds;
                task.Connections = manager.Peers.Available + manager.Peers.Seeds + manager.Peers.Leechs;

                // 校验通过后不再覆盖“完整性校验通过”的说明文字
                if (!job.Verified)
                {
                    var hashInfo = job.HashFailed > 0
                        ? $"分片哈希 {job.HashPassed} 通过 / {job.HashFailed} 失败(已重下)"
                        : $"分片哈希 {job.HashPassed} 通过";
                    task.Detail = $"{hashInfo} · 已知 peers {task.Connections} · 失败连接 {job.ConnectionFailures}";
                }

                switch (manager.State)
                {
                    case TorrentState.Metadata:
                        task.Status = DownloadStatus.Preparing;
                        task.Detail = "等待磁力元数据（DHT/tracker 首次发现 peer 可能需要 1-3 分钟）";
                        break;
                    case TorrentState.Hashing:
                    case TorrentState.HashingPaused:
                    case TorrentState.FetchingHashes:
                        task.Status = DownloadStatus.Preparing;
                        task.Detail = "正在校验已下载分片...";
                        break;
                    case TorrentState.Downloading:
                        task.Status = DownloadStatus.Downloading;
                        break;
                    case TorrentState.Seeding:
                        task.Status = DownloadStatus.Seeding;
                        if (job.Verified)
                            task.Progress = 100;
                        break;
                    case TorrentState.Paused:
                        task.Status = DownloadStatus.Paused;
                        break;
                    case TorrentState.Error:
                        task.Status = DownloadStatus.Error;
                        task.ErrorCode = "BT_ENGINE_ERROR";
                        task.ErrorMessage = job.StateError ?? "BT 引擎错误";
                        break;
                }

                if (manager.Complete && !job.Verified && !job.Verifying && manager.State != TorrentState.Seeding)
                    task.Progress = 100;

                // 校验通过且未设置做种上限时，校验结束后即标记完成并停止做种
                if (job.Verified && !job.Completed &&
                    manager.State is TorrentState.Seeding or TorrentState.Stopped)
                {
                    var wantsSeeding = job.Task.SeedRatioLimit > 0 || job.Task.SeedTimeLimitMinutes > 0;
                    if (!wantsSeeding)
                    {
                        job.Completed = true;
                        task.Status = DownloadStatus.Completed;
                        task.DownloadRate = 0;
                        _ = Task.Run(async () => { try { await manager.StopAsync(); } catch { } });
                    }
                }

                // 做种比例/时间上限
                var size = manager.Torrent?.Size ?? 0;
                var uploaded = manager.Monitor.DataBytesSent;
                var ratio = size > 0 ? uploaded / (double)size : 0;
                var seedMinutes = (DateTime.UtcNow - job.StartedAt).TotalMinutes;
                var ratioReached = job.Task.SeedRatioLimit > 0 && ratio >= job.Task.SeedRatioLimit;
                var timeReached = job.Task.SeedTimeLimitMinutes > 0 && seedMinutes >= job.Task.SeedTimeLimitMinutes;
                if ((ratioReached || timeReached) && manager.State == TorrentState.Seeding && !job.Completed)
                {
                    job.Completed = true;
                    var reason = ratioReached ? $"做种比例达到 {ratio:F2}" : $"做种时间达到 {job.Task.SeedTimeLimitMinutes} 分钟";
                    task.Detail = $"已完成 · {reason}，停止做种";
                    _ = Task.Run(async () => { try { await manager.StopAsync(); } catch { } });
                }

                // 超时保护：3 分钟内没有任何 peer，给出明确原因
                if (!job.TimedOut && manager.State is TorrentState.Downloading or TorrentState.Metadata &&
                    manager.OpenConnections == 0 && task.DownloadedBytes == 0 &&
                    (DateTime.UtcNow - job.StartedAt).TotalSeconds >= 180)
                {
                    job.TimedOut = true;
                    var code = manager.Torrent == null ? "BT_METADATA_TIMEOUT" : "BT_NO_PEERS_TIMEOUT";
                    var message = manager.Torrent == null
                        ? "3 分钟内未获取到磁力元数据：tracker 无响应或 DHT 未发现任何 peer"
                        : $"3 分钟内未连接任何 peer：tracker/DHT 未返回可用节点，或该种子没有做种者（失败连接 {job.ConnectionFailures} 次，最近原因 {job.LastFailure}）";
                    task.Status = DownloadStatus.Error;
                    task.ErrorCode = code;
                    task.ErrorMessage = message;
                    task.DownloadRate = 0;
                    _ = Task.Run(async () => { try { await manager.StopAsync(); } catch { } });
                }

                task.RefreshDisplay();
            });
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

    private sealed class BtJob
    {
        public DownloadTask Task { get; }
        public CancellationTokenSource Cts { get; } = new();
        public TorrentManager Manager { get; set; }
        public DateTime StartedAt { get; } = DateTime.UtcNow;
        public int HashPassed;
        public int HashFailed;
        public int ConnectionFailures;
        public int PeerConnects;
        public string LastFailure = "";
        public string StateError;
        public string MetadataError;
        public bool DownloadStarted;
        public bool VerifyStarted;
        public bool Verifying;
        public bool Verified;
        public bool Completed;
        public bool TimedOut;

        public BtJob(DownloadTask task) => Task = task;
    }
}
