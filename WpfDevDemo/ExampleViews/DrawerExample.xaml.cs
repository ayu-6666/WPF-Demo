using System.Windows;
using System.Windows.Controls;

namespace WpfDevDemo.ExampleViews
{
    /// <summary>
    /// DrawerExample.xaml 的交互逻辑
    /// </summary>
    public partial class DrawerExample : UserControl
    {
        public DrawerExample()
        {
            InitializeComponent();
        }

        private void ButtonTop_Click(object sender, RoutedEventArgs e)
        {
            MyDrawerTop.IsOpen = true;
        }

        private void ButtonBottom_Click(object sender, RoutedEventArgs e)
        {
            MyDrawerBottom.IsOpen = true;
        }

        private void ButtonLeft_Click(object sender, RoutedEventArgs e)
        {
            MyDrawerLeft.IsOpen = true;
        }

        private void ButtonRight_Click(object sender, RoutedEventArgs e)
        {
            MyDrawerRight.IsOpen = true;
        }
    }
}
