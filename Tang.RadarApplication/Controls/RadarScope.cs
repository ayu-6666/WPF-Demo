using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Tang.RadarApplication.Models;

namespace Tang.RadarApplication.Controls
{
    public sealed class RadarScope : Canvas
    {
        public static readonly DependencyProperty TargetsProperty = DependencyProperty.Register(nameof(Targets), typeof(ObservableCollection<RadarTarget>), typeof(RadarScope), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ScanAngleProperty = DependencyProperty.Register(nameof(ScanAngle), typeof(double), typeof(RadarScope), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty RangeKmProperty = DependencyProperty.Register(nameof(RangeKm), typeof(double), typeof(RadarScope), new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));
        public ObservableCollection<RadarTarget>? Targets { get => (ObservableCollection<RadarTarget>?)GetValue(TargetsProperty); set => SetValue(TargetsProperty, value); }
        public double ScanAngle { get => (double)GetValue(ScanAngleProperty); set => SetValue(ScanAngleProperty, value); }
        public double RangeKm { get => (double)GetValue(RangeKmProperty); set => SetValue(RangeKmProperty, value); }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc); var center = new Point(ActualWidth / 2, ActualHeight / 2); var radius = System.Math.Max(0, System.Math.Min(ActualWidth, ActualHeight) / 2 - 18); var pen = new Pen(new SolidColorBrush(Color.FromRgb(35, 150, 120)), 1);
            for (var i = 1; i <= 4; i++) dc.DrawEllipse(null, pen, center, radius * i / 4, radius * i / 4);
            for (var a = 0; a < 360; a += 30) { var p = PointAt(center, radius, a); dc.DrawLine(pen, center, p); }
            var sweepPen = new Pen(new SolidColorBrush(Color.FromArgb(170, 53, 230, 165)), 3); dc.DrawLine(sweepPen, center, PointAt(center, radius, ScanAngle));
            var collection = Targets; if (collection == null || RangeKm <= 0) return; foreach (var t in collection) { var p = new Point(center.X + t.X / RangeKm * radius, center.Y + t.Y / RangeKm * radius); if ((p - center).Length <= radius) { dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 190, 70)), null, p, 5, 5); dc.DrawText(new FormattedText(t.Id, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.White, 1.0), new Point(p.X + 7, p.Y - 7)); } }
            dc.DrawText(new FormattedText("N", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Bold"), 14, Brushes.White, 1.0), new Point(center.X - 6, center.Y - radius - 18)); dc.DrawText(new FormattedText(RangeKm + " km", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.LightGray, 1.0), new Point(12, 12));
        }
        private static Point PointAt(Point c, double r, double degrees) { var rad = degrees * System.Math.PI / 180; return new Point(c.X + r * System.Math.Sin(rad), c.Y - r * System.Math.Cos(rad)); }
    }
}
