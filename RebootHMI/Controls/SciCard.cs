using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace RebootHMI.Controls
{
    /// <summary>
    /// 科技风卡片：青色发光描边、四角折线、顶部切角标题栏（三点 + 居中标题）。
    /// </summary>
    public class SciCard : ContentControl
    {
        static SciCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SciCard),
                new FrameworkPropertyMetadata(typeof(SciCard)));
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(SciCard),
                new PropertyMetadata(string.Empty));

        /// <summary>卡片标题，为空时不显示标题栏。</summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleIconProperty =
            DependencyProperty.Register(nameof(TitleIcon), typeof(PackIconKind), typeof(SciCard),
                new PropertyMetadata(PackIconKind.None));

        /// <summary>标题左侧小图标（None 时不显示）。</summary>
        public PackIconKind TitleIcon
        {
            get => (PackIconKind)GetValue(TitleIconProperty);
            set => SetValue(TitleIconProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusValueProperty =
            DependencyProperty.Register(nameof(CornerRadiusValue), typeof(CornerRadius), typeof(SciCard),
                new PropertyMetadata(new CornerRadius(12)));

        public CornerRadius CornerRadiusValue
        {
            get => (CornerRadius)GetValue(CornerRadiusValueProperty);
            set => SetValue(CornerRadiusValueProperty, value);
        }
    }
}
