using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CoderControl
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // --render &lt;目录&gt;：离屏渲染界面截图（用于文档，不依赖桌面是否可见）
            if (e.Args.Length >= 2 && string.Equals(e.Args[0], "--render", StringComparison.OrdinalIgnoreCase))
            {
                base.OnStartup(e);
                RenderSnapshots(e.Args[1]);
                return;
            }

            base.OnStartup(e);
            new MainWindow().Show();
        }

        private void RenderSnapshots(string outputDirectory)
        {
            try
            {
                Directory.CreateDirectory(outputDirectory);
                var window = new MainWindow
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000,
                    Top = -20000,
                    ShowInTaskbar = false,
                    Width = 1600,
                    Height = 1000
                };
                window.Show();
                window.UpdateLayout();

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    SaveSnapshot(window, Path.Combine(outputDirectory, "after-main.png"));

                    // 最大化状态：验证窗口按钮图标切换与布局
                    window.WindowState = WindowState.Maximized;
                    window.UpdateLayout();
                    SaveSnapshot(window, Path.Combine(outputDirectory, "after-maximized.png"));
                    window.WindowState = WindowState.Normal;
                    window.UpdateLayout();

                    window.Viewer.OpenSample();
                    window.UpdateLayout();
                    SaveSnapshot(window, Path.Combine(outputDirectory, "after-sample.png"));

                    window.Viewer.CloseAll();
                    window.UpdateLayout();
                    SaveSnapshot(window, Path.Combine(outputDirectory, "after-empty.png"));

                    window.Close();
                    Shutdown();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"截图渲染失败：{ex.Message}");
                Shutdown(1);
            }
        }

        private static void SaveSnapshot(Window window, string path)
        {
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            window.UpdateLayout();
            int width = (int)Math.Ceiling(window.ActualWidth);
            int height = (int)Math.Ceiling(window.ActualHeight);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}
