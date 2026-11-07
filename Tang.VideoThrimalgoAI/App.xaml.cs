using System;
using System.IO;
using System.Windows;

namespace Tang.VideoThrimalgoAI
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (_, args) =>
            {
                try
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "TangVideoCrash.log"),
                        args.Exception.ToString());
                }
                catch
                {
                }
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 仅有 WPF 消息循环时，WinForms 的 Vista 风格文件夹对话框依赖视觉样式；
            // 不启用会导致 FolderBrowserDialog 无法创建/显示。
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            base.OnStartup(e);
        }
    }
}
