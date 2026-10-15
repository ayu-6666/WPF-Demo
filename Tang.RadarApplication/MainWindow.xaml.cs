using System.Windows;
using Tang.RadarApplication.Controls;
using System.Windows.Input;
using Tang.RadarApplication.Models;
using System.Collections.ObjectModel;
using Tang.RadarApplication.ViewModels;
namespace Tang.RadarApplication
{
    public partial class MainWindow : Window
    {
        public MainWindow() { InitializeComponent(); }
        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            radar?.ResetZoom();
        }

        private void OpenChart_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            ObservableCollection<RadarTarget>? targets = vm?.Targets as ObservableCollection<RadarTarget>;
            if (targets == null) targets = new ObservableCollection<RadarTarget>();
            var w = new ChartWindow(targets);
            // ChartWindow already constructs and binds its own ViewModel; just show it
            w.Owner = this;
            w.Show();
        }

        private void SelectToggle_Checked(object sender, RoutedEventArgs e)
        {
            // enable selection mode in radar control
            radar.IsHitTestVisible = true; // radar already handles selection; enable if needed
            // set internal flag
            radar.IsEnabled = true;
        }

        private void SelectToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            // disable selection mode
            radar.IsEnabled = false;
        }

        private void CrossToggle_Checked(object sender, RoutedEventArgs e)
        {
            // enable crosshair mode in radar scope
            radar.ShowCrosshair = true;
        }

        private void CrossToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            radar.ShowCrosshair = false;
        }

        private void WaferClear_Click(object sender, RoutedEventArgs e)
        {
            radar.ClearSelection();
            radar.ClearCrosshair();
        }

        private void WaferReset_Click(object sender, RoutedEventArgs e)
        {
            radar.ResetZoom();
            radar.ClearSelection();
            radar.ClearCrosshair();
            var win = new CoordV2System();
            win.Show();
        }

        private void DrawCoordinateSystem_Click(object sender, RoutedEventArgs e)
        {
            var win = new CoordinateSystem();
            win.Show();
        }
    }
}
