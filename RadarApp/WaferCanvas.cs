using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RadarApp
{
    public enum DieState
    {
        Default,   // 默认 (蓝)
        Selected,  // 选中 (红)
        Valid,     // 有效 (绿)
        Invalid    // 无效 (灰)
    }

    public class WaferCanvas : FrameworkElement
    {
        // 配置参数
        public double WaferSize { get; set; } = 12;
        public double DieWidth { get; set; } = 20;
        public double DieHeight { get; set; } = 20;
        public double ScribeGap { get; set; } = 4;

        // 状态记录
        private Dictionary<(int row, int col), DieState> _dieStates = new Dictionary<(int, int), DieState>();
        private List<(int row, int col)> _selectedDies = new List<(int, int)>();
        private int hoveredRow = -1;
        private int hoveredCol = -1;

        // 缩放与平移
        private double _scale = 1.0;
        private double _offsetX = 0;
        private double _offsetY = 0;

        // 坐标计算缓存
        private double _startX, _startY, _stepX, _stepY, _cx, _cy, _radius;
        private int _totalRows, _totalCols;

        // 框选
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private Point _dragEndPoint;

        // 悬浮索引改变事件 (Row, Col) 脱离时为 (-1, -1)
        public event Action<int, int> HoveredDieChanged;

        public WaferCanvas()
        {
            MouseMove += OnMouseMove;
            MouseDown += OnMouseDown;
            MouseUp += OnMouseUp;
            MouseWheel += OnMouseWheel;
            MouseLeave += OnMouseLeave;
            Focusable = true;
        }

        public void ClearSelection()
        {
            _selectedDies.Clear();
            foreach (var key in _dieStates.Keys.ToList())
                if (_dieStates[key] == DieState.Selected) _dieStates[key] = DieState.Default;
            InvalidateVisual();
        }

        public void SetSelectedState(DieState state)
        {
            foreach (var die in _selectedDies) _dieStates[die] = state;
            _selectedDies.Clear();
            InvalidateVisual();
        }

        public void ResetView()
        {
            _scale = 1.0;
            _offsetX = 0;
            _offsetY = 0;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            // 【关键修复1】裁剪超出控件边界的绘制内容 (防止缩放平移溢出)
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));

            dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (ActualWidth <= 0 || ActualHeight <= 0) { dc.Pop(); return; }

            // 应用变换矩阵
            dc.PushTransform(new TranslateTransform(_offsetX, _offsetY));
            dc.PushTransform(new ScaleTransform(_scale, _scale));

            _cx = ActualWidth / 2;
            _cy = ActualHeight / 2;
            double maxCanvasRadius = Math.Min(_cx, _cy) - 40;
            double pxPerInch = (maxCanvasRadius * 2) / WaferSize;
            _radius = (WaferSize * pxPerInch) / 2;

            dc.DrawEllipse(Brushes.LightGray, new Pen(Brushes.Gray, 2), new Point(_cx, _cy), _radius, _radius);

            _stepX = DieWidth + ScribeGap;
            _stepY = DieHeight + ScribeGap;
            int colsLeft = (int)Math.Ceiling(_radius / _stepX);
            int rowsTop = (int)Math.Ceiling(_radius / _stepY);
            _totalCols = colsLeft * 2;
            _totalRows = rowsTop * 2;
            _startX = _cx - colsLeft * _stepX;
            _startY = _cy - rowsTop * _stepY;

            for (int row = 0; row < _totalRows; row++)
            {
                for (int col = 0; col < _totalCols; col++)
                {
                    double x = _startX + col * _stepX;
                    double y = _startY + row * _stepY;
                    double dieCenterX = x + DieWidth / 2;
                    double dieCenterY = y + DieHeight / 2;

                    double dx = dieCenterX - _cx;
                    double dy = dieCenterY - _cy;
                    if (Math.Sqrt(dx * dx + dy * dy) <= _radius)
                    {
                        bool isHovered = (row == hoveredRow && col == hoveredCol);
                        DieState state = _dieStates.ContainsKey((row, col)) ? _dieStates[(row, col)] : DieState.Default;

                        Brush fillBrush = isHovered ? Brushes.Orange :
                            (state == DieState.Selected ? Brushes.Red :
                            (state == DieState.Valid ? Brushes.LimeGreen :
                            (state == DieState.Invalid ? Brushes.DarkGray : new SolidColorBrush(Color.FromRgb(97, 165, 194)))));

                        Pen strokePen = new Pen(isHovered ? Brushes.White : Brushes.White, isHovered ? 2 : 0.5);
                        dc.DrawRectangle(fillBrush, strokePen, new Rect(x, y, DieWidth, DieHeight));

                        if (DieWidth > 35 && DieHeight > 25)
                        {
                            var text = new FormattedText($"{row},{col}",
                                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                new Typeface("Arial"), 10,
                                state == DieState.Default ? Brushes.Black : Brushes.White,
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            dc.DrawText(text, new Point(dieCenterX - text.Width / 2, dieCenterY - text.Height / 2));
                        }
                    }
                }
            }

            // 绘制中心十字
            Pen crosshairPen = new Pen(Brushes.Red, 1);
            dc.DrawLine(crosshairPen, new Point(_cx - 10, _cy), new Point(_cx + 10, _cy));
            dc.DrawLine(crosshairPen, new Point(_cx, _cy - 10), new Point(_cx, _cy + 10));

            if (_isDragging)
            {
                Rect selectRect = new Rect(_dragStartPoint, _dragEndPoint);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(50, 0, 120, 215)), new Pen(Brushes.DodgerBlue, 1), selectRect);
            }

            dc.Pop(); // Scale
            dc.Pop(); // Translate
            dc.Pop(); // Clip (弹出裁剪)
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoom = e.Delta > 0 ? 1.15 : 1 / 1.15;
            double newScale = Math.Max(0.2, Math.Min(10, _scale * zoom));
            if (Math.Abs(newScale - _scale) < 0.001) return;

            Point mousePos = e.GetPosition(this);
            double logicalX = (mousePos.X - _offsetX) / _scale;
            double logicalY = (mousePos.Y - _offsetY) / _scale;

            _scale = newScale;
            _offsetX = mousePos.X - logicalX * _scale;
            _offsetY = mousePos.Y - logicalY * _scale;
            InvalidateVisual();
        }

        private Point ScreenToLogical(Point screenPoint) =>
            new Point((screenPoint.X - _offsetX) / _scale, (screenPoint.Y - _offsetY) / _scale);

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            Point logicalPos = ScreenToLogical(e.GetPosition(this));

            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _dragStartPoint = logicalPos;
                _dragEndPoint = logicalPos;
                double dx = logicalPos.X - _cx;
                double dy = logicalPos.Y - _cy;
                if (Math.Sqrt(dx * dx + dy * dy) > _radius) _isDragging = false;
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                UpdateHover(logicalPos); // 复用逻辑获取行列
                if (hoveredRow != -1 && hoveredCol != -1)
                {
                    _dieStates[(hoveredRow, hoveredCol)] = DieState.Default;
                    _selectedDies.Remove((hoveredRow, hoveredCol));
                    InvalidateVisual();
                }
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point logicalPos = ScreenToLogical(e.GetPosition(this));

            if (_isDragging)
            {
                _dragEndPoint = logicalPos;
                InvalidateVisual();
                return;
            }
            UpdateHover(logicalPos);
        }

        private void UpdateHover(Point logicalPos)
        {
            double dx = logicalPos.X - _cx;
            double dy = logicalPos.Y - _cy;
            if (Math.Sqrt(dx * dx + dy * dy) > _radius)
            {
                if (hoveredRow != -1) { hoveredRow = -1; hoveredCol = -1; HoveredDieChanged?.Invoke(-1, -1); InvalidateVisual(); }
                return;
            }

            int col = (int)Math.Floor((logicalPos.X - _startX) / _stepX);
            int row = (int)Math.Floor((logicalPos.Y - _startY) / _stepY);

            if (row >= 0 && row < _totalRows && col >= 0 && col < _totalCols)
            {
                double cellX = _startX + col * _stepX;
                double cellY = _startY + row * _stepY;
                if (logicalPos.X >= cellX && logicalPos.X <= cellX + DieWidth &&
                    logicalPos.Y >= cellY && logicalPos.Y <= cellY + DieHeight)
                {
                    if (hoveredRow != row || hoveredCol != col)
                    {
                        hoveredRow = row; hoveredCol = col;
                        // 【关键修复2】触发事件，传递当前悬浮的行列号
                        HoveredDieChanged?.Invoke(hoveredRow, hoveredCol);
                        InvalidateVisual();
                    }
                    return;
                }
            }
            if (hoveredRow != -1) { hoveredRow = -1; hoveredCol = -1; HoveredDieChanged?.Invoke(-1, -1); InvalidateVisual(); }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging || e.ChangedButton != MouseButton.Left) return;
            _isDragging = false;

            Point logicalStart = _dragStartPoint;
            Point logicalEnd = _dragEndPoint;

            if (Math.Abs(logicalStart.X - logicalEnd.X) < 2 && Math.Abs(logicalStart.Y - logicalEnd.Y) < 2)
            {
                int col = (int)Math.Floor((logicalStart.X - _startX) / _stepX);
                int row = (int)Math.Floor((logicalStart.Y - _startY) / _stepY);
                if (row >= 0 && row < _totalRows && col >= 0 && col < _totalCols)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (_selectedDies.Contains((row, col))) { _selectedDies.Remove((row, col)); _dieStates[(row, col)] = DieState.Default; }
                        else { _selectedDies.Add((row, col)); _dieStates[(row, col)] = DieState.Selected; }
                    }
                    else
                    {
                        _selectedDies.Clear();
                        foreach (var key in _dieStates.Keys.ToList()) if (_dieStates[key] == DieState.Selected) _dieStates[key] = DieState.Default;
                        _selectedDies.Add((row, col)); _dieStates[(row, col)] = DieState.Selected;
                    }
                }
            }
            else
            {
                double minX = Math.Min(logicalStart.X, logicalEnd.X), maxX = Math.Max(logicalStart.X, logicalEnd.X);
                double minY = Math.Min(logicalStart.Y, logicalEnd.Y), maxY = Math.Max(logicalStart.Y, logicalEnd.Y);
                _selectedDies.Clear();
                foreach (var key in _dieStates.Keys.ToList()) if (_dieStates[key] == DieState.Selected) _dieStates[key] = DieState.Default;

                for (int row = 0; row < _totalRows; row++)
                {
                    for (int col = 0; col < _totalCols; col++)
                    {
                        double cellCenterX = _startX + col * _stepX + DieWidth / 2;
                        double cellCenterY = _startY + row * _stepY + DieHeight / 2;
                        if (cellCenterX >= minX && cellCenterX <= maxX && cellCenterY >= minY && cellCenterY <= maxY)
                        {
                            _selectedDies.Add((row, col));
                            _dieStates[(row, col)] = DieState.Selected;
                        }
                    }
                }
            }
            InvalidateVisual();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (hoveredRow != -1 || hoveredCol != -1)
            {
                hoveredRow = -1; hoveredCol = -1;
                HoveredDieChanged?.Invoke(-1, -1); // 鼠标移出画布时重置
                InvalidateVisual();
            }
        }
    }
}
