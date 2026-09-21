using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Tang.RadarApplication.Models;

namespace Tang.RadarApplication.Controls
{
    public sealed class RadarScope : Canvas
    {
        public static readonly DependencyProperty TargetsProperty = DependencyProperty.Register(nameof(Targets), typeof(ObservableCollection<RadarTarget>), typeof(RadarScope), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnTargetsChanged));
        public static readonly DependencyProperty ScanAngleProperty = DependencyProperty.Register(nameof(ScanAngle), typeof(double), typeof(RadarScope), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty RangeKmProperty = DependencyProperty.Register(nameof(RangeKm), typeof(double), typeof(RadarScope), new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));

        public ObservableCollection<RadarTarget>? Targets { get => (ObservableCollection<RadarTarget>?)GetValue(TargetsProperty); set => SetValue(TargetsProperty, value); }
        public double ScanAngle { get => (double)GetValue(ScanAngleProperty); set => SetValue(ScanAngleProperty, value); }
        public double RangeKm { get => (double)GetValue(RangeKmProperty); set => SetValue(RangeKmProperty, value); }

        // --- interactive & animation state ---
        // internal simulation removed: rely on data source / ViewModel to update Targets
        private Vector _offset = new Vector(0, 0); // pixel offset to allow zoom-centering
        private RadarTarget? _selectedTarget;
        private bool _isZooming;
        private Point _zoomStartMouse;
        private double _zoomStartRangeKm;
        private DateTime _lastFrame = DateTime.UtcNow;
        private readonly Dictionary<string, DateTime> _lastDetected = new();
        private bool _isPotentialZoom;
        private Vector _dragStartOffset;

