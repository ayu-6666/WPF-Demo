using Radae3DDemo3.Models;
using Radae3DDemo3.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Radae3DDemo3
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ViewModels.MainViewModel();
        }

        // 目标点击事件
        private void Target_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ContentPresenter presenter && presenter.Content is RadarTarget target)
            {
                var vm = (MainViewModel)DataContext;
                vm.SelectTargetCommand.Execute(target);
            }
        }
    }
}
