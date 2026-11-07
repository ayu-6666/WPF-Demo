using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CPM.Recipe.Tool.Services;

namespace CPM.Recipe.Tool
{
    /// <summary>
    /// 项目风格统一弹框：图标 + 标题 + 正文 + 按钮，覆盖 Owner 的半透明遮罩，居中显示，Enter/Esc 可用。
    /// 用于替代系统 MessageBox，保证深/浅色主题一致。
    /// </summary>
    public partial class MessageDialog : Window
    {
        private enum DialogKind { Info, Warning, Error, Confirm }

        /// <summary>用户选择结果（Yes / No / OK / Cancel）。</summary>
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private MessageDialog(Window owner, string title, string message, DialogKind kind, MessageBoxButton buttons)
        {
            InitializeComponent();

            Title = title ?? "";
            TitleText.Text = Title;
            BodyText.Text = message ?? "";

            ApplyKind(kind);
            BuildButtons(buttons);

            if (owner != null && owner.IsVisible)
            {
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Width = Math.Max(420, owner.ActualWidth);
                Height = Math.Max(240, owner.ActualHeight);
            }
            else
            {
                Width = 480;
                Height = 300;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            Loaded += (s, e) => { if (DefaultButton != null) DefaultButton.Focus(); };
        }

        private Button DefaultButton { get; set; }

        private void ApplyKind(DialogKind kind)
        {
            switch (kind)
            {
                case DialogKind.Warning:
                    IconText.Text = "⚠";
                    IconText.Foreground = BrushOf("TextMain");
                    IconBadge.Background = BrushOf("WarningText");
                    break;
                case DialogKind.Error:
                    IconText.Text = "✖";
                    IconText.Foreground = BrushOf("TextMain");
                    IconBadge.Background = BrushOf("ErrorText");
                    break;
                case DialogKind.Confirm:
                    IconText.Text = "?";
                    IconText.Foreground = Brushes.White;
                    IconBadge.Background = BrushOf("AccentGradient");
                    break;
                default:
                    IconText.Text = "ℹ";
                    IconText.Foreground = Brushes.White;
                    IconBadge.Background = BrushOf("AccentGradient");
                    break;
            }
        }

        private void BuildButtons(MessageBoxButton buttons)
        {
            switch (buttons)
            {
                case MessageBoxButton.YesNo:
                    AddButton(L.T("btn.no"), MessageBoxResult.No, false, true);
                    AddButton(L.T("btn.yes"), MessageBoxResult.Yes, true, false);
                    break;
                case MessageBoxButton.YesNoCancel:
                    AddButton(L.T("btn.cancel"), MessageBoxResult.Cancel, false, false);
                    AddButton(L.T("btn.no"), MessageBoxResult.No, false, true);
                    AddButton(L.T("btn.yes"), MessageBoxResult.Yes, true, false);
                    break;
                default:
                    AddButton(L.T("btn.ok"), MessageBoxResult.OK, true, false);
                    break;
            }
        }

        private void AddButton(string text, MessageBoxResult result, bool primary, bool isCancel)
        {
            var btn = new Button
            {
                Content = text,
                Width = 104,
                Height = 36,
                Margin = new Thickness(10, 0, 0, 0),
                Style = (Style)Application.Current.TryFindResource(primary ? "GradientBtn" : "OutlineBtn"),
                IsDefault = primary,
                IsCancel = isCancel
            };
            btn.Click += (s, e) =>
            {
                Result = result;
                DialogResult = true;
            };
            if (primary) DefaultButton = btn;
            ButtonsPanel.Children.Add(btn);
        }

        private static Brush BrushOf(string key)
        {
            return Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
        }

        // ================= 静态入口 =================
        public static void Info(Window owner, string message, string title = null)
            => Show(owner, message, title ?? L.T("msg.title.info"), DialogKind.Info, MessageBoxButton.OK);

        public static void Warning(Window owner, string message, string title = null)
            => Show(owner, message, title ?? L.T("msg.title.warning"), DialogKind.Warning, MessageBoxButton.OK);

        public static void Error(Window owner, string message, string title = null)
            => Show(owner, message, title ?? L.T("msg.title.error"), DialogKind.Error, MessageBoxButton.OK);

        /// <summary>确认框：返回 true 表示用户点了“是”。</summary>
        public static bool Confirm(Window owner, string message, string title = null)
            => Show(owner, message, title ?? L.T("msg.title.confirm"), DialogKind.Confirm, MessageBoxButton.YesNo)
               == MessageBoxResult.Yes;

        /// <summary>三键确认框（是 / 否 / 取消）。</summary>
        public static MessageBoxResult Ask(Window owner, string message, string title = null)
            => Show(owner, message, title ?? L.T("msg.title.unsaved"), DialogKind.Confirm, MessageBoxButton.YesNoCancel);

        private static MessageBoxResult Show(Window owner, string message, string title, DialogKind kind, MessageBoxButton buttons)
        {
            if (owner == null) owner = Application.Current?.MainWindow;
            var dlg = new MessageDialog(owner, title, message, kind, buttons);
            dlg.ShowDialog();
            return dlg.Result;
        }
    }
}
