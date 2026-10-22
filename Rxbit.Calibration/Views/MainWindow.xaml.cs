using System.Windows;
using Rxbit.Calibration.ViewModels;

namespace Rxbit.Calibration.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
