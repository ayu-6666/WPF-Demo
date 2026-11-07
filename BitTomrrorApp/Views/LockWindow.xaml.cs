using System.Windows;
using System.Windows.Input;
using BitTomrrorApp.Services;

namespace BitTomrrorApp.Views
{
    public partial class LockWindow : Window
    {
        public LockWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => PasswordBox.Focus();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Unlock_Click(sender, e);
        }

        private void Unlock_Click(object sender, RoutedEventArgs e)
        {
            var settings = DownloadManager.Instance.Settings;
            if (SecureText.VerifyPassword(PasswordBox.Password, settings.LockPasswordHash, settings.LockPasswordSalt))
            {
                DialogResult = true;
                Close();
                return;
            }
            ErrorText.Text = "密码错误，请重试";
            ErrorText.Visibility = Visibility.Visible;
            PasswordBox.Clear();
            PasswordBox.Focus();
        }
    }
}
