using BitTomrrorApp.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;

namespace BitTomrrorApp.Services;

// 内存模拟引擎：保持原 API 形状，让 UI 可独立演示
// （移除了不匹配的 MonoTorrent 3.0.2 依赖）
public class TorrentEngineService
{
    private static TorrentEngineService _instance;
    public static TorrentEngineService Instance => _instance ??= new TorrentEngineService();

    public ObservableCollection<TorrentItem> TorrentItems { get; } = new();

    private Dispatcher _uiDispatcher;
    private DispatcherTimer _refreshTimer;
    private readonly Random _rnd = new();
    private readonly string _downloadPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public Task InitializeAsync(Dispatcher dispatcher)
    {
        _uiDispatcher = dispatcher;
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _refreshTimer.Tick += OnRefreshTick;
        _refreshTimer.Start();
        return Task.CompletedTask;
    }

    private void OnRefreshTick(object sender, EventArgs e)
    {
        if (_uiDispatcher == null) return;
        _uiDispatcher.Invoke(() =>
        {
            foreach (var item in TorrentItems)
            {
                if (item.State == TorrentState.Downloading)
                {
                    // 模拟推进进度
                    var step = _rnd.Next(1, 5);
                    item.Progress = Math.Min(100, item.Progress + step);
                    var dlRate = _rnd.Next(80, 1500) * 1024L;
                    var upRate = _rnd.Next(10, 200) * 1024L;
                    item.DownloadSpeed = FormatSpeed(dlRate);
                    item.UploadSpeed = FormatSpeed(upRate);
                    var remainBytes = (long)((100 - item.Progress) / 100.0 * item.TotalSize);
                    var seconds = dlRate > 0 ? remainBytes / (double)dlRate : 0;
                    item.RemainingTime = seconds > 0
                        ? TimeSpan.FromSeconds(seconds).GetRemainingTimeString()
                        : "即将完成";
                    if (item.Progress >= 100)
                    {
                        item.Progress = 100;
                        item.State = TorrentState.Completed;
                        item.DownloadSpeed = "-";
                        item.UploadSpeed = FormatSpeed(_rnd.Next(20, 200) * 1024L);
                        item.RemainingTime = "已完成";
                    }
                }
                else if (item.State == TorrentState.Seeding)
                {
                    item.UploadSpeed = FormatSpeed(_rnd.Next(30, 300) * 1024L);
                    item.DownloadSpeed = "-";
                    item.RemainingTime = "做种中";
                }
            }
        });
    }

    public Task AddTorrentAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("无效的磁力链接或BT文件");

        // 模拟从输入中解析名称与大小
        var name = "Sample_Torrent_" + (_rnd.Next(1000, 9999));
        if (input.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            // 从磁链里抓个 hash 片段
            var idx = input.IndexOf("xt=urn:btih:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var hash = input.Substring(idx + 12, Math.Min(40, input.Length - idx - 12));
                name = "Magnet_" + hash.Substring(0, Math.Min(8, hash.Length));
            }
        }
        else
        {
            try { name = Path.GetFileNameWithoutExtension(input) ?? name; } catch { }
        }

        var item = new TorrentItem
        {
            Name = name,
            Hash = Guid.NewGuid().ToString("N"),
            TotalSize = _rnd.NextInt64(50L * 1024 * 1024, 4L * 1024 * 1024 * 1024),
            SavePath = _downloadPath,
            State = TorrentState.Downloading,
            Progress = 0,
            DownloadSpeed = FormatSpeed(_rnd.Next(80, 1200) * 1024L),
            UploadSpeed = FormatSpeed(_rnd.Next(10, 200) * 1024L),
            RemainingTime = "计算中..."
        };

        _uiDispatcher?.Invoke(() => TorrentItems.Add(item));
        return Task.CompletedTask;
    }

    public Task PauseTorrentAsync(TorrentItem item)
    {
        if (item == null) return Task.CompletedTask;
        _uiDispatcher?.Invoke(() =>
        {
            if (item.State == TorrentState.Downloading || item.State == TorrentState.Seeding)
            {
                item.State = TorrentState.Paused;
                item.DownloadSpeed = "-";
                item.UploadSpeed = "-";
                item.RemainingTime = "已暂停";
            }
            else if (item.State == TorrentState.Paused)
            {
                item.State = TorrentState.Downloading;
            }
        });
        return Task.CompletedTask;
    }

    public Task DeleteTorrentAsync(TorrentItem item, bool deleteFile = false)
    {
        if (item == null) return Task.CompletedTask;
        _uiDispatcher?.Invoke(() => TorrentItems.Remove(item));
        return Task.CompletedTask;
    }

    public Task BatchPauseAsync(IEnumerable<TorrentItem> items)
    {
        if (items == null) return Task.CompletedTask;
        foreach (var item in items.ToList())
            _ = PauseTorrentAsync(item);
        return Task.CompletedTask;
    }

    public Task BatchDeleteAsync(IEnumerable<TorrentItem> items, bool deleteFile = false)
    {
        if (items == null) return Task.CompletedTask;
        foreach (var item in items.ToList())
            _ = DeleteTorrentAsync(item, deleteFile);
        return Task.CompletedTask;
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
        return $"{bytesPerSecond / 1024.0 / 1024:F1} MB/s";
    }
}

public static class TimeSpanExtensions
{
    public static string GetRemainingTimeString(this TimeSpan time)
    {
        if (time.TotalSeconds <= 0) return "即将完成";
        if (time.TotalSeconds < 60) return $"剩余 {time.TotalSeconds:F0} 秒";
        if (time.TotalMinutes < 60) return $"剩余 {time.TotalMinutes:F0} 分钟";
        if (time.TotalHours < 24) return $"剩余 {time.TotalHours:F1} 小时";
        return $"剩余 {time.TotalDays:F1} 天";
    }
}
