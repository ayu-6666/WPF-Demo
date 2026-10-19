using System.Windows;
using System.Windows.Threading;

namespace Elastic.BMS
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), "电池监控与检测系统 运行异常", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
