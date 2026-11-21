using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaStudioWpf48.Services;

namespace MediaStudioWpf48.Views
{
    public enum MessageDialogKind
    {
        Success,
        Warning,
        Error
    }

    public partial class MessageDialog : Window
    {
        private MessageDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 显示自定义消息弹框：成功（绿色勾）、警告（黄色三角）、错误（红色 X）。
        /// </summary>
        public static void ShowMessage(MessageDialogKind kind, string message, string title = null)
        {
            var dialog = new MessageDialog();
            dialog.Apply(kind, message, title);

            Window owner = Application.Current == null ? null : Application.Current.MainWindow;
            if (owner != null && owner.IsVisible && !ReferenceEquals(owner, dialog))
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            dialog.ShowDialog();
        }

        private void Apply(MessageDialogKind kind, string message, string title)
        {
            MessageText.Text = message ?? "";

            string colorKey;
            string glyph;
            string defaultTitle;
            switch (kind)
            {
                case MessageDialogKind.Success:
                    colorKey = "Success";
                    glyph = "\uE73E";
                    defaultTitle = LocalizationManager.Instance["DialogSuccessTitle"];
                    break;
                case MessageDialogKind.Warning:
                    colorKey = "Warning";
                    glyph = "\uE7BA";
                    defaultTitle = LocalizationManager.Instance["DialogWarningTitle"];
                    break;
                default:
                    colorKey = "Danger";
                    glyph = "\uE711";
                    defaultTitle = LocalizationManager.Instance["ErrorTitle"];
                    break;
            }

            var accent = (SolidColorBrush)(Application.Current.TryFindResource(colorKey) ?? Brushes.Gray);
            IconGlyph.Text = glyph;
            IconGlyph.Foreground = accent;
            IconBadge.Background = new SolidColorBrush(accent.Color) { Opacity = 0.14 };
            TitleText.Text = string.IsNullOrEmpty(title) ? defaultTitle : title;
        }

        private void Ok_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
