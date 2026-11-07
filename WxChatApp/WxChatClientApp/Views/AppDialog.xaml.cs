using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WxChatApp.Services;

namespace WxChatApp.Views;

public enum DialogKind
{
    Info,
    Success,
    Warning,
    Error,
    Confirm
}

/// <summary>
/// 统一风格的自定义弹框：遮罩 + 图标 + 标题 + 正文 + 按钮，替代系统 MessageBox。
/// Info/Warning/Error 只有一个“知道了”按钮；Confirm 提供“取消 / 确定”。
/// Enter 触发主按钮，Esc 触发取消（信息类弹框等同于关闭）。
/// </summary>
public partial class AppDialog : Window
{
    /// <summary>Confirm 时表示用户点击了“确定”；其余类型恒为 true。</summary>
    public bool Result { get; private set; }

    public AppDialog(DialogKind kind, string title, string message, Window? owner, string? okText = null, string? cancelText = null)
    {
        InitializeComponent();
        if (owner != null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        switch (kind)
        {
            case DialogKind.Confirm:
                CancelButton.Visibility = Visibility.Visible;
                OkButton.Content = string.IsNullOrEmpty(okText) ? Loc.T("Common.Ok") : okText;
                CancelButton.Content = string.IsNullOrEmpty(cancelText) ? Loc.T("Common.Cancel") : cancelText;
                break;
        }

        ApplyIcon(kind);

        if (kind != DialogKind.Confirm)
        {
            OkButton.Content = string.IsNullOrEmpty(okText) ? Loc.T("Common.Know") : okText;
        }

        Loaded += (_, _) =>
        {
            FitOwner();
            OkButton.Focus();
        };
    }

    /// <summary>
    /// 统一线性图标：浅色圆底 + 品牌色描边，避免出现“安全软件”式的实心警示符号。
    /// info=圆底 i，success=圆底对勾，warning=圆底感叹号，error=圆底叉，confirm=圆底问号。
    /// </summary>
    private void ApplyIcon(DialogKind kind)
    {
        Color tint;
        Color stroke;
        string? glyph;
        bool showDot = false;
        bool showQuestion = false;
        double dotTop = 0;

        switch (kind)
        {
            case DialogKind.Success:
                tint = Color.FromRgb(0xE8, 0xF8, 0xEE);
                stroke = Color.FromRgb(0x07, 0xC1, 0x60);
                glyph = "M13,20.5 L18,25.5 L27,15.5";
                break;
            case DialogKind.Warning:
                tint = Color.FromRgb(0xFF, 0xF4, 0xE5);
                stroke = Color.FromRgb(0xE0, 0x93, 0x2F);
                glyph = "M20,11.5 L20,23.5";
                showDot = true;
                dotTop = 27;
                break;
            case DialogKind.Error:
                tint = Color.FromRgb(0xFD, 0xEC, 0xEC);
                stroke = Color.FromRgb(0xE6, 0x43, 0x40);
                glyph = "M14,14 L26,26 M26,14 L14,26";
                break;
            case DialogKind.Confirm:
                tint = Color.FromRgb(0xEA, 0xF2, 0xFF);
                stroke = Color.FromRgb(0x2B, 0x6C, 0xF0);
                glyph = null;
                showQuestion = true;
                break;
            default:
                tint = Color.FromRgb(0xEA, 0xF2, 0xFF);
                stroke = Color.FromRgb(0x2B, 0x6C, 0xF0);
                glyph = "M20,17.5 L20,27.5";
                showDot = true;
                dotTop = 12.5;
                break;
        }

        IconBorder.Background = new SolidColorBrush(tint);
        IconRing.Stroke = new SolidColorBrush(stroke);
        IconGlyph.Stroke = new SolidColorBrush(stroke);
        IconGlyph.Data = glyph == null ? Geometry.Empty : Geometry.Parse(glyph);
        IconDot.Fill = new SolidColorBrush(stroke);
        IconDot.Visibility = showDot ? Visibility.Visible : Visibility.Collapsed;
        IconDot.VerticalAlignment = VerticalAlignment.Top;
        IconDot.Margin = new Thickness(0, dotTop, 0, 0);
        IconText.Visibility = showQuestion ? Visibility.Visible : Visibility.Collapsed;
        IconText.Foreground = new SolidColorBrush(stroke);
    }

    private void FitOwner()
    {
        if (Owner == null)
        {
            return;
        }
        Left = Owner.Left;
        Top = Owner.Top;
        Width = Owner.Width;
        Height = Owner.Height;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Result = false;
            Close();
        }
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 点击遮罩不关闭，保持阻断语义；仅吞掉事件避免穿透到主窗口
        e.Handled = true;
    }
}
