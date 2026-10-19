using System.Windows;
using System.Windows.Threading;

namespace RebootHMI
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), "RebootHMI 运行异常", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
