using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace CPM.Recipe.Tool
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 全局异常捕获 —— 避免 WinExe 静默退出
            DispatcherUnhandledException += (s, args) =>
            {
                LogFatal("DispatcherUnhandledException", args.Exception);
                args.Handled = true;
                try
                {
                    MessageBox.Show(args.Exception.ToString(),
                        "未处理异常", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
                Shutdown(-1);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogFatal("AppDomain.UnhandledException", ex);
            };
        }

        private static void LogFatal(string source, Exception ex)
        {
            try
            {
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                var sb = new StringBuilder();
                sb.AppendLine($"[Crash] source={source}  time={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine(ex?.ToString() ?? "(null exception)");
                File.WriteAllText(file, sb.ToString());
            }
            catch { /* best-effort */ }
        }
    }
}
