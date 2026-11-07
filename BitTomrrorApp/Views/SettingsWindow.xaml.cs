using System.Windows;
using System.Windows.Input;
using BitTomrrorApp.Models;
using BitTomrrorApp.Services;

namespace BitTomrrorApp.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;

        public SettingsWindow()
        {
            InitializeComponent();
            _settings = DownloadManager.Instance.Settings;
            LoadValues();
        }

        private void LoadValues()
        {
            FtpHost.Text = _settings.FtpHost;
            FtpPort.Text = _settings.FtpPort > 0 ? _settings.FtpPort.ToString() : "21";
            FtpUser.Text = _settings.FtpUser;
            FtpPassword.Password = _settings.FtpPassword;
            FtpRemotePath.Text = _settings.FtpRemotePath;
            FtpTls.IsChecked = _settings.FtpUseTls;

            TgToken.Text = _settings.TelegramToken;
            TgChatId.Text = _settings.TelegramChatId;

            LockStatus.Text = DownloadManager.Instance.IsLocked ? "已启用启动密码" : "未启用启动密码";
            LockStatus.Foreground = DownloadManager.Instance.IsLocked
                ? System.Windows.Media.Brushes.ForestGreen
                : System.Windows.Media.Brushes.Gray;
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

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var port = 21;
            int.TryParse(FtpPort.Text.Trim(), out port);
            _settings.FtpHost = FtpHost.Text.Trim();
            _settings.FtpPort = Math.Clamp(port, 1, 65535);
            _settings.FtpUser = FtpUser.Text.Trim();
            _settings.FtpPassword = FtpPassword.Password;
            _settings.FtpRemotePath = FtpRemotePath.Text.Trim();
            _settings.FtpUseTls = FtpTls.IsChecked == true;

            _settings.TelegramToken = TgToken.Text.Trim();
            _settings.TelegramChatId = TgChatId.Text.Trim();

            DownloadManager.Instance.SaveSettingsNow();
            StatusText.Text = "设置已保存（敏感凭据已加密落盘）";
        }

        private void SetLock_Click(object sender, RoutedEventArgs e)
        {
            var newPassword = LockNewPwd.Password;
            var confirmPassword = LockConfirmPwd.Password;
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                StatusText.Text = "请输入新启动密码";
                StatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
                return;
            }
            if (newPassword != confirmPassword)
            {
                StatusText.Text = "两次输入的密码不一致";
                StatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
                return;
            }
            if (DownloadManager.Instance.IsLocked)
            {
                var current = LockCurrentPwd.Password;
                if (string.IsNullOrEmpty(current) ||
                    !SecureText.VerifyPassword(current, _settings.LockPasswordHash, _settings.LockPasswordSalt))
                {
                    StatusText.Text = "当前启动密码校验失败，无法修改";
                    StatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
                    return;
                }
            }

            var (hash, salt) = SecureText.HashPassword(newPassword);
            _settings.LockPasswordHash = hash;
            _settings.LockPasswordSalt = salt;
            DownloadManager.Instance.SaveSettingsNow();
            LockNewPwd.Clear();
            LockConfirmPwd.Clear();
            LockCurrentPwd.Clear();
            LoadValues();
            StatusText.Text = "启动密码已保存，下次启动应用时生效";
            StatusText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        }

        private void ClearLock_Click(object sender, RoutedEventArgs e)
        {
            if (!DownloadManager.Instance.IsLocked)
            {
                StatusText.Text = "当前未启用启动密码";
                StatusText.Foreground = System.Windows.Media.Brushes.Gray;
                return;
            }
            var current = LockCurrentPwd.Password;
            if (string.IsNullOrEmpty(current) ||
                !SecureText.VerifyPassword(current, _settings.LockPasswordHash, _settings.LockPasswordSalt))
            {
                StatusText.Text = "当前启动密码校验失败，无法清除";
                StatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
                return;
            }
            _settings.LockPasswordHash = "";
            _settings.LockPasswordSalt = "";
            DownloadManager.Instance.SaveSettingsNow();
            LockCurrentPwd.Clear();
            LoadValues();
            StatusText.Text = "启动密码已清除";
            StatusText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        }

        private void ClearCredentials_Click(object sender, RoutedEventArgs e)
        {
            _settings.FtpPassword = "";
            _settings.TelegramToken = "";
            DownloadManager.Instance.SaveSettingsNow();
            FtpPassword.Clear();
            TgToken.Clear();
            StatusText.Text = "已清除 FTP 密码与 Telegram Token";
            StatusText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        }
    }
}
