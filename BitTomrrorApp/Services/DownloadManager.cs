using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows.Threading;
using BitTomrrorApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BitTomrrorApp.Services;

public partial class DownloadManager : ObservableObject
{
    public static DownloadManager Instance { get; } = new();

    private readonly Dictionary<DownloadType, IDownloadService> _services = new();
    private readonly BitTorrentDownloadService _bt = new();
    private DispatcherTimer _timer;
    private SynchronizationContext _syncContext;
    private DateTime _lastSave = DateTime.MinValue;

    public ObservableCollection<DownloadTask> Tasks { get; } = new();
    public AppSettings Settings { get; private set; } = new();
    public FfmpegLocator Ffmpeg { get; } = new();
    public BitTorrentDownloadService BitTorrent => _bt;

    [ObservableProperty] private string _totalDownloadRateText = "↓ 0 B/s";
    [ObservableProperty] private string _totalUploadRateText = "↑ 0 B/s";
    [ObservableProperty] private string _taskSummaryText = "0 个任务";
    [ObservableProperty] private string _ffmpegStatusText = "FFmpeg 检测中...";
    [ObservableProperty] private string _ffmpegPathText = "";

    public bool HasTasks => Tasks.Count > 0;
    public bool IsEmpty => !HasTasks;

    // ---- 个人设置中心 ----
    public bool IsLocked => !string.IsNullOrEmpty(Settings.LockPasswordHash);

    public void SaveSettingsNow()
    {
        try { DownloadStore.SaveSettings(Settings); } catch { }
    }

    public async Task InitializeAsync(Dispatcher dispatcher)
    {
        _syncContext = SynchronizationContext.Current ?? new DispatcherSynchronizationContext(dispatcher);
        AppPaths.Ensure();
        Settings = DownloadStore.LoadSettings();
        await Ffmpeg.CheckAsync(Settings.FfmpegPath);
        FfmpegStatusText = Ffmpeg.StatusText;
        FfmpegPathText = Ffmpeg.Path;

        _services[DownloadType.BitTorrent] = _bt;
        _services[DownloadType.Http] = new HttpDownloadService();
        _services[DownloadType.M3u8] = new M3u8DownloadService();
        _services[DownloadType.Telegram] = new TelegramDownloadService();
        _services[DownloadType.Ftp] = new FtpDownloadService();

        Tasks.CollectionChanged += OnTasksChanged;

        foreach (var task in DownloadStore.LoadTasks())
        {
            task.SyncContext = _syncContext;
            task.Cancellation = null;
            if (task.Status is DownloadStatus.Downloading or DownloadStatus.Preparing or DownloadStatus.Seeding)
                task.Status = DownloadStatus.Paused;
            task.RefreshDisplay();
            Tasks.Add(task);
        }

        await _bt.InitializeAsync();

        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        TaskSummaryText = BuildSummary();
    }

    private void OnTasksChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(IsEmpty));
        TaskSummaryText = BuildSummary();
    }

    public async Task<bool> AddTaskAsync(DownloadTask task)
    {
        if (task == null) return false;
        var duplicate = Tasks.Any(t => t.Type == task.Type &&
                                       string.Equals(t.Source, task.Source, StringComparison.OrdinalIgnoreCase) &&
                                       t.Status != DownloadStatus.Completed);
        if (duplicate) return false;

        AppPaths.Ensure();
        if (string.IsNullOrWhiteSpace(task.SavePath)) task.SavePath = Settings.DefaultSavePath;
        Directory.CreateDirectory(task.SavePath);
        task.SyncContext = _syncContext;
        task.Status = DownloadStatus.Waiting;
        task.RefreshDisplay();
        Tasks.Insert(0, task);
        SaveState();

        var service = _services[task.Type];
        // 启动/恢复立即返回，不阻塞 UI；下载过程中的失败由服务写入 task.Status/ErrorMessage
        _ = ObserveAsync(service.StartAsync(task));
        return true;
    }

    private static async Task ObserveAsync(Task work)
    {
        try { await work; } catch { }
    }

    public async Task PauseAsync(DownloadTask task)
    {
        var service = _services[task.Type];
        await service.PauseAsync(task);
        SaveState();
    }

    public Task ResumeAsync(DownloadTask task)
    {
        var service = _services[task.Type];
        _ = ObserveAsync(service.ResumeAsync(task));
        SaveState();
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(DownloadTask task)
    {
        var service = _services[task.Type];
        await service.RemoveAsync(task);
        Tasks.Remove(task);
        SaveState();
    }

    public async Task PauseAllAsync()
    {
        foreach (var task in Tasks.Where(t => t.Status is DownloadStatus.Downloading or DownloadStatus.Preparing or DownloadStatus.Seeding).ToList())
            await PauseAsync(task);
    }

    public async Task DeleteAllAsync()
    {
        foreach (var task in Tasks.ToList())
            await DeleteAsync(task);
    }

    private void OnTick()
    {
        foreach (var service in _services.Values)
        {
            try { service.Tick(); } catch { }
        }

        var active = Tasks.Where(t => t.Status is DownloadStatus.Downloading or DownloadStatus.Preparing or DownloadStatus.Seeding).ToList();
        var totalDown = active.Sum(t => Math.Max(0, t.DownloadRate));
        var totalUp = active.Sum(t => Math.Max(0, t.UploadRate));
        TotalDownloadRateText = "↓ " + DownloadTask.FormatRate(totalDown);
        TotalUploadRateText = "↑ " + DownloadTask.FormatRate(totalUp);
        TaskSummaryText = BuildSummary();

        if (active.Count > 0 && (DateTime.UtcNow - _lastSave).TotalSeconds >= 10)
        {
            _lastSave = DateTime.UtcNow;
            SaveState();
        }
    }

    private string BuildSummary()
    {
        var active = Tasks.Count(t => t.Status is DownloadStatus.Downloading or DownloadStatus.Preparing or DownloadStatus.Seeding);
        var completed = Tasks.Count(t => t.Status == DownloadStatus.Completed);
        return $"{Tasks.Count} 个任务 · {active} 个进行中 · {completed} 个已完成";
    }

    public void SaveState()
    {
        try { DownloadStore.SaveTasks(Tasks); } catch { }
    }

    public async Task UpdateFfmpegPathAsync(string path)
    {
        Settings.FfmpegPath = path ?? "";
        DownloadStore.SaveSettings(Settings);
        await Ffmpeg.CheckAsync(Settings.FfmpegPath);
        FfmpegStatusText = Ffmpeg.StatusText;
        FfmpegPathText = Ffmpeg.Path;
    }

    public void UpdateDefaultSavePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        Settings.DefaultSavePath = path;
        DownloadStore.SaveSettings(Settings);
    }

    public async Task ShutdownAsync()
    {
        _timer?.Stop();
        SaveState();
        DownloadStore.SaveSettings(Settings);
        foreach (var service in _services.Values)
        {
            try { await service.ShutdownAsync(); } catch { }
        }
    }
}
