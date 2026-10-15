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
using System.Windows.Shapes;
using Tang.RadarApplication.ViewModels;

namespace Tang.RadarApplication
{
    /// <summary>
    /// CoordV2System.xaml 的交互逻辑
    /// </summary>
    public partial class CoordV2System : Window
    {
        public CoordV2ViewModel ViewModel { get; } = new CoordV2ViewModel();

        public CoordV2System()
        {
            InitializeComponent();
            DataContext = ViewModel;

            // 光标位置实时反馈到 VM（左下角面板绑定它）
            Canvas.MouseWorldPositionChanged += (_, p) => ViewModel.MouseWorld = p;

            // 目标被选中或手动创建时，实时更新选中项
            Canvas.SelectionChanged += (_, t) => ViewModel.SelectedTarget = t;
            Canvas.TargetCreated += (_, t) =>
            {
                ViewModel.SelectedTarget = t;
            };

            ViewModel.ResetViewRequested += () => Canvas.ResetView();
        }
    }
}
