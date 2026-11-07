using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace BitTomrrorApp.Models;

public enum DownloadType
{
    BitTorrent,
    Http,
    M3u8,
    Telegram,
    Ftp
}

public enum DownloadStatus
{
    Waiting,
    Preparing,
    Downloading,
    Paused,
    Seeding,
    Completed,
    Error,
    Stopped
}

public partial class DownloadTask : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private DownloadType _type;
    [ObservableProperty] private string _name = "解析中...";
    [ObservableProperty] private string _source = "";
    [ObservableProperty] private string _savePath = "";
    [ObservableProperty] private long _totalBytes;
    [ObservableProperty] private long _downloadedBytes;
    [ObservableProperty] private long _downloadRate;
    [ObservableProperty] private long _uploadRate;
    [ObservableProperty] private int _peers;
    [ObservableProperty] private int _seeds;
    [ObservableProperty] private int _connections;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private DownloadStatus _status = DownloadStatus.Waiting;
    [ObservableProperty] private string _errorCode = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private DateTime _createdAt = DateTime.Now;

    // ---- 各类型任务的输入与配置（持久化） ----
    [ObservableProperty] private string _torrentPath = "";
    [ObservableProperty] private long _downloadLimitBytesPerSec;
    [ObservableProperty] private long _uploadLimitBytesPerSec;
    [ObservableProperty] private double _seedRatioLimit;
    [ObservableProperty] private int _seedTimeLimitMinutes;
    [ObservableProperty] private int _httpConnections = 1;
    [ObservableProperty] private int _m3u8QualityKbps;
    [ObservableProperty] private string _ffmpegPath = "";
    [ObservableProperty] private string _telegramToken = "";
    [ObservableProperty] private string _telegramFileId = "";
    [ObservableProperty] private string _telegramChatId = "";
    [ObservableProperty] private string _telegramMessageId = "";
    [ObservableProperty] private string _ftpHost = "";
    [ObservableProperty] private int _ftpPort = 21;
    [ObservableProperty] private string _ftpUser = "";
    [ObservableProperty] private string _ftpPassword = "";
    [ObservableProperty] private string _ftpRemotePath = "";
    [ObservableProperty] private bool _ftpUseTls;

    [JsonIgnore] public SynchronizationContext SyncContext { get; set; }
    [JsonIgnore] public CancellationTokenSource Cancellation { get; set; }

    public string TypeText => Type switch
    {
        DownloadType.BitTorrent => "BT",
        DownloadType.Http => "HTTP",
        DownloadType.M3u8 => "M3U8",
        DownloadType.Telegram => "TG",
        DownloadType.Ftp => "FTP",
        _ => "?"
    };

    public string StateText => Status switch
    {
        DownloadStatus.Waiting => "等待中",
        DownloadStatus.Preparing => "解析中",
        DownloadStatus.Downloading => "下载中",
        DownloadStatus.Paused => "已暂停",
        DownloadStatus.Seeding => "做种中",
        DownloadStatus.Completed => "已完成",
        DownloadStatus.Error => "失败",
        DownloadStatus.Stopped => "已停止",
        _ => Status.ToString()
    };

    public string SizeText => FormatBytes(TotalBytes);

    public string DownloadedText => TotalBytes > 0
        ? $"{FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)}"
        : FormatBytes(DownloadedBytes);

    public string DownloadSpeedText => Status is DownloadStatus.Downloading or DownloadStatus.Preparing or DownloadStatus.Seeding
        ? FormatRate(DownloadRate)
        : "-";

    public string UploadSpeedText => Status is DownloadStatus.Downloading or DownloadStatus.Seeding
        ? FormatRate(UploadRate)
        : "-";

    public string RemainingText
    {
        get
        {
            if (Status == DownloadStatus.Completed) return "已完成";
            if (Status == DownloadStatus.Seeding) return "做种中";
            if (Status == DownloadStatus.Paused) return "已暂停";
            if (Status == DownloadStatus.Error) return ErrorMessage;
            if (Status == DownloadStatus.Preparing) return "解析中...";
            if (Status == DownloadStatus.Stopped) return "已停止";
            var remaining = TotalBytes - DownloadedBytes;
            if (remaining <= 0) return "即将完成";
            if (DownloadRate <= 0) return "计算中...";
            var seconds = remaining / (double)DownloadRate;
            if (seconds < 60) return $"剩余 {seconds:F0} 秒";
            if (seconds < 3600) return $"剩余 {seconds / 60:F0} 分钟";
            if (seconds < 86400) return $"剩余 {seconds / 3600:F1} 小时";
            return $"剩余 {seconds / 86400:F1} 天";
        }
    }

    public string PeerText => Type switch
    {
        DownloadType.BitTorrent => $"种子 {Seeds} · 连接 {Peers} · 节点 {Connections}",
        DownloadType.M3u8 => Detail,
        DownloadType.Ftp => string.IsNullOrEmpty(Detail) ? "FTP 直连" : Detail,
        _ => Detail
    };

    public bool HasError => Status == DownloadStatus.Error && !string.IsNullOrWhiteSpace(ErrorMessage);

    public string ErrorText => HasError ? (string.IsNullOrWhiteSpace(ErrorCode) ? ErrorMessage : $"{ErrorCode}：{ErrorMessage}") : "";

    public bool CanPause => Status is DownloadStatus.Downloading or DownloadStatus.Preparing or DownloadStatus.Seeding;

    public bool CanResume => Status is DownloadStatus.Paused or DownloadStatus.Error or DownloadStatus.Stopped or DownloadStatus.Waiting;

    public void Apply(Action action)
    {
        if (action == null) return;
        var ctx = SyncContext;
        if (ctx != null && SynchronizationContext.Current != ctx)
            ctx.Post(_ => action(), null);
        else
            action();
    }

    public void RefreshDisplay()
    {
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(DownloadedText));
        OnPropertyChanged(nameof(DownloadSpeedText));
        OnPropertyChanged(nameof(UploadSpeedText));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(PeerText));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024:F1} MB";
        return $"{bytes / 1024.0 / 1024 / 1024:F2} GB";
    }

    public static string FormatRate(long bytesPerSecond)
    {
        if (bytesPerSecond <= 0) return "0 B/s";
        if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
        if (bytesPerSecond < 1024L * 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
        return $"{bytesPerSecond / 1024.0 / 1024:F1} MB/s";
    }
}
