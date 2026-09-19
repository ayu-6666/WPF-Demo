using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RadarApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 订阅悬浮事件
            waferCanvas.HoveredDieChanged += WaferCanvas_HoveredDieChanged;
            menuBar.IsChecked = false;
            UpdateWaferConfig();
            Loaded += Window_Loaded;
        }
        // 处理悬浮索引变化
        private void WaferCanvas_HoveredDieChanged(int row, int col)
        {
            if (row == -1 || col == -1)
            {
                txtHoveredIndex.Text = "当前悬浮: 无";
            }
            else
            {
                txtHoveredIndex.Text = $"当前悬浮坐标: 行 [{row}] , 列 [{col}]";
            }
        }
        private void CmbWaferSize_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateWaferConfig();
        private void Config_TextChanged(object sender, TextChangedEventArgs e) => UpdateWaferConfig();

        private void UpdateWaferConfig()
        {
            if (waferCanvas == null || cmbWaferSize == null || txtDieWidth == null) return;

            if (cmbWaferSize.SelectedItem is ComboBoxItem item)
            {
                string sizeStr = item.Content.ToString().Replace(" 英寸", "");
                if (double.TryParse(sizeStr, out double size)) waferCanvas.WaferSize = size;
            }

            if (double.TryParse(txtDieWidth.Text, out double dw)) waferCanvas.DieWidth = dw;
            if (double.TryParse(txtDieHeight.Text, out double dh)) waferCanvas.DieHeight = dh;
            if (double.TryParse(txtScribeGap.Text, out double gap)) waferCanvas.ScribeGap = gap;

            waferCanvas.InvalidateVisual();
        }

        private void BtnSetValid_Click(object sender, RoutedEventArgs e) => waferCanvas.SetSelectedState(DieState.Valid);
        private void BtnSetInvalid_Click(object sender, RoutedEventArgs e) => waferCanvas.SetSelectedState(DieState.Invalid);
        private void BtnClearSelection_Click(object sender, RoutedEventArgs e) => waferCanvas.ClearSelection();
        private void BtnResetView_Click(object sender, RoutedEventArgs e) => waferCanvas.ResetView();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 默认选中第一个菜单，触发 Checked 后自动导航
            if (MenuPanel.Children.Count > 0 &&
                MenuPanel.Children[0] is RadioButton first)
            {
                first.IsChecked = true;
            }
        }

        private void Menu_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string pageUri)
            {
                if (pageUri == "others") 
                {
                    menuBar.IsChecked = false;
                    return;
                }
                MainFrame.Navigate(new Uri(pageUri, UriKind.Relative));
                menuBar.IsChecked = true;
            }
        }
    }
}
