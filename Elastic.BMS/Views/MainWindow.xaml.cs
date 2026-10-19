using System.Windows;
using Elastic.BMS.ViewModels;

namespace Elastic.BMS.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void OnExit(object sender, RoutedEventArgs e) => Close();
    }
}
