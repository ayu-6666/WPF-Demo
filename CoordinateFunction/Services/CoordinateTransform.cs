using System.Windows;
using TVisionUpperDemp.Models;

namespace TVisionUpperDemp.Services;

public sealed class CoordinateTransform
{
    public double Scale { get; private set; } = 1;
    public Vector Offset { get; private set; }
    public AxisMode AxisMode { get; set; } = AxisMode.FourQuadrants;

    public Point ToScreen(Point world, Size size)
    {
        var origin = GetOrigin(size);
        return new(origin.X + Offset.X + world.X * Scale, origin.Y + Offset.Y - world.Y * Scale);
    }

    public Point ToWorld(Point screen, Size size)
    {
        var origin = GetOrigin(size);
        return new((screen.X - origin.X - Offset.X) / Scale, -(screen.Y - origin.Y - Offset.Y) / Scale);
    }

    public void ZoomAt(Point screen, double factor, Size size)
    {
        var worldPoint = ToWorld(screen, size);
        Scale = Math.Clamp(Scale * factor, 0.15, 8);
        Offset += screen - ToScreen(worldPoint, size);
    }

    public void Pan(Vector delta) => Offset += delta;

    public void Reset()
    {
        Scale = 1;
        Offset = new Vector();
    }

    private Point GetOrigin(Size size) => AxisMode switch
    {
        AxisMode.FirstQuadrant => new Point(0, size.Height),
        AxisMode.SecondQuadrant => new Point(size.Width, size.Height),
        _ => new Point(size.Width / 2, size.Height / 2)
    };
}
