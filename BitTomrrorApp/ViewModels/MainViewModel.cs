using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BitTomrrorApp.Models;
using BitTomrrorApp.Services;
using BitTomrrorApp.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitTomrrorApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public DownloadManager Managed { get; } = DownloadManager.Instance;
        public ObservableCollection<DownloadTask> Tasks => Managed.Tasks;

        [RelayCommand]
        private async Task AddTorrentAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("请先粘贴磁力链接、资源地址或 .torrent 文件路径", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var task = BuildQuickTask(input.Trim());
            if (task == null) return;

            var ok = await Managed.AddTaskAsync(task);
            if (!ok)
            {
                MessageBox.Show("该资源已在任务列表中，请勿重复添加", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show("任务已添加，正在连接资源...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private DownloadTask BuildQuickTask(string input)
        {
            var settings = Managed.Settings;
            var task = new DownloadTask
            {
                SavePath = settings.DefaultSavePath,
                DownloadLimitBytesPerSec = settings.DefaultDownloadLimitBytesPerSec,
                UploadLimitBytesPerSec = settings.DefaultUploadLimitBytesPerSec,
                SeedRatioLimit = settings.DefaultSeedRatioLimit,
                SeedTimeLimitMinutes = settings.DefaultSeedTimeLimitMinutes,
                FfmpegPath = settings.FfmpegPath
            };

            if (File.Exists(input) && input.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                task.Type = DownloadType.BitTorrent;
                task.Source = input;
                task.TorrentPath = input;
                task.Name = Path.GetFileNameWithoutExtension(input);
                return task;
            }

            if (input.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                task.Type = DownloadType.BitTorrent;
                task.Source = input;
                task.Name = "磁力任务";
                return task;
            }

            if (input.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            {
                // 设置中心已保存 FTP 连接信息时，用保存的凭据 + URL 中的主机/路径直接建任务
                var hasCredentials = !string.IsNullOrWhiteSpace(settings.FtpUser) || !string.IsNullOrWhiteSpace(settings.FtpPassword);
                if (!hasCredentials)
                {
                    MessageBox.Show("FTP 下载需要账号信息：请先在「个人设置中心 → FTP 服务器」保存连接信息，或点击「高级添加」手动填写。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return null;
                }
                if (!TryParseFtpUrl(input, out var ftpHost, out var ftpPort, out var ftpRemote))
                {
                    MessageBox.Show("FTP 地址无法解析，示例：ftp://ftp.example.com:21/pub/file.zip",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                task.Type = DownloadType.Ftp;
                task.Source = input;
                task.Name = Path.GetFileName(ftpRemote);
                task.FtpHost = ftpHost;
                task.FtpPort = ftpPort;
                task.FtpRemotePath = ftpRemote;
                task.FtpUser = settings.FtpUser;
                task.FtpPassword = settings.FtpPassword;
                task.FtpUseTls = settings.FtpUseTls;
                return task;
            }

            if (input.Contains("t.me/", StringComparison.OrdinalIgnoreCase) || input.StartsWith("tg:", StringComparison.OrdinalIgnoreCase))
            {
                var tokenHint = string.IsNullOrWhiteSpace(settings.TelegramToken)
                    ? "可先在「个人设置中心 → Telegram 认证」保存 Bot Token，新建任务时自动填充。"
                    : "Bot Token 已从设置中心自动填充，只需补充 file_id。";
                MessageBox.Show("Telegram 下载需要定位文件：" + tokenHint + "请点击「高级添加」完成填写。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            if (input.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
            {
                task.Type = DownloadType.M3u8;
                task.Source = input;
                task.Name = "HLS 任务";
                return task;
            }

            if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                task.Type = DownloadType.Http;
                task.Source = input;
                task.Name = HttpDownloadEngine.GuessFileName(input);
                task.HttpConnections = 1;
                return task;
            }

            MessageBox.Show("无法识别该资源类型。支持：.torrent / magnet / http(s) / m3u8 / ftp / Telegram（高级添加）。",
                "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        private static bool TryParseFtpUrl(string url, out string host, out int port, out string remote)
        {
            host = "";
            port = 21;
            remote = "";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeFtp)
                return false;
            host = uri.Host;
            port = uri.Port > 0 ? uri.Port : 21;
            remote = uri.AbsolutePath;
            return !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(remote) && remote != "/";
        }

        public async Task AddTorrentFileAsync(string path)
        {
            var task = new DownloadTask
            {
                Type = DownloadType.BitTorrent,
                Source = path,
                TorrentPath = path,
                SavePath = Managed.Settings.DefaultSavePath,
                Name = Path.GetFileNameWithoutExtension(path),
                DownloadLimitBytesPerSec = Managed.Settings.DefaultDownloadLimitBytesPerSec,
                UploadLimitBytesPerSec = Managed.Settings.DefaultUploadLimitBytesPerSec,
                SeedRatioLimit = Managed.Settings.DefaultSeedRatioLimit,
                SeedTimeLimitMinutes = Managed.Settings.DefaultSeedTimeLimitMinutes
            };
            if (!await Managed.AddTaskAsync(task))
                MessageBox.Show("该种子已在任务列表中", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        [RelayCommand]
        private async Task AddAdvancedAsync()
        {
            var dialog = new AddTaskWindow(Managed.Settings, Managed.Ffmpeg)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (dialog.ShowDialog() == true && dialog.ResultTask != null)
            {
                if (!await Managed.AddTaskAsync(dialog.ResultTask))
                    MessageBox.Show("该资源已在任务列表中，请勿重复添加", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (!string.IsNullOrWhiteSpace(dialog.ResultTask.SavePath))
                    Managed.UpdateDefaultSavePath(dialog.ResultTask.SavePath);
                if (!string.IsNullOrWhiteSpace(dialog.ResultTask.FfmpegPath))
                    await Managed.UpdateFfmpegPathAsync(dialog.ResultTask.FfmpegPath);
            }
        }

        [RelayCommand]
        private async Task LoadedAsync()
        {
            // 历史任务由 DownloadManager 在应用启动时从 tasks.json 恢复，不再生成任何演示任务
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task PauseAsync(DownloadTask item)
        {
            if (item == null) return;
            if (item.CanResume)
                await Managed.ResumeAsync(item);
            else if (item.CanPause)
                await Managed.PauseAsync(item);
        }

        [RelayCommand]
        private async Task DeleteAsync(DownloadTask item)
        {
            if (item == null) return;
            var result = MessageBox.Show($"确定要删除「{item.Name}」吗？\n已下载的文件不会被删除。", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                await Managed.DeleteAsync(item);
        }

        [RelayCommand]
        private async Task BatchPauseAsync(IList items)
        {
            var list = items?.OfType<DownloadTask>().ToList();
            if (list == null || !list.Any()) return;
            foreach (var item in list)
            {
                if (item.CanPause) await Managed.PauseAsync(item);
            }
        }

        [RelayCommand]
        private async Task BatchDeleteAsync(IList items)
        {
            var list = items?.OfType<DownloadTask>().ToList();
            if (list == null || !list.Any()) return;
            var result = MessageBox.Show($"确定要删除选中的 {list.Count} 个任务吗？\n已下载的文件不会被删除。", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                foreach (var item in list)
                    await Managed.DeleteAsync(item);
            }
        }

        [RelayCommand]
        private void OpenFolder(DownloadTask item)
        {
            var path = item?.SavePath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
