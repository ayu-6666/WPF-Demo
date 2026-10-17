using System;
using System.Windows;
using System.Windows.Threading;

namespace IpanelWpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 全局异常兜底，避免未处理异常直接崩溃
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"发生未处理异常：\n\n{args.Exception.Message}",
                    "IpanelWpf", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
            base.OnStartup(e);
        }
    }
}
