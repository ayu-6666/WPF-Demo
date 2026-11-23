using CoderControl.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoderControl.Views
{
    /// <summary>
    /// BreatheLightExample.xaml 的交互逻辑
    /// </summary>
    public partial class BreatheLightExample : UserControl
    {
        public BreatheLightExample()
        {
            InitializeComponent();
            ItemsList.ItemsSource = new[]
            {
                new BreatheLightModel
                {
                    Title = "产品发布会直播",
                    Status = "正在直播",
                    ImageSource = CreateAvatar(Color.FromRgb(255, 109, 148), Color.FromRgb(255, 64, 129))
                },
                new BreatheLightModel
                {
                    Title = "WPF 动画技术分享",
                    Status = "正在直播",
                    ImageSource = CreateAvatar(Color.FromRgb(130, 214, 255), Color.FromRgb(0, 140, 255))
                },
                new BreatheLightModel
                {
                    Title = "界面设计评审",
                    Status = "正在直播",
                    ImageSource = CreateAvatar(Color.FromRgb(255, 190, 120), Color.FromRgb(255, 128, 66))
                },
                new BreatheLightModel
                {
                    Title = "在线答疑",
                    Status = "正在直播",
                    ImageSource = CreateAvatar(Color.FromRgb(140, 235, 190), Color.FromRgb(38, 180, 140))
                }
            };
        }

        /// <summary>生成圆形渐变头像占位图，避免 Sample 页依赖外部图片资源。</summary>
        private static ImageSource CreateAvatar(Color inner, Color outer)
        {
            var brush = new RadialGradientBrush(inner, outer)
            {
                RadiusX = 0.65,
                RadiusY = 0.65,
                GradientOrigin = new Point(0.35, 0.3),
                Center = new Point(0.5, 0.5)
            };
            var drawing = new GeometryDrawing(brush, null, new EllipseGeometry(new Point(50, 50), 50, 50));
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }
    }
}
