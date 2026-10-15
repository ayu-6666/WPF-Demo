using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Tang.RadarApplication.Models;

namespace Tang.RadarApplication.ViewModels
{
    public class CoordinateCanvas : FrameworkElement
    {
        private readonly VisualCollection _visuals;
        private readonly DrawingVisual _drawingVisual;

/// <summary>
/// 依赖属性：范围、单位与自定义刻度值
/// </summary>
        public static readonly DependencyProperty XMinProperty = DependencyProperty.Register(nameof(XMin), typeof(double), typeof(CoordinateCanvas), new FrameworkPropertyMetadata(-100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPropertyChanged));
        public static readonly DependencyProperty XMaxProperty = DependencyProperty.Register(nameof(XMax), typeof(double), typeof(CoordinateCanvas), new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPropertyChanged));
        public static readonly DependencyProperty YMinProperty = DependencyProperty.Register(nameof(YMin), typeof(double), typeof(CoordinateCanvas), new FrameworkPropertyMetadata(-100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPropertyChanged));
        public static readonly DependencyProperty YMaxProperty = DependencyProperty.Register(nameof(YMax), typeof(double), typeof(CoordinateCanvas), new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPropertyChanged));

        public static readonly DependencyProperty XUnitProperty = DependencyProperty.Register(nameof(XUnit), typeof(string), typeof(CoordinateCanvas), new FrameworkPropertyMetadata("X", OnPropertyChanged));
        public static readonly DependencyProperty YUnitProperty = DependencyProperty.Register(nameof(YUnit), typeof(string), typeof(CoordinateCanvas), new FrameworkPropertyMetadata("Y", OnPropertyChanged));

        // 自定义刻度值（若为0或NaN则自动计算）
        public static readonly DependencyProperty CustomTickStepXProperty = DependencyProperty.Register(nameof(CustomTickStepX), typeof(double), typeof(CoordinateCanvas), new FrameworkPropertyMetadata(0.0, OnPropertyChanged));
        public static readonly DependencyProperty CustomTickStepYProperty = DependencyProperty.Register(nameof(CustomTickStepY), typeof(double), typeof(CoordinateCanvas), new FrameworkPropertyMetadata(0.0, OnPropertyChanged));

        public static readonly DependencyProperty ShapesProperty = DependencyProperty.Register(nameof(Shapes), typeof(IEnumerable<ShapeModel>), typeof(CoordinateCanvas), new FrameworkPropertyMetadata(null, OnShapesChanged));
        public static readonly DependencyProperty SelectedShapeProperty = DependencyProperty.Register(nameof(SelectedShape), typeof(ShapeModel), typeof(CoordinateCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedShapeChanged));

        public double XMin { get => (double)GetValue(XMinProperty); set => SetValue(XMinProperty, value); }
        public double XMax { get => (double)GetValue(XMaxProperty); set => SetValue(XMaxProperty, value); }
        public double YMin { get => (double)GetValue(YMinProperty); set => SetValue(YMinProperty, value); }
        public double YMax { get => (double)GetValue(YMaxProperty); set => SetValue(YMaxProperty, value); }
        public string XUnit { get => (string)GetValue(XUnitProperty); set => SetValue(XUnitProperty, value); }
        public string YUnit { get => (string)GetValue(YUnitProperty); set => SetValue(YUnitProperty, value); }
        public double CustomTickStepX { get => (double)GetValue(CustomTickStepXProperty); set => SetValue(CustomTickStepXProperty, value); }
        public double CustomTickStepY { get => (double)GetValue(CustomTickStepYProperty); set => SetValue(CustomTickStepYProperty, value); }
        public IEnumerable<ShapeModel> Shapes { get => (IEnumerable<ShapeModel>)GetValue(ShapesProperty); set => SetValue(ShapesProperty, value); }
        public ShapeModel SelectedShape { get => (ShapeModel)GetValue(SelectedShapeProperty); set => SetValue(SelectedShapeProperty, value); }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CoordinateCanvas)d).Redraw();

        private static void OnShapesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (CoordinateCanvas)d;
            if (e.OldValue is INotifyCollectionChanged oldCol) oldCol.CollectionChanged -= canvas.OnCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newCol) newCol.CollectionChanged += canvas.OnCollectionChanged;
            canvas.Redraw();
        }

        private static void OnSelectedShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 选中项改变时触发实时重绘以高亮显示
            ((CoordinateCanvas)d).Redraw();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 监听集合内部数据变化或重新赋值时实时重绘
            if (e.NewItems != null)
            {
                foreach (ShapeModel item in e.NewItems) item.PropertyChanged += OnItemPropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (ShapeModel item in e.OldItems) item.PropertyChanged -= OnItemPropertyChanged;
            }
            Redraw();
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) => Redraw();

        private bool _isPanning;
        private Point _lastMousePos;

        public CoordinateCanvas()
        {
            _visuals = new VisualCollection(this);
            _drawingVisual = new DrawingVisual();
            _visuals.Add(_drawingVisual);
            ClipToBounds = true;
        }

        protected override int VisualChildrenCount => _visuals.Count;
        protected override Visual GetVisualChild(int index) => _visuals[index];
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) => Redraw();

        public Point LogicToScreen(Point p)
        {
            double x = (p.X - XMin) / (XMax - XMin) * ActualWidth;
            double y = ActualHeight - (p.Y - YMin) / (YMax - YMin) * ActualHeight;
            return new Point(x, y);
        }

        public Point ScreenToLogic(Point p)
        {
            double x = p.X / ActualWidth * (XMax - XMin) + XMin;
            double y = (ActualHeight - p.Y) / ActualHeight * (YMax - YMin) + YMin;
            return new Point(x, y);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            _isPanning = true;
            _lastMousePos = e.GetPosition(this);
            CaptureMouse();

            // 鼠标点击命中目标，同步选中状态并在左下角联动显示详情
            Point clickScreen = e.GetPosition(this);
            SelectedShape = HitTestShape(clickScreen);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _isPanning = false;
            ReleaseMouseCapture();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isPanning) return;

            Point currentPos = e.GetPosition(this);
            Vector diff = currentPos - _lastMousePos;
            _lastMousePos = currentPos;

            double scaleX = (XMax - XMin) / ActualWidth;
            double scaleY = (YMax - YMin) / ActualHeight;

            XMin -= diff.X * scaleX;
            XMax -= diff.X * scaleX;
            YMin += diff.Y * scaleY;
            YMax += diff.Y * scaleY;

            Redraw();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            Point mousePos = e.GetPosition(this);
            Point logicPos = ScreenToLogic(mousePos);

            double factor = e.Delta > 0 ? 0.9 : 1.1;
            double w = (XMax - XMin) * factor;
            double h = (YMax - YMin) * factor;

            double rx = mousePos.X / ActualWidth;
            double ry = (ActualHeight - mousePos.Y) / ActualHeight;

            XMin = logicPos.X - w * rx;
            XMax = logicPos.X + w * (1 - rx);
            YMin = logicPos.Y - h * ry;
            YMax = logicPos.Y + h * (1 - ry);

            Redraw();
        }

        public void Redraw()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            using (DrawingContext dc = _drawingVisual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));
                DrawGridAndAxes(dc);

                if (Shapes != null)
                {
                    foreach (var shape in Shapes)
                    {
                        bool isSelected = (shape == SelectedShape);
                        DrawShape(dc, shape, isSelected);
                    }
                }
            }
        }

        private void DrawGridAndAxes(DrawingContext dc)
        {
            Pen gridPen = new Pen(new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)), 0.5);
            Pen axisPen = new Pen(Brushes.Black, 1.5);

            Point origin = LogicToScreen(new Point(0, 0));

            // 1. 绘制正负方向坐标轴及箭头
            if (YMin <= 0 && YMax >= 0)
            {
                dc.DrawLine(axisPen, new Point(0, origin.Y), new Point(ActualWidth, origin.Y));
                DrawArrow(dc, axisPen, new Point(ActualWidth - 10, origin.Y - 4), new Point(ActualWidth, origin.Y), new Point(ActualWidth - 10, origin.Y + 4));
                DrawText(dc, XUnit, ActualWidth - 35, origin.Y - 20);
            }
            if (XMin <= 0 && XMax >= 0)
            {
                dc.DrawLine(axisPen, new Point(origin.X, 0), new Point(origin.X, ActualHeight));
                DrawArrow(dc, axisPen, new Point(origin.X - 4, 10), new Point(origin.X, 0), new Point(origin.X + 4, 10));
                DrawText(dc, YUnit, origin.X + 10, 5);
            }

            // 2. X轴刻度计算（支持用户自定义刻度值）
            double stepX = CustomTickStepX > 0 ? CustomTickStepX : CalculateAutoStep(XMax - XMin);
            for (double x = Math.Ceiling(XMin / stepX) * stepX; x <= XMax; x += stepX)
            {
                if (Math.Abs(x) < 1e-5) continue;
                Point sp = LogicToScreen(new Point(x, 0));
                if (YMin <= 0 && YMax >= 0)
                {
                    dc.DrawLine(gridPen, new Point(sp.X, 0), new Point(sp.X, ActualHeight));
                    dc.DrawLine(axisPen, sp, new Point(sp.X, origin.Y + 4));
                    DrawText(dc, x.ToString("G4"), sp.X - 12, origin.Y + 6);
                }
            }

            // 3. Y轴刻度计算（支持用户自定义刻度值）
            double stepY = CustomTickStepY > 0 ? CustomTickStepY : CalculateAutoStep(YMax - YMin);
            for (double y = Math.Ceiling(YMin / stepY) * stepY; y <= YMax; y += stepY)
            {
                if (Math.Abs(y) < 1e-5) continue;
                Point sp = LogicToScreen(new Point(0, y));
                if (XMin <= 0 && XMax >= 0)
                {
                    dc.DrawLine(gridPen, new Point(0, sp.Y), new Point(ActualWidth, sp.Y));
                    dc.DrawLine(axisPen, sp, new Point(origin.X - 4, sp.Y));
                    DrawText(dc, y.ToString("G4"), origin.X - 38, sp.Y - 8);
                }
            }
        }

        private double CalculateAutoStep(double range)
        {
            double roughStep = range / 10.0;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
            double norm = roughStep / magnitude;
            if (norm < 1.5) return 1.0 * magnitude;
            if (norm < 3.5) return 2.0 * magnitude;
            if (norm < 7.5) return 5.0 * magnitude;
            return 10.0 * magnitude;
        }

        private void DrawShape(DrawingContext dc, ShapeModel shape, bool isSelected)
        {
            var strokeColor = isSelected ? Colors.Red : shape.StrokeColor;
            var strokeBrush = new SolidColorBrush(strokeColor);
            var fillBrush = new SolidColorBrush(shape.FillColor);
            var pen = new Pen(strokeBrush, isSelected ? shape.StrokeThickness + 2 : shape.StrokeThickness);

            switch (shape.Type)
            {
                case ShapeType.Point:
                    if (shape.Points.Count > 0)
                    {
                        Point pt = LogicToScreen(shape.Points[0]);
                        dc.DrawEllipse(strokeBrush, null, pt, isSelected ? 7 : 4, isSelected ? 7 : 4);
                        DrawText(dc, shape.Text, pt.X + 8, pt.Y - 8, isSelected ? Colors.Red : Colors.DarkSlateGray);
                    }
                    break;
                case ShapeType.Line:
                case ShapeType.ArrowLine:
                    if (shape.Points.Count >= 2)
                    {
                        Point p1 = LogicToScreen(shape.Points[0]);
                        Point p2 = LogicToScreen(shape.Points[1]);
                        dc.DrawLine(pen, p1, p2);
                        if (shape.Type == ShapeType.ArrowLine) DrawArrowHead(dc, pen, p1, p2);
                        DrawText(dc, shape.Text, (p1.X + p2.X) / 2 + 5, (p1.Y + p2.Y) / 2 - 15);
                    }
                    break;
                case ShapeType.Rectangle:
                    if (shape.Points.Count >= 2)
                    {
                        Rect r = new Rect(LogicToScreen(shape.Points[0]), LogicToScreen(shape.Points[1]));
                        dc.DrawRectangle(fillBrush, pen, r);
                        DrawText(dc, shape.Text, r.X + 5, r.Y + 5);
                    }
                    break;
                case ShapeType.Ellipse:
                    if (shape.Points.Count >= 2)
                    {
                        Rect r = new Rect(LogicToScreen(shape.Points[0]), LogicToScreen(shape.Points[1]));
                        dc.DrawEllipse(fillBrush, pen, new Point(r.X + r.Width / 2, r.Y + r.Height / 2), r.Width / 2, r.Height / 2);
                        DrawText(dc, shape.Text, r.X + 5, r.Y + 5);
                    }
                    break;
                case ShapeType.Ring:
                    if (shape.Points.Count >= 2)
                    {
                        Point center = LogicToScreen(shape.Points[0]);
                        double rOuter = Math.Abs(LogicToScreen(shape.Points[1]).X - center.X);
                        dc.DrawEllipse(fillBrush, pen, center, rOuter, rOuter);
                        dc.DrawEllipse(Brushes.White, pen, center, rOuter * 0.6, rOuter * 0.6);
                        DrawText(dc, shape.Text, center.X, center.Y - rOuter - 15);
                    }
                    break;
            }
        }

        private ShapeModel HitTestShape(Point screenPos)
        {
            if (Shapes == null) return null;
            foreach (var shape in Shapes)
            {
                if (shape.Points.Count > 0)
                {
                    Point pt = LogicToScreen(shape.Points[0]);
                    if (Math.Abs(pt.X - screenPos.X) < 12 && Math.Abs(pt.Y - screenPos.Y) < 12)
                    {
                        return shape;
                    }
                }
            }
            return null;
        }

        private void DrawArrow(DrawingContext dc, Pen pen, Point p1, Point p2, Point p3)
        {
            StreamGeometry geom = new StreamGeometry();
            using (var c = geom.Open()) { c.BeginFigure(p1, true, true); c.LineTo(p2, true, true); c.LineTo(p3, true, true); }
            geom.Freeze();
            dc.DrawGeometry(pen.Brush, pen, geom);
        }

        private void DrawArrowHead(DrawingContext dc, Pen pen, Point p1, Point p2)
        {
            Vector v = p2 - p1; v.Normalize();
            Vector n = new Vector(-v.Y, v.X);
            DrawArrow(dc, pen, p2 - v * 10 + n * 5, p2, p2 - v * 10 - n * 5);
        }

        private void DrawText(DrawingContext dc, string text, double x, double y, Color? color = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 11, new SolidColorBrush(color ?? Colors.Black), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }
    public class CoordinateViewModel : DependencyObject
    {
        public double XMin { get => (double)GetValue(XMinProperty); set => SetValue(XMinProperty, value); }
        public static readonly DependencyProperty XMinProperty = DependencyProperty.Register(nameof(XMin), typeof(double), typeof(CoordinateViewModel), new PropertyMetadata(-100.0));

        public double XMax { get => (double)GetValue(XMaxProperty); set => SetValue(XMaxProperty, value); }
        public static readonly DependencyProperty XMaxProperty = DependencyProperty.Register(nameof(XMax), typeof(double), typeof(CoordinateViewModel), new PropertyMetadata(100.0));

        public double YMin { get => (double)GetValue(YMinProperty); set => SetValue(YMinProperty, value); }
        public static readonly DependencyProperty YMinProperty = DependencyProperty.Register(nameof(YMin), typeof(double), typeof(CoordinateViewModel), new PropertyMetadata(-100.0));

        public double YMax { get => (double)GetValue(YMaxProperty); set => SetValue(YMaxProperty, value); }
        public static readonly DependencyProperty YMaxProperty = DependencyProperty.Register(nameof(YMax), typeof(double), typeof(CoordinateViewModel), new PropertyMetadata(100.0));

        // 自定义刻度值属性（绑定到界面输入框或预设值）
        public double CustomTickStepX { get => (double)GetValue(CustomTickStepXProperty); set => SetValue(CustomTickStepXProperty, value); }
        public static readonly DependencyProperty CustomTickStepXProperty = DependencyProperty.Register(nameof(CustomTickStepX), typeof(double), typeof(CoordinateViewModel), new PropertyMetadata(20.0));

        public double CustomTickStepY { get => (double)GetValue(CustomTickStepYProperty); set => SetValue(CustomTickStepYProperty, value); }
        public static readonly DependencyProperty CustomTickStepYProperty = DependencyProperty.Register(nameof(CustomTickStepY), typeof(double), typeof(CoordinateViewModel), new PropertyMetadata(20.0));

        public ObservableCollection<ShapeModel> Shapes { get; set; } = new ObservableCollection<ShapeModel>();

        private ShapeModel _selectedShape;
        public ShapeModel SelectedShape
        {
            get => _selectedShape;
            set
            {
                _selectedShape = value;
                // 实时通知属性更新，驱动左下角绑定文本即刻刷新
                OnPropertyChanged(nameof(SelectedShape));
            }
        }

        public RelayCommand<object> GenerateCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand UpdateSelectedTextCommand { get; }

        public CoordinateViewModel()
        {
            GenerateCommand = new RelayCommand<object>(p => GenerateShapes(Convert.ToInt32(p)));
            ClearCommand = new RelayCommand(() => { Shapes.Clear(); SelectedShape = null; });
            UpdateSelectedTextCommand = new RelayCommand(() => {
                if (SelectedShape != null)
                {
                    SelectedShape.Text = "已激活目标"; // 测试实时重绘与更新
                }
            });

            GenerateShapes(50); // 默认生成 50 个目标
        }

        public void GenerateShapes(int count)
        {
            Shapes.Clear();
            Random rand = new Random();

            for (int i = 0; i < count; i++)
            {
                double x = rand.Next(-80, 80);
                double y = rand.Next(-80, 80);
                ShapeType type = (ShapeType)(i % 6);

                var shape = new ShapeModel
                {
                    Id = i + 1,
                    Type = type,
                    Text = $"T_{i + 1}",
                    StrokeColor = Color.FromRgb((byte)rand.Next(30, 200), (byte)rand.Next(30, 200), 230)
                };

                shape.Points.Add(new Point(x, y));
                shape.Points.Add(new Point(x + rand.Next(10, 25), y + rand.Next(10, 25)));

                Shapes.Add(shape);
            }
        }
/// <summary>
/// INotifyPropertyChanged 简单实现
/// </summary>
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}
