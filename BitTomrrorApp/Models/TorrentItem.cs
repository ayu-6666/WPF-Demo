using CommunityToolkit.Mvvm.ComponentModel;

namespace BitTomrrorApp.Models;

// 下载状态枚举（不再依赖 MonoTorrent，便于 UI 演示）
public enum TorrentState
{
    Waiting,
    Downloading,
    Paused,
    Seeding,
    Completed,
    Error
}

public partial class TorrentItem : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _hash;
    [ObservableProperty] private long _totalSize;
    [ObservableProperty] private string _savePath;

    [ObservableProperty] private TorrentState _state;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _downloadSpeed;
    [ObservableProperty] private string _uploadSpeed;
    [ObservableProperty] private string _remainingTime;
    [ObservableProperty] private string _errorMessage;

    public string SizeDisplay => TotalSize switch
    {
        > 1024L * 1024 * 1024 => $"{TotalSize / 1024.0 / 1024 / 1024:F2} GB",
        > 1024 * 1024 => $"{TotalSize / 1024.0 / 1024:F2} MB",
        _ => $"{TotalSize / 1024.0:F2} KB"
    };
}
