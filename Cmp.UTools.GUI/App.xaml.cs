using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Cmp.UTools.GUI
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Application.Current.DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"发生未处理的异常：{args.Exception.Message}\n详细信息已写入日志文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "unhandled-exception.log");
                try
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:O}]{Environment.NewLine}{args.Exception}{Environment.NewLine}{Environment.NewLine}");
                }
                catch
                {
                }
                args.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"发生未处理的异常：{ex?.Message}\n详细信息已写入日志文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "unhandled-exception.log");
                try
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:O}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
                }
                catch
                {
                }
            };  
            base.OnStartup(e);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            
        }
    }
}
