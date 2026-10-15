using System.Globalization;
using System.Windows;
using System.Windows.Media;
using TVisionUpperDemp.Models;

namespace TVisionUpperDemp.Rendering;

// Compatibility drawing helper retained for older callers.
public static class WpfDrawingTools
{
    public static void DrawScene(DrawingContext drawingContext, DrawingDocument document, Size size)
    {
        WpfCoordinateTools.DrawScene(drawingContext, document, size);
    }

    public static void DrawAxes(DrawingContext drawingContext, DrawingDocument document, Size size)
    {
        WpfCoordinateTools.DrawCoordinateSystem(drawingContext, document, size);
    }

    public static void DrawShape(DrawingContext drawingContext, DrawingDocument document, DrawShape shape, Size size)
    {
        WpfCoordinateTools.DrawShape(drawingContext, document, shape, size);
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

    private static void DrawArrow(DrawingContext drawingContext, Point start, Point end, Pen pen)
    {
        var direction = end - start;
        if (direction.LengthSquared < double.Epsilon)
        {
            return;
        }

        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var basePoint = end - direction * 12;
        drawingContext.DrawLine(pen, end, basePoint + normal * 5);
        drawingContext.DrawLine(pen, end, basePoint - normal * 5);
    }
}
