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
            w.DataContext = new ChartWinViewModel(targets);
            w.Owner = this;
            w.Show();
        }
    }
}
