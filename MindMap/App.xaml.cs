using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MindMapWpf48
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 未处理异常写入日志，便于排查启动崩溃
            DispatcherUnhandledException += (s, args) =>
            {
                try { File.WriteAllText(@"d:\tang\vs-workspace\WPF-Demo\_crash_ui.log", args.Exception.ToString()); } catch { }
                args.Handled = false;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                try { File.WriteAllText(@"d:\tang\vs-workspace\WPF-Demo\_crash_app.log", args.ExceptionObject.ToString()); } catch { }
            };
            base.OnStartup(e);
        }
    }
}
