using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Tang.RadarApplication.Models;

namespace Tang.RadarApplication.ViewModels
{
    public class CoordinateV2Canvas  : FrameworkElement
    {
        #region 字段
        private readonly VisualCollection _visuals;
        private readonly DrawingVisual _gridLayer;     // 网格 + 坐标轴（缩放/平移时重绘）
        private readonly DrawingVisual _targetLayer;   // 所有目标（数据或视图变化时重绘）
        private readonly DrawingVisual _overlayLayer;  // 绘制预览

        // 视图变换：屏幕像素 = Origin + World * Scale （Y 轴反向）
        private double _scaleX = 20, _scaleY = 20;
        private double _originX, _originY;

        private Point _lastPan;
        private bool _isPanning;

        // 绘制模式用
        private Point? _drawStartWorld;
        private Point _drawCurrentWorld;
        #endregion

        #region 依赖属性
        public static readonly DependencyProperty TargetsProperty =
            DependencyProperty.Register(nameof(Targets), typeof(ObservableCollection<TargetItem>),
                typeof(CoordinateV2Canvas), new PropertyMetadata(null, OnTargetsChanged));

        public ObservableCollection<TargetItem> Targets
        {
            get => (ObservableCollection<TargetItem>)GetValue(TargetsProperty);
            set => SetValue(TargetsProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(TargetItem),
                typeof(CoordinateV2Canvas),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    (d, e) => ((CoordinateV2Canvas)d).RenderTargets()));

        public TargetItem SelectedItem
        {
            get => (TargetItem)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty XUnitProperty =
            DependencyProperty.Register(nameof(XUnit), typeof(string), typeof(CoordinateV2Canvas),
                new PropertyMetadata("m", (d, e) => ((CoordinateV2Canvas)d).RenderGrid()));

        public string XUnit
        {
            get => (string)GetValue(XUnitProperty);
            set => SetValue(XUnitProperty, value);
        }

        public static readonly DependencyProperty YUnitProperty =
            DependencyProperty.Register(nameof(YUnit), typeof(string), typeof(CoordinateV2Canvas),
                new PropertyMetadata("m", (d, e) => ((CoordinateV2Canvas)d).RenderGrid()));

        public string YUnit
        {
            get => (string)GetValue(YUnitProperty);
            set => SetValue(YUnitProperty, value);
        }

        /// <summary>强制刻度间距（0 = 自动）</summary>
        public static readonly DependencyProperty FixedTickStepProperty =
            DependencyProperty.Register(nameof(FixedTickStep), typeof(double), typeof(CoordinateV2Canvas),
                new PropertyMetadata(0d, (d, e) => ((CoordinateV2Canvas)d).RenderGrid()));

        public double FixedTickStep
        {
            get => (double)GetValue(FixedTickStepProperty);
            set => SetValue(FixedTickStepProperty, value);
        }

        /// <summary>当前交互模式：null = 选择/平移；非空 = 手动绘制</summary>
        public static readonly DependencyProperty DrawingTypeProperty =
            DependencyProperty.Register(nameof(DrawingType), typeof(TargetType?), typeof(CoordinateV2Canvas),
                new PropertyMetadata(null, (d, e) =>
                {
                    var c = (CoordinateV2Canvas)d;
                    c.Cursor = e.NewValue == null ? Cursors.Arrow : Cursors.Cross;
                    c._drawStartWorld = null;
                    c.RenderOverlay();
                }));

        public TargetType? DrawingType
        {
            get => (TargetType?)GetValue(DrawingTypeProperty);
            set => SetValue(DrawingTypeProperty, value);
        }

        /// <summary>默认新目标颜色</summary>
        public Color NewTargetColor { get; set; } = Colors.OrangeRed;
        #endregion

        #region 事件
        public event EventHandler<Point> MouseWorldPositionChanged;
        public event EventHandler<TargetItem> SelectionChanged;
        public event EventHandler<TargetItem> TargetCreated;
        #endregion

        public CoordinateV2Canvas()
        {
            _visuals = new VisualCollection(this);
            _gridLayer = new DrawingVisual();
            _targetLayer = new DrawingVisual();
            _overlayLayer = new DrawingVisual();
            _visuals.Add(_gridLayer);
            _visuals.Add(_targetLayer);
            _visuals.Add(_overlayLayer);

            Focusable = true;
            ClipToBounds = true;

            Loaded += (_, __) => { ResetView(); Render(); };
        }

        protected override int VisualChildrenCount => _visuals.Count;
        protected override Visual GetVisualChild(int index) => _visuals[index];

        #region 坐标变换
        public Point WorldToScreen(double wx, double wy)
            => new Point(_originX + wx * _scaleX, _originY - wy * _scaleY);

        public Point WorldToScreen(Point w) => WorldToScreen(w.X, w.Y);

        public Point ScreenToWorld(double sx, double sy)
            => new Point((sx - _originX) / _scaleX, (_originY - sy) / _scaleY);

        public Point ScreenToWorld(Point s) => ScreenToWorld(s.X, s.Y);

        private double W2Sx(double wx) => _originX + wx * _scaleX;
        private double W2Sy(double wy) => _originY - wy * _scaleY;

        /// <summary>设置可见的 X/Y 世界范围</summary>
        public void SetViewRange(double xMin, double xMax, double yMin, double yMax)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            if (xMax <= xMin || yMax <= yMin) return;
            _scaleX = ActualWidth / (xMax - xMin);
            _scaleY = ActualHeight / (yMax - yMin);
            _originX = -xMin * _scaleX;
            _originY = yMax * _scaleY;
            Render();
        }

