using System.Windows;
using RebootHMI.ViewModels;

namespace RebootHMI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        // 自定义标题栏窗控
        private void OnMinimize(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void OnMaximizeRestore(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void OnClose(object sender, RoutedEventArgs e)
            => Close();
    }
}
