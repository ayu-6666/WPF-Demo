using System.Windows;
using IpanelWpf.ViewModels;

namespace IpanelWpf.Views
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
