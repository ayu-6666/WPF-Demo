using BitTomrrorApp.Services;
using System.Windows;

namespace BitTomrrorApp
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            await DownloadManager.Instance.InitializeAsync(Dispatcher);

            // 启动密码锁：启用了启动密码时先弹出解锁窗口，取消解锁则退出应用
            if (DownloadManager.Instance.IsLocked)
            {
                var lockWindow = new Views.LockWindow();
                if (lockWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { DownloadManager.Instance.ShutdownAsync().Wait(TimeSpan.FromSeconds(8)); } catch { }
            base.OnExit(e);
        }
    }
}