        public void ResetView()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            // 默认 X∈[-10,10]，Y 保持等比例
            _scaleX = ActualWidth / 20.0;
            _scaleY = _scaleX;
            _originX = ActualWidth / 2.0;
            _originY = ActualHeight / 2.0;
            Render();
        }
        #endregion

        #region 渲染入口
        public void Render()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            RenderGrid();
            RenderTargets();
            RenderOverlay();
        }
        #endregion

        #region 网格 / 坐标轴渲染
        private void RenderGrid()
        {
            using var dc = _gridLayer.RenderOpen();
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));

            var tl = ScreenToWorld(0, 0);
            var br = ScreenToWorld(ActualWidth, ActualHeight);
            double wMinX = Math.Min(tl.X, br.X), wMaxX = Math.Max(tl.X, br.X);
            double wMinY = Math.Min(tl.Y, br.Y), wMaxY = Math.Max(tl.Y, br.Y);

            // 计算刻度步长
            double stepX = FixedTickStep > 0 ? FixedTickStep : NiceStep((wMaxX - wMinX) / 10);
            double stepY = FixedTickStep > 0 ? FixedTickStep : NiceStep((wMaxY - wMinY) / 10);

            // ---- 网格 ----
            var gridPen = FrozenPen(Color.FromArgb(50, 180, 180, 180), 1);
            for (double x = Math.Ceiling(wMinX / stepX) * stepX; x <= wMaxX; x += stepX)
            {
                double sx = W2Sx(x);
                if (sx < 0 || sx > ActualWidth) continue;
                dc.DrawLine(gridPen, new Point(sx, 0), new Point(sx, ActualHeight));
            }
            for (double y = Math.Ceiling(wMinY / stepY) * stepY; y <= wMaxY; y += stepY)
            {
                double sy = W2Sy(y);
                if (sy < 0 || sy > ActualHeight) continue;
                dc.DrawLine(gridPen, new Point(0, sy), new Point(ActualWidth, sy));
            }

            // ---- 坐标轴 ----
            var axisPen = FrozenPen(Colors.Black, 1.5);
            double xAxisY = W2Sy(0);
            double yAxisX = W2Sx(0);

            // X 轴（含正负箭头）
            if (xAxisY >= -1 && xAxisY <= ActualHeight + 1)
            {
                dc.DrawLine(axisPen, new Point(0, xAxisY), new Point(ActualWidth, xAxisY));
                DrawArrowHead(dc, new Point(ActualWidth - 4, xAxisY), 0, axisPen.Brush, 10, 6);
                DrawArrowHead(dc, new Point(4, xAxisY), Math.PI, axisPen.Brush, 10, 6);

                var txt = MakeText($"X ({XUnit})", 12, Brushes.Black);
                dc.DrawText(txt, new Point(ActualWidth - txt.Width - 15, xAxisY - txt.Height - 6));
            }
            // Y 轴
            if (yAxisX >= -1 && yAxisX <= ActualWidth + 1)
            {
                dc.DrawLine(axisPen, new Point(yAxisX, 0), new Point(yAxisX, ActualHeight));
                DrawArrowHead(dc, new Point(yAxisX, 4), -Math.PI / 2, axisPen.Brush, 10, 6);
                DrawArrowHead(dc, new Point(yAxisX, ActualHeight - 4), Math.PI / 2, axisPen.Brush, 10, 6);

                var txt = MakeText($"Y ({YUnit})", 12, Brushes.Black);
                dc.DrawText(txt, new Point(yAxisX + 10, 10));
            }

            // ---- 刻度 & 标签 ----
            var tickPen = FrozenPen(Colors.Black, 1);
            if (xAxisY >= 0 && xAxisY <= ActualHeight)
            {
                for (double x = Math.Ceiling(wMinX / stepX) * stepX; x <= wMaxX; x += stepX)
                {
                    if (Math.Abs(x) < stepX * 1e-9) continue;
                    double sx = W2Sx(x);
                    if (sx < 0 || sx > ActualWidth) continue;
                    dc.DrawLine(tickPen, new Point(sx, xAxisY - 4), new Point(sx, xAxisY + 4));
                    var t = MakeText(FormatTick(x, stepX), 11, Brushes.DimGray);
                    dc.DrawText(t, new Point(sx - t.Width / 2, xAxisY + 6));
                }
            }
            if (yAxisX >= 0 && yAxisX <= ActualWidth)
            {
                for (double y = Math.Ceiling(wMinY / stepY) * stepY; y <= wMaxY; y += stepY)
                {
                    if (Math.Abs(y) < stepY * 1e-9) continue;
                    double sy = W2Sy(y);
                    if (sy < 0 || sy > ActualHeight) continue;
                    dc.DrawLine(tickPen, new Point(yAxisX - 4, sy), new Point(yAxisX + 4, sy));
                    var t = MakeText(FormatTick(y, stepY), 11, Brushes.DimGray);
                    dc.DrawText(t, new Point(yAxisX - t.Width - 8, sy - t.Height / 2));
                }
            }

            // 原点 O
            if (xAxisY >= 0 && xAxisY <= ActualHeight && yAxisX >= 0 && yAxisX <= ActualWidth)
            {
                var o = MakeText("O", 11, Brushes.Black);
                dc.DrawText(o, new Point(yAxisX - o.Width - 4, xAxisY + 4));
            }
        }
        #endregion

        #region 目标渲染
        private void RenderTargets()
        {
            using var dc = _targetLayer.RenderOpen();
            if (Targets == null) return;

            foreach (var t in Targets)
                DrawTarget(dc, t);
        }

        private void DrawTarget(DrawingContext dc, TargetItem t)
        {
            var stroke = new SolidColorBrush(t.Color); stroke.Freeze();
            var pen = new Pen(stroke, t.Thickness); pen.Freeze();
            var fill = new SolidColorBrush(Color.FromArgb(60, t.Color.R, t.Color.G, t.Color.B));
            fill.Freeze();

            var p = WorldToScreen(t.X, t.Y);

            switch (t.Type)
            {
                case TargetType.Point:
                    dc.DrawEllipse(stroke, null, p, 4, 4);
                    break;

                case TargetType.Line:
                    dc.DrawLine(pen, p, WorldToScreen(t.X2, t.Y2));
                    break;

                case TargetType.Arrow:
                    {
                        var p2 = WorldToScreen(t.X2, t.Y2);
                        dc.DrawLine(pen, p, p2);
                        double ang = Math.Atan2(p2.Y - p.Y, p2.X - p.X);
                        DrawArrowHead(dc, p2, ang, stroke, 12, 6);
                        break;
                    }

                case TargetType.Rectangle:
                    {
                        double w = Math.Abs(t.Width * _scaleX);
                        double h = Math.Abs(t.Height * _scaleY);
                        dc.DrawRectangle(fill, pen, new Rect(p.X - w / 2, p.Y - h / 2, w, h));
                        break;
                    }

                case TargetType.Ellipse:
                    {
                        double rx = Math.Abs(t.Width * _scaleX / 2);
                        double ry = Math.Abs(t.Height * _scaleY / 2);
                        if (rx < 0.5 || ry < 0.5) break;
                        dc.DrawEllipse(fill, pen, p, rx, ry);
                        break;
                    }

                case TargetType.Triangle:
                    {
                        double w = t.Width * _scaleX;
                        double h = t.Height * _scaleY;
                        var g = new StreamGeometry();
                        using (var c = g.Open())
                        {
                            c.BeginFigure(new Point(p.X, p.Y - h / 2), true, true);
                            c.LineTo(new Point(p.X + w / 2, p.Y + h / 2), true, false);
                            c.LineTo(new Point(p.X - w / 2, p.Y + h / 2), true, false);
                        }
                        g.Freeze();
                        dc.DrawGeometry(fill, pen, g);
                        break;
                    }

                case TargetType.Ring:
                    {
                        double ro = Math.Abs(t.Radius * _scaleX);
                        double ri = Math.Abs(t.InnerRadius * _scaleX);
                        if (ro < 0.5) break;
                        var outer = new EllipseGeometry(p, ro, ro);
                        var inner = new EllipseGeometry(p, Math.Max(0, ri), Math.Max(0, ri));
                        var ring = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
                        ring.Freeze();
                        dc.DrawGeometry(fill, pen, ring);
                        break;
                    }
            }

            // 文字标签
            if (!string.IsNullOrEmpty(t.Label))
            {
                var ft = MakeText(t.Label, 12, Brushes.Black);
                // 半透明底
                var bg = new Rect(p.X + 6, p.Y - ft.Height - 6, ft.Width + 6, ft.Height + 2);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), null, bg);
                dc.DrawText(ft, new Point(p.X + 9, p.Y - ft.Height - 5));
            }

            // 选中高亮
            if (ReferenceEquals(t, SelectedItem) || t.IsSelected)
            {
                var selPen = new Pen(Brushes.OrangeRed, 1.8) { DashStyle = DashStyles.Dash };
                selPen.Freeze();
                dc.DrawEllipse(null, selPen, p, 14, 14);
            }
        }
        #endregion

        #region 绘制预览
        private void RenderOverlay()
        {
            using var dc = _overlayLayer.RenderOpen();
            if (DrawingType == null || _drawStartWorld == null) return;

            var a = WorldToScreen(_drawStartWorld.Value);
            var b = WorldToScreen(_drawCurrentWorld);

            var pen = new Pen(new SolidColorBrush(Color.FromArgb(180, NewTargetColor.R,
                NewTargetColor.G, NewTargetColor.B)), 1.5)
            { DashStyle = DashStyles.Dash };
            pen.Freeze();

            switch (DrawingType.Value)
            {
                case TargetType.Line:
                case TargetType.Arrow:
                    dc.DrawLine(pen, a, b);
                    break;
                case TargetType.Rectangle:
                case TargetType.Ellipse:
                case TargetType.Triangle:
                    dc.DrawRectangle(null, pen,
                        new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                                 Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)));
                    break;
                case TargetType.Ring:
                    double r = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
                    dc.DrawEllipse(null, pen, a, r, r);
                    break;
            }
        }
        #endregion

        #region 交互
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(this);
            double factor = e.Delta > 0 ? 1.15 : 1 / 1.15;

            // 关键：保持光标下的世界坐标点不动
            _originX = pos.X + (_originX - pos.X) * factor;
            _originY = pos.Y + (_originY - pos.Y) * factor;
            _scaleX *= factor;
            _scaleY *= factor;

            Render();
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            Focus();
            var pos = e.GetPosition(this);

            // 绘制模式
            if (DrawingType != null && e.ChangedButton == MouseButton.Left)
            {
                _drawStartWorld = ScreenToWorld(pos);
                _drawCurrentWorld = _drawStartWorld.Value;
                RenderOverlay();
                CaptureMouse();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                var world = ScreenToWorld(pos);
                var hit = HitTest(world);
                SelectedItem = hit;
                SelectionChanged?.Invoke(this, hit);
                if (hit == null)
                {
                    _isPanning = true;
                    _lastPan = pos;
                    CaptureMouse();
                    Cursor = Cursors.SizeAll;
                }
            }
            else if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right)
            {
                _isPanning = true;
                _lastPan = pos;
                CaptureMouse();
                Cursor = Cursors.SizeAll;
            }
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            var world = ScreenToWorld(pos);
            MouseWorldPositionChanged?.Invoke(this, world);

            if (DrawingType != null && _drawStartWorld != null)
            {
                _drawCurrentWorld = world;
                RenderOverlay();
            }
            else if (_isPanning)
            {
                _originX += pos.X - _lastPan.X;
                _originY += pos.Y - _lastPan.Y;
                _lastPan = pos;
                Render();
            }
            e.Handled = true;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (DrawingType != null && _drawStartWorld != null && e.ChangedButton == MouseButton.Left)
            {
                var end = ScreenToWorld(e.GetPosition(this));
                var created = BuildTarget(_drawStartWorld.Value, end, DrawingType.Value);
                _drawStartWorld = null;
                ReleaseMouseCapture();
                RenderOverlay();
                if (created != null)
                {
                    Targets?.Add(created);
                    TargetCreated?.Invoke(this, created);
                }
            }
            else if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
                Cursor = DrawingType == null ? Cursors.Arrow : Cursors.Cross;
            }
            e.Handled = true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (_originX == 0 && _originY == 0) ResetView();
            else Render();
        }
        #endregion

        #region 构建目标（手动绘制结果）
        private TargetItem BuildTarget(Point a, Point b, TargetType type)
        {
            var t = new TargetItem { Type = type, Color = NewTargetColor };
            switch (type)
            {
                case TargetType.Point:
                    t.X = a.X; t.Y = a.Y; break;

                case TargetType.Line:
                case TargetType.Arrow:
                    t.X = a.X; t.Y = a.Y; t.X2 = b.X; t.Y2 = b.Y; break;

                case TargetType.Rectangle:
                case TargetType.Ellipse:
                case TargetType.Triangle:
                    t.X = (a.X + b.X) / 2; t.Y = (a.Y + b.Y) / 2;
                    t.Width = Math.Abs(b.X - a.X);
                    t.Height = Math.Abs(b.Y - a.Y);
                    break;

                case TargetType.Ring:
                    t.X = a.X; t.Y = a.Y;
                    t.Radius = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
                    t.InnerRadius = t.Radius * 0.6;
                    break;
            }
            t.Label = $"{type}";
            return t;
        }
        #endregion

        #region 命中测试
        private TargetItem HitTest(Point world)
        {
            if (Targets == null) return null;
            for (int i = Targets.Count - 1; i >= 0; i--)
                if (IsHit(Targets[i], world)) return Targets[i];
            return null;
        }

        private bool IsHit(TargetItem t, Point w)
        {
            double tol = 6 / Math.Min(_scaleX, _scaleY);
            double dx = w.X - t.X, dy = w.Y - t.Y;

            switch (t.Type)
            {
                case TargetType.Point:
                    return Math.Sqrt(dx * dx + dy * dy) <= tol + 4 / _scaleX;

                case TargetType.Line:
                case TargetType.Arrow:
                    return DistToSeg(w, new Point(t.X, t.Y), new Point(t.X2, t.Y2)) <= tol;

                case TargetType.Rectangle:
                case TargetType.Triangle:
                    return Math.Abs(dx) <= t.Width / 2 + tol && Math.Abs(dy) <= t.Height / 2 + tol;

                case TargetType.Ellipse:
                    if (t.Width <= 0 || t.Height <= 0) return false;
                    double nx = dx / (t.Width / 2), ny = dy / (t.Height / 2);
                    return nx * nx + ny * ny <= 1 + tol;

                case TargetType.Ring:
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    return r >= t.InnerRadius - tol && r <= t.Radius + tol;
            }
            return false;
        }

        private static double DistToSeg(Point p, Point a, Point b)
        {
            double vx = b.X - a.X, vy = b.Y - a.Y;
            double wx = p.X - a.X, wy = p.Y - a.Y;
            double len2 = vx * vx + vy * vy;
            if (len2 < 1e-12) return Math.Sqrt(wx * wx + wy * wy);
            double s = Math.Max(0, Math.Min(1, (wx * vx + wy * vy) / len2));
            double px = a.X + s * vx, py = a.Y + s * vy;
            return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
        }
        #endregion

        #region 辅助绘图
        private static void DrawArrowHead(DrawingContext dc, Point tip, double angle, Brush brush,
            double length, double width)
        {
            double a1 = angle + Math.PI - 0.35;
            double a2 = angle + Math.PI + 0.35;
            var p1 = new Point(tip.X + Math.Cos(a1) * length, tip.Y + Math.Sin(a1) * length);
            var p2 = new Point(tip.X + Math.Cos(a2) * length, tip.Y + Math.Sin(a2) * length);
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(tip, true, true);
                c.LineTo(p1, false, false);
                c.LineTo(p2, false, false);
            }
            g.Freeze();
            dc.DrawGeometry(brush, null, g);
        }

        private static Pen FrozenPen(Color c, double th)
        {
            var p = new Pen(new SolidColorBrush(c), th);
            p.Freeze();
            return p;
        }

        private FormattedText MakeText(string text, double size, Brush brush)
        {
            return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), size, brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private static string FormatTick(double value, double step)
        {
            int digits = Math.Max(0, (int)Math.Ceiling(-Math.Log10(step)) + 1);
            return value.ToString("F" + Math.Min(digits, 6));
        }

        /// <summary>自动挑选 1/2/5×10^n 的“好看”步长</summary>
        private static double NiceStep(double rough)
        {
            if (rough <= 0 || double.IsNaN(rough) || double.IsInfinity(rough)) return 1;
            double exp = Math.Floor(Math.Log10(rough));
            double baseVal = Math.Pow(10, exp);
            double m = rough / baseVal;
            double nice = m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10;
            return nice * baseVal;
        }
        #endregion

        #region 集合变更订阅
        private static void OnTargetsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (CoordinateV2Canvas)d;
            if (e.OldValue is INotifyCollectionChanged oldCol)
                oldCol.CollectionChanged -= c.OnTargetsCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newCol)
                newCol.CollectionChanged += c.OnTargetsCollectionChanged;

            c.RenderTargets();
        }

        private void OnTargetsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RenderTargets();
        }
        #endregion
    }

    public class CoordV2ViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<TargetItem> Targets { get; } = new();

        private TargetItem _selected;
        public TargetItem SelectedTarget
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                if (_selected != null) _selected.IsSelected = false;
                _selected = value;
                if (_selected != null) _selected.IsSelected = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
            }
        }

        public bool HasSelection => _selected != null;

        private Point _mouseWorld;
        public Point MouseWorld
        {
            get => _mouseWorld;
            set { _mouseWorld = value; OnPropertyChanged(); }
        }

        private TargetType? _drawingType;
        public TargetType? DrawingType
        {
            get => _drawingType;
            set { _drawingType = value; OnPropertyChanged(); }
        }

        public ICommand ClearCommand { get; }
        public RelayCommand<object> GenerateCommand { get; }
        public RelayCommand<object> SetDrawingTypeCommand { get; }
        public ICommand ResetViewCommand { get; }

        public event Action ResetViewRequested;

        public CoordV2ViewModel()
        {
            ClearCommand = new RelayCommand(() =>
            {
                Targets.Clear();
                SelectedTarget = null;
            });

            GenerateCommand = new RelayCommand<object>(p =>
            {
                if (int.TryParse(p?.ToString(), out int n)) Generate(n);
            });

            SetDrawingTypeCommand = new RelayCommand<object>(p =>
            {
                if (p == null || p.ToString() == "None") DrawingType = null;
                else if (Enum.TryParse<TargetType>(p.ToString(), out var t)) DrawingType = t;
            });

            ResetViewCommand = new RelayCommand(()=> ResetViewRequested?.Invoke());
        }

        /// <summary>批量生成 3~255 个随机目标</summary>
        public void Generate(int count)
        {
            count = Math.Max(3, Math.Min(255, count));
            Targets.Clear();

            var rnd = new Random(2024);
            var types = new[]
            {
                TargetType.Point, TargetType.Line, TargetType.Arrow,
                TargetType.Rectangle, TargetType.Ellipse, TargetType.Triangle, TargetType.Ring
            };

            for (int i = 0; i < count; i++)
            {
                var t = new TargetItem
                {
                    Type = types[i % types.Length],
                    X = rnd.NextDouble() * 18 - 9,
                    Y = rnd.NextDouble() * 18 - 9,
                    X2 = rnd.NextDouble() * 18 - 9,
                    Y2 = rnd.NextDouble() * 18 - 9,
                    Width = 0.6 + rnd.NextDouble() * 1.6,
                    Height = 0.6 + rnd.NextDouble() * 1.6,
                    Radius = 0.4 + rnd.NextDouble() * 0.8,
                    InnerRadius = 0.2 + rnd.NextDouble() * 0.4,
                    Label = $"T{i + 1:D3}",
                    Color = Color.FromRgb(
                        (byte)rnd.Next(40, 210),
                        (byte)rnd.Next(40, 210),
                        (byte)rnd.Next(40, 210))
                };
                Targets.Add(t);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
