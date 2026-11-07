using System.IO;
using System.Windows;
using System.Windows.Input;
using BitTomrrorApp.Models;
using BitTomrrorApp.Services;
using Microsoft.Win32;

namespace BitTomrrorApp.Views
{
    public partial class AddTaskWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly FfmpegLocator _ffmpeg;

        public DownloadTask ResultTask { get; private set; }

        public AddTaskWindow(AppSettings settings, FfmpegLocator ffmpeg)
        {
            InitializeComponent();
            _settings = settings;
            _ffmpeg = ffmpeg;

            BtSavePath.Text = settings.DefaultSavePath;
            HttpSavePath.Text = settings.DefaultSavePath;
            M3u8SavePath.Text = settings.DefaultSavePath;
            TgSavePath.Text = settings.DefaultSavePath;
            FtpSavePath.Text = settings.DefaultSavePath;
            FfmpegPathBox.Text = settings.FfmpegPath;
            FfmpegStatus.Text = ffmpeg.StatusText + (string.IsNullOrEmpty(ffmpeg.Path) ? "" : $"　路径：{ffmpeg.Path}");

            // 个人设置中心：自动填充已保存的 Telegram 认证信息
            TgToken.Text = settings.TelegramToken;
            TgChatId.Text = settings.TelegramChatId;

            // 个人设置中心：自动填充已保存的 FTP 连接信息
            FtpHost.Text = settings.FtpHost;
            FtpPort.Text = settings.FtpPort > 0 ? settings.FtpPort.ToString() : "21";
            FtpUser.Text = settings.FtpUser;
            FtpPassword.Password = settings.FtpPassword;
            FtpRemotePath.Text = settings.FtpRemotePath;
            FtpTls.IsChecked = settings.FtpUseTls;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BrowseTorrent_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择 BT 种子文件",
                Filter = "种子文件 (*.torrent)|*.torrent|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() == true)
                BtSource.Text = dlg.FileName;
        }

        private void BrowseBtFolder_Click(object sender, RoutedEventArgs e) => BrowseFolder(BtSavePath);
        private void BrowseHttpFolder_Click(object sender, RoutedEventArgs e) => BrowseFolder(HttpSavePath);
        private void BrowseM3u8Folder_Click(object sender, RoutedEventArgs e) => BrowseFolder(M3u8SavePath);
        private void BrowseTgFolder_Click(object sender, RoutedEventArgs e) => BrowseFolder(TgSavePath);
        private void BrowseFtpFolder_Click(object sender, RoutedEventArgs e) => BrowseFolder(FtpSavePath);

