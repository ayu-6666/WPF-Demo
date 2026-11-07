using System.Windows;
using System.Windows.Input;
using BitTomrrorApp.ViewModels;
using Microsoft.Win32;

namespace BitTomrrorApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }
            DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

        private void ToggleMaximize()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaxIcon.Kind = MahApps.Metro.IconPacks.PackIconMaterialKind.WindowMaximize;
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaxIcon.Kind = MahApps.Metro.IconPacks.PackIconMaterialKind.WindowRestore;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            new Views.SettingsWindow { Owner = this }.ShowDialog();
        }

        private async void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择 BT 种子文件",
                Filter = "种子文件 (*.torrent)|*.torrent|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() != true) return;

            if (DataContext is MainViewModel vm)
                await vm.AddTorrentFileAsync(dialog.FileName);
        }
    }
}
