using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RebootHMI.Controls
{
    /// <summary>
    /// 速度设定用数字微调框：文本框 + 上/下小三角。
    /// </summary>
    [TemplatePart(Name = PartUp, Type = typeof(RepeatButton))]
    [TemplatePart(Name = PartDown, Type = typeof(RepeatButton))]
    public class NumberUpDown : Control
    {
        private const string PartUp = "PART_Up";
        private const string PartDown = "PART_Down";

        static NumberUpDown()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumberUpDown),
                new FrameworkPropertyMetadata(typeof(NumberUpDown)));
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(NumberUpDown),
                new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(NumberUpDown),
                new PropertyMetadata(0));

        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(NumberUpDown),
                new PropertyMetadata(999));

        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild(PartUp) is RepeatButton up)
                up.Click += (s, e) => Value = System.Math.Min(Maximum, Value + 1);
            if (GetTemplateChild(PartDown) is RepeatButton down)
                down.Click += (s, e) => Value = System.Math.Max(Minimum, Value - 1);
        }
    }
}
