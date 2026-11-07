using System.IO;
using System.Windows;
using WxChatApp.Helpers;
using WxChatApp.Models;
using WxChatApp.Services;
using WxChatApp.Views;

namespace WxChatApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                WriteCrashLog("AppDomain.UnhandledException", ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrashLog("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Loc.Initialize();
        ShowLogin();
    }

    private static void WriteCrashLog(string source, Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WxChatApp");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "crash.log");
            var lines = new List<string>
            {
                "=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + source + " ===",
                ex.ToString(),
                "",
            };
            File.AppendAllLines(path, lines);
        }
        catch
        {
            // 日志写不进去就算了，不要二次抛
        }
    }

    public static void ShowLogin()
    {
        var login = new LoginWindow();
        Current.MainWindow = login;
        login.Show();
        login.Activate();
        login.Closed += (_, _) =>
        {
            if (!Current.Windows.OfType<MainWindow>().Any() &&
                !Current.Windows.OfType<LoginWindow>().Any())
            {
                Current.Shutdown();
            }
        };
    }

    /// <summary>
    /// 登录成功的快速进入：先 Show 主窗口、关闭登录窗口（避免灰色假死），
    /// SignalR 连接和数据加载放到 MainWindow.InitializeAsync 里后台完成。
    /// </summary>
    public static bool EnterMain(AuthResult auth, Window? loginWindow)
    {
        AppServices.Api.SetToken(auth.Token);
        AppServices.Token = auth.Token;
        AppServices.CurrentUser = auth.User;

        try
        {
            var main = new MainWindow();
            Current.MainWindow = main;
            main.Show();
            loginWindow?.Close();
            return true;
        }
        catch (Exception ex)
        {
            WriteCrashLog("EnterMain.ShowMain", ex);
            Dialogs.Error(loginWindow, Loc.T("App.MainStartFail", ex.Message), Loc.T("App.StartFailTitle"));
            if (loginWindow != null) loginWindow.IsEnabled = true;
            return false;
        }
    }

    /// <summary>退出登录：先打开登录窗口，再关闭主窗口，避免触发应用退出。</summary>
    public static void ReturnToLogin(MainWindow main)
    {
        ShowLogin();
        main.Close();
    }
}