        public RadarScope()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            MouseLeftButtonDown += RadarScope_MouseLeftButtonDown;
            MouseLeftButtonUp += RadarScope_MouseLeftButtonUp;
            MouseMove += RadarScope_MouseMove;
            MouseWheel += RadarScope_MouseWheel;
        }

        private static void OnTargetsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not RadarScope rs) return;
            if (e.OldValue is ObservableCollection<RadarTarget> old)
            {
                old.CollectionChanged -= rs.Targets_CollectionChanged;
            }
            if (e.NewValue is ObservableCollection<RadarTarget> neu)
            {
                neu.CollectionChanged += rs.Targets_CollectionChanged;
            }
        }

        public static readonly DependencyProperty IsScanningProperty = DependencyProperty.Register(nameof(IsScanning), typeof(bool), typeof(RadarScope), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
        public bool IsScanning { get => (bool)GetValue(IsScanningProperty); set => SetValue(IsScanningProperty, value); }

        public void StartScanning() => IsScanning = true;
        public void StopScanning() => IsScanning = false;

        public void ResetZoom()
        {
            RangeKm = 100; // reset to default
            _offset = new Vector(0, 0);
            InvalidateVisual();
        }

        private void OnLoaded(object? s, RoutedEventArgs e)
        {
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            if (Targets != null) Targets.CollectionChanged += Targets_CollectionChanged;
        }

        private void OnUnloaded(object? s, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            if (Targets != null) Targets.CollectionChanged -= Targets_CollectionChanged;
        }

        private void Targets_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // no internal simulation here; we just keep subscription alive
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            var dt = (now - _lastFrame).TotalSeconds;
            _lastFrame = now;

            // advance sweep when scanning
            if (IsScanning)
            {
                ScanAngle = (ScanAngle + 60.0 * dt) % 360.0; // 60 deg/sec
            }

            // no internal target simulation here; Targets are updated by ViewModel or data source

            InvalidateVisual();
        }

        private void RadarScope_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // zoom centered at mouse
            var pos = e.GetPosition(this);
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 18);
            if (radius <= 0) return;
            var p = pos;
            var worldBefore = new Vector((p.X - center.X - _offset.X) / radius * RangeKm, (p.Y - center.Y - _offset.Y) / radius * RangeKm);
            var factor = e.Delta > 0 ? 0.9 : 1.1;
            var newRange = Math.Max(1, Math.Min(100000, RangeKm * factor));
            RangeKm = newRange;
            // compute new offset so worldBefore maps to same pixel
            _offset = new Vector(p.X - center.X - worldBefore.X / RangeKm * radius, p.Y - center.Y - worldBefore.Y / RangeKm * radius);
        }

        private void RadarScope_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this);
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 18);
            if (radius <= 0) return;

            // check for target selection
            if (Targets != null)
            {
                foreach (var t in Targets)
                {
                    var p = new Point(center.X + _offset.X + t.X / RangeKm * radius, center.Y + _offset.Y + t.Y / RangeKm * radius);
                    if ((p - pos).Length <= 7)
                    {
                        _selectedTarget = t;
                        // start potential zoom; actual zoom begins on mouse move beyond threshold
                        _isPotentialZoom = true;
                        CaptureMouse();
                        _zoomStartMouse = pos;
                        _zoomStartRangeKm = RangeKm;
                        _dragStartOffset = _offset;
                        e.Handled = true;
                        return;
                    }
                }
            }

            // clicked empty radar area: start zoom anchored at click
            if ((pos - center).Length <= radius)
            {
                _selectedTarget = null;
                _isPotentialZoom = true;
                CaptureMouse();
                _zoomStartMouse = pos;
                _zoomStartRangeKm = RangeKm;
                _dragStartOffset = _offset;
                e.Handled = true;
            }
        }

        private void RadarScope_MouseMove(object? sender, MouseEventArgs e)
        {
            if ((!_isZooming && !_isPotentialZoom) || !IsMouseCaptured) return;
            var pos = e.GetPosition(this);
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 18);
            if (radius <= 0) return;
            var moveDist = (pos - _zoomStartMouse).Length;
            if (!_isZooming && _isPotentialZoom)
            {
                // start zoom if moved beyond threshold; keep original click as anchor
                if (moveDist < 4) return;
                _isZooming = true;
                // keep _zoomStartMouse (initial click) and _zoomStartRangeKm set on MouseDown
            }

            // compute new range based on vertical drag (use start drag distance for factor)
            var dy = pos.Y - _zoomStartMouse.Y;
            var factor = 1.0 + (-dy) / 300.0; // drag up to zoom in
            factor = Math.Max(0.1, Math.Min(10.0, factor));
            var newRange = Math.Max(1, Math.Min(100000, _zoomStartRangeKm * factor));

            // anchor zoom at the initial click position so the pixel under the click remains fixed
            var p = _zoomStartMouse;
            var worldBefore = new Vector((p.X - center.X - _dragStartOffset.X) / radius * _zoomStartRangeKm, (p.Y - center.Y - _dragStartOffset.Y) / radius * _zoomStartRangeKm);
            RangeKm = newRange;
            _offset = new Vector(p.X - center.X - worldBefore.X / RangeKm * radius, p.Y - center.Y - worldBefore.Y / RangeKm * radius);
        }

        private void RadarScope_MouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured) ReleaseMouseCapture();
            // if it was only a click (no zoom started), keep selection but clear potential flag
            if (_isPotentialZoom && !_isZooming)
            {
                _isPotentialZoom = false;
                return;
            }
            _isZooming = false;
            _isPotentialZoom = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 18);
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(35, 150, 120)), 1);

            // apply translation transform so all drawn elements (circles, targets, sector) share same offset
            dc.PushTransform(new TranslateTransform(_offset.X, _offset.Y));

            // draw concentric circles and spokes (use center without offset)
            for (var i = 1; i <= 4; i++) dc.DrawEllipse(null, pen, center, radius * i / 4, radius * i / 4);
            for (var a = 0; a < 360; a += 30)
            {
                var p = PointAt(center, radius, a);
                dc.DrawLine(pen, center, p);
            }

            // clip to circular radar area so things outside are hidden (clip must account for transform)
            dc.PushClip(new EllipseGeometry(center, radius, radius));

            // sweep half-angle (degrees)
            var sweepHalf = 7.5;
            // draw sweeping sector (15 degrees) inside clip only when scanning
            if (IsScanning)
            {
                var start = (ScanAngle - sweepHalf + 360) % 360;
                var end = (ScanAngle + sweepHalf) % 360;
                var geom = CreateSectorGeometry(center, radius, start, end);
                dc.PushOpacity(0.35);
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(53, 230, 165)), null, geom);
                dc.Pop();
            }

            var collection = Targets;
            if (!(collection == null) && RangeKm > 0)
            {
                foreach (var t in collection)
                {
                    var p = new Point(center.X + t.X / RangeKm * radius, center.Y + t.Y / RangeKm * radius);
                    if ((p - center).Length <= radius)
                    {
                        var isSelected = _selectedTarget != null && _selectedTarget == t;

                        // compute angle of target (0 = north)
                        var dx = t.X; var dy = t.Y; // km
                        var angRad = Math.Atan2(dx, -dy);
                        var angDeg = (angRad * 180.0 / Math.PI + 360) % 360;
                        // simpler minimal delta
                        var minDelta = Math.Min(Math.Abs(angDeg - ScanAngle), 360 - Math.Abs(angDeg - ScanAngle));

                        var detected = minDelta <= sweepHalf + 1.0; // add small tolerance
                        if (detected)
                        {
                            _lastDetected[t.Id] = DateTime.UtcNow;
                        }

                        // highlight if recently detected
                        var recentlyDetected = false;
                        if (_lastDetected.TryGetValue(t.Id, out var dtDetected))
                        {
                            if ((DateTime.UtcNow - dtDetected).TotalSeconds <= 2.0) recentlyDetected = true;
                        }

                        Brush brush;
                        if (isSelected) brush = Brushes.OrangeRed;
                        else if (recentlyDetected) brush = Brushes.LimeGreen;
                        else brush = new SolidColorBrush(Color.FromRgb(255, 190, 70));

                        dc.DrawEllipse(brush, null, p, isSelected ? 7 : 5, isSelected ? 7 : 5);
                        dc.DrawText(new FormattedText(t.Id, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.White, 1.0), new Point(p.X + 7, p.Y - 7));
                    }
                }
            }

            dc.Pop(); // pop clip

            // pop transform after drawing radar elements
            dc.Pop();

            // north indicator and range info (drawn above transformed radar)
            dc.DrawText(new FormattedText("N", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Bold"), 14, Brushes.White, 1.0), new Point(center.X - 6 + _offset.X, center.Y - radius - 18 + _offset.Y));
            dc.DrawText(new FormattedText(RangeKm + " km", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.LightGray, 1.0), new Point(12, 12));

            // draw selected target info at lower-left
            if (_selectedTarget != null)
            {
                var info = $"ID: {_selectedTarget.Id}  X:{_selectedTarget.X:F2} km  Y:{_selectedTarget.Y:F2} km";
                var ft = new FormattedText(info, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.LightGreen, 1.0);
                dc.DrawText(ft, new Point(12, ActualHeight - ft.Height - 12));
            }
        }

        private static Geometry CreateSectorGeometry(Point center, double radius, double startDeg, double endDeg)
        {
            // create sector from startDeg to endDeg (both in degrees) going the short way
            var startRad = startDeg * Math.PI / 180.0;
            var endRad = endDeg * Math.PI / 180.0;
            var largeArc = Math.Abs(endDeg - startDeg) > 180;
            var start = new Point(center.X + radius * Math.Sin(startRad), center.Y - radius * Math.Cos(startRad));
            var end = new Point(center.X + radius * Math.Sin(endRad), center.Y - radius * Math.Cos(endRad));
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                ctx.BeginFigure(center, true, true);
                ctx.LineTo(start, true, true);
                ctx.ArcTo(end, new Size(radius, radius), 0, largeArc, SweepDirection.Clockwise, true, true);
                ctx.LineTo(center, true, true);
            }
            sg.Freeze();
            return sg;
        }

        private static Point PointAt(Point c, double r, double degrees) { var rad = degrees * System.Math.PI / 180; return new Point(c.X + r * System.Math.Sin(rad), c.Y - r * System.Math.Cos(rad)); }
    }
}
