using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace MediaStudioWpf48
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            try
            {
                var window = new MainWindow();
                MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                WriteStartupError(ex);
                MessageBox.Show(
                    "MediaStudioWpf48 启动失败。\n\n" + ex.Message +
                    "\n\n详细信息已写入程序目录 startup-error.log。",
                    "Media Studio - Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteStartupError(e.Exception);
            MessageBox.Show("程序遇到未处理异常：\n" + e.Exception.Message +
                "\n\n详细信息已写入 startup-error.log。",
                "Media Studio - Runtime Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteStartupError(e.ExceptionObject as Exception ??
                new Exception(Convert.ToString(e.ExceptionObject)));
        }

        private static void WriteStartupError(Exception ex)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-error.log");
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    Environment.NewLine + ex + Environment.NewLine +
                    "------------------------------" + Environment.NewLine, Encoding.UTF8);
            }
            catch { /* logging must never prevent shutdown */ }
        }
    }
}