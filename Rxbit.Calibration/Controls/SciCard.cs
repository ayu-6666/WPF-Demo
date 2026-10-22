using System.Windows;
using System.Windows.Controls;

namespace Rxbit.Calibration.Controls
{
    /// <summary>深灰扁平风格卡片容器。</summary>
    public class SciCard : ContentControl
    {
        static SciCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SciCard),
                new FrameworkPropertyMetadata(typeof(SciCard)));
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
