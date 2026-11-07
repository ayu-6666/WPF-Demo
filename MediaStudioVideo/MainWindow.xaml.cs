using System.Windows;
using System.Windows.Input;
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
        }

        private MainViewModel Vm => (MainViewModel)DataContext;

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
