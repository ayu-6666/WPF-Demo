using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WpfDevDemo
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log("Dispatcher", e.Exception);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log("AppDomain", e.ExceptionObject as Exception);
        }

        private static void Log(string source, Exception ex)
        {
            try
            {
                var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(file,
                    $"[{DateTime.Now:HH:mm:ss}] {source}: {ex}\r\n{ex?.StackTrace}\r\n\r\n");
            }
            catch
            {
                // ignore
            }
        }
    }
}
