using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WxChatApp.Models;
using WxChatApp.Services;
using WxChatApp.ViewModels;

namespace WxChatApp.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm = new();
    private bool _entering;

    // 服务可用状态检测：进入登录界面即检测，此后每 5 秒轮询一次
    private static readonly HttpClient HealthClient = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _checking;

    public LoginWindow()
    {
        InitializeComponent();
        _vm.LoginSucceeded += OnLoginSucceeded;
        DataContext = _vm;

        _statusTimer.Tick += async (_, _) => await RefreshServerStatusAsync();
        Closed += (_, _) => _statusTimer.Stop();

        Loaded += async (_, _) =>
        {
            _statusTimer.Start();
            await RefreshServerStatusAsync();
            if (_vm.Account.Length == 0)
            {
                AccountBoxFocus();
            }
            else
            {
                PasswordBox.Focus();
            }
        };
    }

    private void AccountBoxFocus()
    {
        if (FindName("AccountTextBox") is FrameworkElement element)
        {
            element.Focus();
        }
    }

    private async void Submit_Click(object sender, RoutedEventArgs e) => await SubmitAsync();

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SubmitAsync();
        }
    }

    private async Task SubmitAsync()
    {
        await _vm.SubmitAsync(PasswordBox.Password);
    }

    private void Switch_Click(object sender, RoutedEventArgs e)
    {
        _vm.ToggleMode();
        PasswordBox.Focus();
    }

    private void OnLoginSucceeded(AuthResult auth)
    {
        if (_entering)
        {
            return;
        }
        _entering = true;
        IsEnabled = false;
        Cursor = Cursors.Wait;
        // 快速切换到主窗口（同步操作，瞬间完成），SignalR 连接和数据加载
        // 放到 MainWindow.InitializeAsync 里后台进行，用户不会看到灰色假死。
        var entered = App.EnterMain(auth, this);
        if (!entered)
        {
            _entering = false;
            IsEnabled = true;
            Cursor = Cursors.Arrow;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async void Status_Click(object sender, MouseButtonEventArgs e) => await RefreshServerStatusAsync();

    /// <summary>请求 /api/health，用绿/红灯表示服务端是否可用。</summary>
    private async Task RefreshServerStatusAsync()
    {
        if (_checking) return;
        _checking = true;
        try
        {
            using var resp = await HealthClient.GetAsync(AppConfig.ServerBaseUrl + "/api/health");
            ApplyStatus(resp.IsSuccessStatusCode);
        }
        catch
        {
            ApplyStatus(false);
        }
        finally
        {
            _checking = false;
        }
    }

    private void ApplyStatus(bool online)
    {
        var color = online ? Color.FromRgb(0x07, 0xC1, 0x60) : Color.FromRgb(0xE6, 0x43, 0x40);
        StatusLight.Fill = new SolidColorBrush(color);
        StatusText.Text = online ? Loc.T("Login.StatusOnline") : Loc.T("Login.StatusOffline");
        StatusText.Foreground = new SolidColorBrush(online ? Color.FromRgb(0x80, 0x80, 0x80) : color);
        StatusLight.ToolTip = Loc.T("Login.StatusToolTip", AppConfig.ServerBaseUrl, DateTime.Now.ToString("HH:mm:ss"));
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
