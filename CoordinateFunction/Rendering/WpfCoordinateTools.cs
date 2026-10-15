using System.Globalization;
using System.Windows;
using System.Windows.Media;
using TVisionUpperDemp.Models;

namespace TVisionUpperDemp.Rendering;

public static class WpfCoordinateTools
{
    private static readonly Brush Background = new SolidColorBrush(Color.FromRgb(9, 17, 29));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromRgb(27, 45, 67)), 1);
    private static readonly Pen AxisPen = new(new SolidColorBrush(Color.FromRgb(76, 164, 232)), 2);

    public static void DrawScene(DrawingContext dc, DrawingDocument document, Size size)
    {
        dc.DrawRectangle(Background, null, new Rect(size));
        DrawCoordinateSystem(dc, document, size);

        foreach (var shape in document.Shapes)
        {
            DrawShape(dc, document, shape, size);
        }
    }

    public static void DrawCoordinateSystem(DrawingContext dc, DrawingDocument document, Size size)
    {
        var transform = document.Transform;
        var origin = transform.ToScreen(new Point(), size);
        var step = Math.Clamp(50 * transform.Scale, 18, 90);

        for (var x = origin.X % step; x < size.Width; x += step)
        {
            dc.DrawLine(GridPen, new Point(x, 0), new Point(x, size.Height));
        }

        for (var y = origin.Y % step; y < size.Height; y += step)
        {
            dc.DrawLine(GridPen, new Point(0, y), new Point(size.Width, y));
        }

        dc.DrawLine(AxisPen, new Point(0, origin.Y), new Point(size.Width, origin.Y));
        dc.DrawLine(AxisPen, new Point(origin.X, 0), new Point(origin.X, size.Height));
        DrawArrow(dc, origin, new Point(size.Width, origin.Y), AxisPen);
        DrawArrow(dc, origin, new Point(0, origin.Y), AxisPen);
        DrawArrow(dc, origin, new Point(origin.X, 0), AxisPen);
        DrawArrow(dc, origin, new Point(origin.X, size.Height), AxisPen);

        for (var value = -30; value <= 30; value++)
        {
            var point = transform.ToScreen(new Point(value * 50, 0), size);
            if (point.X < 0 || point.X > size.Width)
            {
                continue;
            }

            dc.DrawLine(GridPen, new Point(point.X, origin.Y - 4), new Point(point.X, origin.Y + 4));
            if (value != 0)
            {
                dc.DrawText(new FormattedText(
                    (value * 50).ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    10,
                    Brushes.Gray,
                    1), new Point(point.X + 3, origin.Y + 5));
            }
        }

        dc.DrawText(new FormattedText(
            "O (0, 0)", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"), 11, Brushes.White, 1), origin + new Vector(8, 8));
    }

    public static void DrawShape(DrawingContext dc, DrawingDocument document, DrawShape shape, Size size)
    {
        var point = document.Transform.ToScreen(shape.Position, size);
        var stroke = new Pen(shape.Stroke ?? new SolidColorBrush(Color.FromRgb(77, 205, 185)), 2);
        var fill = shape.Fill ?? new SolidColorBrush(Color.FromArgb(45, 77, 205, 185));
        var radius = shape.Size * document.Transform.Scale;

        switch (shape.Kind)
        {
            case ShapeKind.Text:
                dc.DrawText(new FormattedText(
                    $"{shape.Tag}\n({shape.Position.X:0}, {shape.Position.Y:0})",
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 14, Brushes.White, 1), point);
                break;
            case ShapeKind.Rectangle:
                dc.DrawRoundedRectangle(fill, stroke,
                    new Rect(point.X - radius, point.Y - radius / 2, radius * 2, radius), 6, 6);
                break;
            case ShapeKind.Triangle:
                dc.DrawGeometry(fill, stroke, Polygon(new[]
                {
                    new Point(point.X, point.Y - radius),
                    new Point(point.X - radius, point.Y + radius),
                    new Point(point.X + radius, point.Y + radius)
                }));
                break;
            case ShapeKind.Polygon:
                dc.DrawGeometry(fill, stroke, Polygon(new[]
                {
                    new Point(point.X, point.Y - radius),
                    new Point(point.X + radius, point.Y - radius / 3),
                    new Point(point.X + radius / 2, point.Y + radius),
                    new Point(point.X - radius / 2, point.Y + radius),
                    new Point(point.X - radius, point.Y - radius / 3)
                }));
                break;
            case ShapeKind.Line:
            case ShapeKind.Arrow:
                var end = new Point(point.X + radius * 1.8, point.Y - radius * .7);
                dc.DrawLine(stroke, point, end);
                if (shape.Kind == ShapeKind.Arrow)
                {
                    DrawArrow(dc, point, end, stroke);
                }
                break;
            case ShapeKind.Arc:
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    context.BeginFigure(new Point(point.X - radius, point.Y), false, false);
                    context.ArcTo(
                        new Point(point.X + radius, point.Y),
                        new Size(radius, radius),
                        0,
                        false,
                        SweepDirection.Clockwise,
                        true,
                        false);
                }
                dc.DrawGeometry(null, stroke, geometry);
                break;
            case ShapeKind.Ring:
                dc.DrawEllipse(null,
                    new Pen(stroke.Brush, Math.Max(2, 8 * document.Transform.Scale)),
                    point, radius, radius);
                break;
            case ShapeKind.Ellipse:
                dc.DrawEllipse(fill, stroke, point, radius * 1.2, radius * .7);
                break;
        }

        dc.DrawText(new FormattedText(
            shape.Tag, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 11, Brushes.LightGray, 1),
            point + new Vector(radius + 5, -radius - 5));
    }

    private static void DrawArrow(DrawingContext dc, Point start, Point end, Pen pen)
    {
        var direction = end - start;
        if (direction.LengthSquared < double.Epsilon)
        {
            return;
        }

        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var basePoint = end - direction * 12;
        dc.DrawLine(pen, end, basePoint + normal * 5);
        dc.DrawLine(pen, end, basePoint - normal * 5);
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
}
