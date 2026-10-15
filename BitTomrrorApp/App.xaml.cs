using BitTomrrorApp.Services;
using System.Configuration;
using System.Data;
using System.Windows;

namespace BitTomrrorApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            await TorrentEngineService.Instance.InitializeAsync(Dispatcher);
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }

}
