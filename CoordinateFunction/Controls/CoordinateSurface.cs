using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TVisionUpperDemp.Models;
using TVisionUpperDemp.Services;

namespace TVisionUpperDemp.Controls;

// Compatibility control retained for existing callers. The main window uses the renderer pages.
public sealed class CoordinateSurface : FrameworkElement
{
    public List<DrawShape> Shapes { get; } = new();
    public int RenderMode { get; set; }

    private readonly CoordinateTransform _transform = new();
    private Point? _dragStart;
    private int _serial;

    public CoordinateSurface()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public void AddDemoShape(ShapeKind kind)
    {
        var index = Shapes.Count;
        Shapes.Add(new DrawShape(
            kind,
            new Point(-220 + index % 6 * 85, 150 - index / 6 * 90),
            $"{kind}-{++_serial}",
            42 + index % 3 * 12));
        InvalidateVisual();
    }

    public void ResetView()
    {
        _transform.Reset();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(
            new SolidColorBrush(Color.FromRgb(11, 17, 27)),
            null,
            new Rect(RenderSize));

        var pen = new Pen(new SolidColorBrush(Color.FromRgb(29, 44, 63)), 1);
        var origin = _transform.ToScreen(new Point(), RenderSize);
        var step = Math.Clamp(50 * _transform.Scale, 18, 90);

        for (var x = origin.X % step; x < ActualWidth; x += step)
        {
            drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
        }

        for (var y = origin.Y % step; y < ActualHeight; y += step)
        {
            drawingContext.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
        }

        var axis = new Pen(new SolidColorBrush(Color.FromRgb(107, 160, 218)), 2);
        drawingContext.DrawLine(axis, new Point(0, origin.Y), new Point(ActualWidth, origin.Y));
        drawingContext.DrawLine(axis, new Point(origin.X, 0), new Point(origin.X, ActualHeight));

        foreach (var shape in Shapes)
        {
            var point = _transform.ToScreen(shape.Position, RenderSize);
            var radius = shape.Size * _transform.Scale;
            var stroke = new Pen(shape.Stroke ?? Brushes.MediumTurquoise, 2);
            var fill = shape.Fill ?? new SolidColorBrush(Color.FromArgb(40, 74, 194, 180));

            switch (shape.Kind)
            {
                case ShapeKind.Rectangle:
                    drawingContext.DrawRoundedRectangle(
                        fill, stroke,
                        new Rect(point.X - radius, point.Y - radius / 2, radius * 2, radius), 5, 5);
                    break;
                case ShapeKind.Ellipse:
                    drawingContext.DrawEllipse(fill, stroke, point, radius * 1.2, radius * .7);
                    break;
                case ShapeKind.Ring:
                    drawingContext.DrawEllipse(null, new Pen(stroke.Brush, Math.Max(2, radius / 8)), point, radius, radius);
                    break;
                case ShapeKind.Triangle:
                    drawingContext.DrawGeometry(fill, stroke, Polygon(new[]
                    {
                        new Point(point.X, point.Y - radius),
                        new Point(point.X - radius, point.Y + radius),
                        new Point(point.X + radius, point.Y + radius)
                    }));
                    break;
                default:
                    drawingContext.DrawText(
                        new FormattedText(shape.Tag, System.Globalization.CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Brushes.White, 1), point);
                    break;
            }
        }
    }

    private static StreamGeometry Polygon(IEnumerable<Point> points)
    {
        var values = points.ToList();
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(values[0], true, true);
        context.PolyLineTo(values, true, true);
        return geometry;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        _transform.ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1.15 : .87, RenderSize);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragStart is Point start)
        {
            var current = e.GetPosition(this);
            _transform.Pan(current - start);
            _dragStart = current;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _dragStart = null;
        ReleaseMouseCapture();
    }
}
