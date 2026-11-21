using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MediaStudioWpf48.Services;
using MediaStudioWpf48.ViewModels;

namespace MediaStudioWpf48
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ThemeManager.Apply(false);

            // 日志追加后自动滚动到底部。
            if (DataContext is MainViewModel vm)
                vm.LogLines.CollectionChanged += LogLines_CollectionChanged;
        }

        private MainViewModel Vm => DataContext as MainViewModel;

        private void LogLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add) return;
            var vm = Vm;
            if (vm == null || vm.LogLines.Count == 0) return;
            var last = vm.LogLines[vm.LogLines.Count - 1];
            Dispatcher.BeginInvoke(new Action(() => LogList.ScrollIntoView(last)), DispatcherPriority.Background);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Window_StateChanged(object sender, System.EventArgs e)
        {
            // WindowChrome 最大化时窗口会向屏幕外扩展，留出内边距避免内容被裁切。
            RootBorder.Padding = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Min(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Max(object sender, RoutedEventArgs e) => ToggleMaximize();

        private void CloseWin(object sender, RoutedEventArgs e) => Close();

        private void ToggleLanguage(object sender, RoutedEventArgs e) =>
            LocalizationManager.Instance.ToggleLanguage();

        private void ToggleTheme(object sender, RoutedEventArgs e) =>
            ThemeManager.Apply(!ThemeManager.IsDark);

        private void InputClick(object sender, RoutedEventArgs e) => Vm.BrowseInput.Execute(null);

        private void MusicClick(object sender, RoutedEventArgs e) => Vm.BrowseMusic.Execute(null);

        private void OutputClick(object sender, RoutedEventArgs e) => Vm.BrowseOutput.Execute(null);

        private void LogVerbose_Checked(object sender, RoutedEventArgs e) => SetDetailedLog(true);

        private void LogSummary_Checked(object sender, RoutedEventArgs e) => SetDetailedLog(false);

        private void SetDetailedLog(bool value)
        {
            if (DataContext is MainViewModel vm) vm.DetailedLog = value;
        }
    }
}
