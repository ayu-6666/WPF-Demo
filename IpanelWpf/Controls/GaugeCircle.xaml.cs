using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IpanelWpf.Controls
{
    public partial class GaugeCircle : UserControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(GaugeCircle),
                new PropertyMetadata(0.0, (d, e) => ((GaugeCircle)d).Update()));
        public static readonly DependencyProperty CenterValueProperty =
            DependencyProperty.Register(nameof(CenterValue), typeof(string), typeof(GaugeCircle), new PropertyMetadata(""));
        public static readonly DependencyProperty CenterLabelProperty =
            DependencyProperty.Register(nameof(CenterLabel), typeof(string), typeof(GaugeCircle), new PropertyMetadata(""));
        public static readonly DependencyProperty BottomTextProperty =
            DependencyProperty.Register(nameof(BottomText), typeof(string), typeof(GaugeCircle), new PropertyMetadata(""));
        public static readonly DependencyProperty GaugeBrushProperty =
            DependencyProperty.Register(nameof(GaugeBrush), typeof(Brush), typeof(GaugeCircle),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x33, 0x70, 0xFF))));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string CenterValue
        {
            get => (string)GetValue(CenterValueProperty);
            set => SetValue(CenterValueProperty, value);
        }

        public string CenterLabel
        {
            get => (string)GetValue(CenterLabelProperty);
            set => SetValue(CenterLabelProperty, value);
        }

        public string BottomText
        {
            get => (string)GetValue(BottomTextProperty);
            set => SetValue(BottomTextProperty, value);
        }

        public Brush GaugeBrush
        {
            get => (Brush)GetValue(GaugeBrushProperty);
            set => SetValue(GaugeBrushProperty, value);
        }

        public GaugeCircle()
        {
            InitializeComponent();
            SizeChanged += (s, e) => Update();
            Loaded += (s, e) => Update();
        }

        private void Update()
        {
            if (Fill == null)
                return;

            if (Value <= 0)
            {
                Fill.Visibility = Visibility.Hidden;
                return;
            }

            Fill.Visibility = Visibility.Visible;
            var diameter = Math.Max(Fill.ActualWidth - Fill.StrokeThickness, 1);
            var circumference = Math.PI * diameter;
            var dash = circumference * Math.Min(Value, 100.0) / 100.0 / Fill.StrokeThickness;
            Fill.StrokeDashArray = new DoubleCollection { dash, circumference / Fill.StrokeThickness + 10 };
        }
    }
}
