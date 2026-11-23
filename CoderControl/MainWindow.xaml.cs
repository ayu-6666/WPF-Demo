using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CoderControl
{
    /// <summary>
    /// 主窗口：自绘标题栏 / 菜单栏 / 工具栏 + 解决方案资源管理器 + 标签文档区。
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double DefaultFontSize = 14;
        private const double ExplorerDefaultWidth = 300;

        private double _fontSize = DefaultFontSize;

        public MainWindow()
        {
            InitializeComponent();

            Viewer.CurrentDocumentChanged += (_, _) => UpdateWindowTitle();
            Explorer.FileOpenRequested += (_, path) => Viewer.OpenFile(path);

            UpdateWindowTitle();
        }

        // ================= 窗口标题栏 =================

        private void UpdateWindowTitle()
        {
            string title = string.IsNullOrEmpty(Viewer.CurrentTitle)
                ? "CoderControl - 代码查看器"
                : $"{Viewer.CurrentTitle} - CoderControl";
            WindowTitleText.Text = title;
            Title = title;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            bool maximized = WindowState == WindowState.Maximized;
            MaximizeButton.Content = maximized ? "\uE923" : "\uE922";
            MaximizeButton.ToolTip = maximized ? "还原" : "最大化";
            System.Windows.Automation.AutomationProperties.SetName(MaximizeButton, maximized ? "还原" : "最大化");
            MaximizeRestoreMenuItem.Header = maximized ? "还原窗口" : "最大化窗口";

            // WindowChrome 最大化时窗口会向屏幕外多出边框宽度，用内边距补偿
            Dispatcher.BeginInvoke(new Action(UpdateMaximizePadding), DispatcherPriority.Render);
        }

        private void UpdateMaximizePadding()
        {
            if (WindowState != WindowState.Maximized)
            {
                RootBorder.Margin = new Thickness(0);
                return;
            }

            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                if (handle == IntPtr.Zero || !GetWindowRect(handle, out NativeRect windowRect))
                {
                    return;
                }

                IntPtr monitor = MonitorFromWindow(handle, 2 /* MONITOR_DEFAULTTONEAREST */);
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (!GetMonitorInfo(monitor, ref info))
                {
                    return;
                }

                double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
                RootBorder.Margin = new Thickness(
                    Math.Max(0, (windowRect.Left - info.Work.Left) / scale),
                    Math.Max(0, (windowRect.Top - info.Work.Top) / scale),
                    Math.Max(0, (info.Work.Right - windowRect.Right) / scale),
                    Math.Max(0, (info.Work.Bottom - windowRect.Bottom) / scale));
            }
            catch
            {
                RootBorder.Margin = new Thickness(7);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public int Flags;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ================= 快捷键 =================

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            ModifierKeys mods = Keyboard.Modifiers;

            if (mods == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key == Key.L)
            {
                SetExplorerVisible(!ExplorerMenuItem.IsChecked);
                e.Handled = true;
                return;
            }

            if (mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.W)
            {
                Viewer.CloseAll();
                e.Handled = true;
                return;
            }

            if (mods == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.N:
                        Viewer.NewFile();
                        e.Handled = true;
                        return;
                    case Key.O:
                        OpenFileDialog();
                        e.Handled = true;
                        return;
                    case Key.W:
                        Viewer.CloseCurrent();
                        e.Handled = true;
                        return;
                    case Key.F:
                        Viewer.ShowFind();
                        e.Handled = true;
                        return;
                    case Key.OemPlus:
                    case Key.Add:
                        ChangeFontSize(2);
                        e.Handled = true;
                        return;
                    case Key.OemMinus:
                    case Key.Subtract:
                        ChangeFontSize(-2);
                        e.Handled = true;
                        return;
                }
            }

            if (mods == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.F5:
                        Viewer.ReloadCurrent();
                        e.Handled = true;
                        return;
                    case Key.F6:
                        Viewer.OpenSample();
                        e.Handled = true;
                        return;
                    case Key.F3:
                        Viewer.FindNext();
                        e.Handled = true;
                        return;
                }
            }

            if (mods == ModifierKeys.Shift && e.Key == Key.F3)
            {
                Viewer.FindPrevious();
                e.Handled = true;
            }
        }

        // ================= 文件 =================

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            Viewer.NewFile();
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog();
        }

        private void OpenFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                Title = "打开文件",
                Filter = "代码文件|*.cs;*.xaml;*.csproj;*.sln;*.json;*.xml;*.config;*.md;*.txt;*.bat;*.ps1;*.html;*.css;*.js;*.sql|所有文件|*.*"
            };
            if (dialog.ShowDialog(this) == true)
            {
                Viewer.OpenFile(dialog.FileName);
            }
        }

        private void ReloadFile_Click(object sender, RoutedEventArgs e)
        {
            Viewer.ReloadCurrent();
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            Viewer.CloseCurrent();
        }

        private void CloseAllTabs_Click(object sender, RoutedEventArgs e)
        {
            Viewer.CloseAll();
        }

        // ================= 编辑 =================

        private void Undo_Click(object sender, RoutedEventArgs e) => Viewer.Undo();

        private void Redo_Click(object sender, RoutedEventArgs e) => Viewer.Redo();

        private void Cut_Click(object sender, RoutedEventArgs e) => Viewer.Cut();

        private void Copy_Click(object sender, RoutedEventArgs e) => Viewer.Copy();

        private void Paste_Click(object sender, RoutedEventArgs e) => Viewer.Paste();

        private void SelectAll_Click(object sender, RoutedEventArgs e) => Viewer.SelectAll();

        // ================= 搜索 =================

        private void Find_Click(object sender, RoutedEventArgs e) => Viewer.ShowFind();

        private void FindNext_Click(object sender, RoutedEventArgs e) => Viewer.FindNext();

        private void FindPrev_Click(object sender, RoutedEventArgs e) => Viewer.FindPrevious();

        private void CopyPath_Click(object sender, RoutedEventArgs e) => Viewer.CopyCurrentPath();

        // ================= 视图 =================

        private void ToggleExplorer_Click(object sender, RoutedEventArgs e)
        {
            SetExplorerVisible(ExplorerMenuItem.IsChecked);
        }

        private void SetExplorerVisible(bool visible)
        {
            Explorer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            ExplorerSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            ExplorerColumn.MinWidth = visible ? 200 : 0;
            ExplorerColumn.Width = visible ? new GridLength(ExplorerDefaultWidth) : new GridLength(0);
            ExplorerMenuItem.IsChecked = visible;
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            Explorer.ExpandAll();
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            Explorer.CollapseAll();
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => ChangeFontSize(2);

        private void ZoomOut_Click(object sender, RoutedEventArgs e) => ChangeFontSize(-2);

        private void ZoomReset_Click(object sender, RoutedEventArgs e)
        {
            _fontSize = DefaultFontSize;
            Viewer.SetFontSize(_fontSize);
        }

        private void ChangeFontSize(double delta)
        {
            _fontSize = Math.Clamp(_fontSize + delta, 10, 28);
            Viewer.SetFontSize(_fontSize);
        }

        private void FontSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: string tag } && double.TryParse(tag, out double size))
            {
                _fontSize = size;
                Viewer.SetFontSize(size);
            }
        }

        // ================= 编码 / 语言 =================

        private void ReloadUtf8_Click(object sender, RoutedEventArgs e) => Viewer.ReloadWithEncoding(65001);

        private void ReloadGbk_Click(object sender, RoutedEventArgs e) => Viewer.ReloadWithEncoding(936);

        private void LanguageAuto_Click(object sender, RoutedEventArgs e) => Viewer.SetLanguage("Auto");

        private void LanguageCSharp_Click(object sender, RoutedEventArgs e) => Viewer.SetLanguage("C#");

        private void LanguageXml_Click(object sender, RoutedEventArgs e) => Viewer.SetLanguage("XAML");

        private void LanguageJs_Click(object sender, RoutedEventArgs e) => Viewer.SetLanguage("JavaScript");

        private void LanguageText_Click(object sender, RoutedEventArgs e) => Viewer.SetLanguage("Text");

        // ================= 设置 / 工具 =================

        private void WordWrap_Click(object sender, RoutedEventArgs e)
        {
            Viewer.SetWordWrap(WordWrapMenuItem.IsChecked);
        }

        private void LineNumbers_Click(object sender, RoutedEventArgs e)
        {
            Viewer.SetShowLineNumbers(LineNumbersMenuItem.IsChecked);
        }

        private void Format_Click(object sender, RoutedEventArgs e) => Viewer.FormatCurrent();

        private void TrimTrailing_Click(object sender, RoutedEventArgs e) => Viewer.TrimTrailingWhitespace();

        private void RemoveEmptyLines_Click(object sender, RoutedEventArgs e) => Viewer.RemoveEmptyLines();

        private void OpenModuleDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", FindModuleDirectory());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"无法打开目录：\n{ex.Message}", "CoderControl",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string FindModuleDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "CoderControl.csproj")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return AppContext.BaseDirectory;
        }

        // ================= 运行 / 插件 / 帮助 =================

        private void RunSample_Click(object sender, RoutedEventArgs e) => Viewer.OpenSample();

        private void Statistics_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, Viewer.GetStatistics(), "源码统计",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this,
                "CoderControl - 代码查看器\n\n" +
                "技术栈：WPF + AvalonEdit + Roslyn\n" +
                "功能：解决方案文件树、标签式代码预览、C# 格式化、语法高亮、编码切换。\n\n" +
                "版本 1.1.0",
                "关于 CoderControl", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
