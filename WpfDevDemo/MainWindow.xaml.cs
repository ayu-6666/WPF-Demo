using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfDevDemo.ViewModels;
using WPFDevelopers;
using WPFDevelopers.Controls;

namespace WpfDevDemo
{
    public partial class MainWindow : WPFDevelopers.Net45x.Window
    {
        private bool _isDark;
        private Color _currentColor;

        public MainWindow()
        {
            InitializeComponent();
            ThemeManager.Instance.SetTheme(ThemeType.Light);
            var vm = DataContext as MainVM;
            if (vm != null && vm.ControlPanel == null)
                throw new NullReferenceException(nameof(vm.ControlPanel));
        }

        private void ThemeSwitchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isDark)
                ThemeManager.Instance.SetTheme(ThemeType.Light);
            else
                ThemeManager.Instance.SetTheme(ThemeType.Dark);
            _isDark = !_isDark;
        }

        private void ColorPicker_Apply(object sender, RoutedEventArgs e)
        {
            _currentColor = ThemeColorPicker.SelectedColor;
            ThemeManager.Instance.SetColor(ThemeColorPicker.SelectedColor);
        }

        private void ColorPicker_Cancel(object sender, RoutedEventArgs e)
        {
            ThemeColorPicker.SelectedColor = _currentColor;
            ColorSwitchBtn.IsChecked = false;
        }

        private void MenuTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var vm = DataContext as MainVM;
            if (vm == null) return;
            vm.MenuSearchTextChanged.Execute(PART_MenuTextBox.Text);
        }

        private void NavigateMenu_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var vm = DataContext as MainVM;
            if (vm == null) return;
            vm.MenuSelectionChangedCommand.Execute(NavigateMenu.SelectedItem);
        }

        private void CircleMenu_ItemClick(object sender, RoutedEventArgs e)
        {
            var circleMenuItem = e.OriginalSource as CircleMenuItem;
            if (circleMenuItem == null) return;
            switch (circleMenuItem.Name)
            {
                case "Github":
                    Process.Start(new ProcessStartInfo("https://github.com/WPFDevelopersOrg/WPFDevelopers")
                    {
                        UseShellExecute = true
                    });
                    break;
                case "Code":
                    CodeDrawer.IsOpen = true;
                    break;
                case "ClearToast":
                    Toast.Push("Clear Toast", ToastImage.Info, true);
                    break;
                default:
                    Toast.Push(circleMenuItem.ToolTip?.ToString() ?? string.Empty, ToastImage.Info, true);
                    break;
            }
        }
    }
}