        private static void BrowseFolder(System.Windows.Controls.TextBox target)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "选择保存目录",
                InitialDirectory = Directory.Exists(target.Text) ? target.Text : ""
            };
            if (dlg.ShowDialog() == true)
                target.Text = dlg.FolderName;
        }

        private void BrowseFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择 ffmpeg.exe",
                Filter = "ffmpeg (ffmpeg.exe)|ffmpeg.exe|可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() == true)
                FfmpegPathBox.Text = dlg.FileName;
        }

        private async void CheckFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            FfmpegStatus.Text = "检测中...";
            await _ffmpeg.CheckAsync(FfmpegPathBox.Text);
            FfmpegStatus.Text = _ffmpeg.StatusText + (string.IsNullOrEmpty(_ffmpeg.Path) ? "" : $"　路径：{_ffmpeg.Path}");
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            ValidationText.Text = "";
            var selected = TypeTabs.SelectedIndex;
            DownloadTask task;
            switch (selected)
            {
                case 0: task = BuildBtTask(); break;
                case 1: task = BuildHttpTask(); break;
                case 2: task = BuildM3u8Task(); break;
                case 3: task = BuildTelegramTask(); break;
                case 4: task = BuildFtpTask(); break;
                default: return;
            }

            if (task == null) return;
            if (string.IsNullOrWhiteSpace(task.SavePath))
            {
                ValidationText.Text = "请填写保存目录";
                return;
            }
            try
            {
                Directory.CreateDirectory(task.SavePath);
            }
            catch (Exception ex)
            {
                ValidationText.Text = "保存目录不可用：" + ex.Message;
                return;
            }

            task.FfmpegPath = FfmpegPathBox.Text.Trim();
            ResultTask = task;
            DialogResult = true;
            Close();
        }

        private DownloadTask BuildBtTask()
        {
            var source = BtSource.Text.Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                ValidationText.Text = "请填写磁力链接或选择 .torrent 文件";
                return null;
            }

            var task = new DownloadTask
            {
                Type = DownloadType.BitTorrent,
                Source = source,
                SavePath = BtSavePath.Text.Trim(),
                DownloadLimitBytesPerSec = ParseLong(BtDownLimit.Text) * 1024,
                UploadLimitBytesPerSec = ParseLong(BtUpLimit.Text) * 1024,
                SeedRatioLimit = ParseDouble(BtSeedRatio.Text),
                SeedTimeLimitMinutes = (int)ParseLong(BtSeedMinutes.Text)
            };

            if (source.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                task.Name = "磁力任务";
            }
            else if (File.Exists(source))
            {
                task.TorrentPath = source;
                task.Name = Path.GetFileNameWithoutExtension(source);
            }
            else
            {
                ValidationText.Text = "种子文件不存在：" + source;
                return null;
            }
            return task;
        }

        private DownloadTask BuildHttpTask()
        {
            var url = HttpUrl.Text.Trim();
            if (!IsHttpUrl(url))
            {
                ValidationText.Text = "请填写有效的 http(s) 直链";
                return null;
            }
            return new DownloadTask
            {
                Type = DownloadType.Http,
                Source = url,
                SavePath = HttpSavePath.Text.Trim(),
                Name = HttpDownloadEngine.GuessFileName(url),
                HttpConnections = (int)Math.Clamp(ParseLong(HttpConnections.Text), 1, 8)
            };
        }

        private DownloadTask BuildM3u8Task()
        {
            var url = M3u8Url.Text.Trim();
            if (!IsHttpUrl(url))
            {
                ValidationText.Text = "请填写有效的 M3U8 地址";
                return null;
            }
            return new DownloadTask
            {
                Type = DownloadType.M3u8,
                Source = url,
                SavePath = M3u8SavePath.Text.Trim(),
                Name = "HLS 任务",
                M3u8QualityKbps = (int)Math.Max(0, ParseLong(M3u8Quality.Text)),
                FfmpegPath = FfmpegPathBox.Text.Trim()
            };
        }

        private DownloadTask BuildTelegramTask()
        {
            var token = TgToken.Text.Trim();
            var fileId = TgFileId.Text.Trim();
            var chatId = TgChatId.Text.Trim();
            var messageId = TgMessageId.Text.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                ValidationText.Text = "请填写 Telegram Bot Token";
                return null;
            }
            if (string.IsNullOrWhiteSpace(fileId) && (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(messageId)))
            {
                ValidationText.Text = "请填写 file_id，或同时填写 chat_id 与 message_id";
                return null;
            }
            return new DownloadTask
            {
                Type = DownloadType.Telegram,
                Source = string.IsNullOrWhiteSpace(fileId) ? $"chat {chatId} / message {messageId}" : $"file_id {fileId}",
                SavePath = TgSavePath.Text.Trim(),
                Name = "Telegram 文件",
                TelegramToken = token,
                TelegramFileId = fileId,
                TelegramChatId = chatId,
                TelegramMessageId = messageId
            };
        }

        private DownloadTask BuildFtpTask()
        {
            var host = FtpHost.Text.Trim();
            var remote = FtpRemotePath.Text.Trim();
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(remote))
            {
                ValidationText.Text = "请填写 FTP 主机与远程文件路径";
                return null;
            }
            var port = (int)Math.Clamp(ParseLong(FtpPort.Text), 1, 65535);
            return new DownloadTask
            {
                Type = DownloadType.Ftp,
                Source = $"ftp://{host}:{port}/{remote.TrimStart('/')}",
                SavePath = FtpSavePath.Text.Trim(),
                Name = Path.GetFileName(remote),
                FtpHost = host,
                FtpPort = port,
                FtpUser = FtpUser.Text.Trim(),
                FtpPassword = FtpPassword.Password,
                FtpRemotePath = remote,
                FtpUseTls = FtpTls.IsChecked == true
            };
        }

        private static bool IsHttpUrl(string url) =>
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        private static long ParseLong(string text)
        {
            long.TryParse(text.Trim(), out var value);
            return value;
        }

        private static double ParseDouble(string text)
        {
            double.TryParse(text.Trim(), out var value);
            return value;
        }
    }
}
