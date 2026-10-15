using BitTomrrorApp.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BitTomrrorApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 启动一个 UI 刷新定时器：更新底部统计与空状态可见性
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        timer.Tick += RefreshStatusBar;
        timer.Start();
        RefreshStatusBar(this, EventArgs.Empty);
    }

    private void RefreshStatusBar(object sender, EventArgs e)
    {
        var items = TorrentEngineService.Instance.TorrentItems;
        TaskCount.Text = $"{items.Count} 个任务";
        long down = 0, up = 0;
        foreach (var it in items)
        {
            if (double.TryParse(new string(it.DownloadSpeed.Where(char.IsDigit).ToArray()), out var d)) down += (long)d * 1024;
            if (double.TryParse(new string(it.UploadSpeed.Where(char.IsDigit).ToArray()), out var u)) up += (long)u * 1024;
        }
        TotalDown.Text = FormatSpeed(down);
        TotalUp.Text = FormatSpeed(up);
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string FormatSpeed(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B/s";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB/s";
        return $"{bytes / 1024.0 / 1024:F1} MB/s";
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaxIcon.Kind = MahApps.Metro.IconPacks.PackIconMaterialKind.WindowMaximize;
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaxIcon.Kind = MahApps.Metro.IconPacks.PackIconMaterialKind.WindowRestore;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择 BT 种子文件",
            Filter = "种子文件 (*.torrent)|*.torrent|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
            InputBox.Text = dlg.FileName;
    }
}
