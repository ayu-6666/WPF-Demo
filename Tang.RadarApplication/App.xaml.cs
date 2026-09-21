using System.Windows;
using Tang.RadarApplication.Services;
using Tang.RadarApplication.ViewModels;

namespace Tang.RadarApplication
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var source = new SwitchingRadarDataSource(new KvaserCanDataSource(), new SimulatedRadarDataSource());
            var vm = new MainViewModel(source, new OpenAiAnalysisService());
            var window = new MainWindow { DataContext = vm };
            MainWindow = window;
            window.Show();
        }
    }
}
