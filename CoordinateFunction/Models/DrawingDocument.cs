using System.Windows;
using System.Windows.Media;
using TVisionUpperDemp.Services;

namespace TVisionUpperDemp.Models;

public enum ShapeKind
{
    Text, Rectangle, Triangle, Polygon, Line, Arrow, Arc, Ring, Ellipse
}

public enum AxisMode
{
    FourQuadrants, FirstQuadrant, SecondQuadrant
}

public enum RendererKind
{
    Canvas, FrameworkElement, DrawingVisual, SkiaSharp, WriteableBitmap
}

public sealed record DrawShape(
    ShapeKind Kind,
    Point Position,
    string Tag,
    double Size = 55,
    Brush? Stroke = null,
    Brush? Fill = null);

public sealed class DrawingDocument
{
    private int _serial;

    public List<DrawShape> Shapes { get; } = new();
    public CoordinateTransform Transform { get; } = new();

    public void Add(ShapeKind kind)
    {
        var index = Shapes.Count;
        var position = new Point(-180 + index % 5 * 85, 140 - index / 5 * 85);
        Shapes.Add(new DrawShape(kind, position, $"{kind}-{++_serial}", 38 + index % 3 * 10));
    }
}
