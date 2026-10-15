using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TVisionUpperDemp.Models;

namespace TVisionUpperDemp.Rendering;

public interface IRenderSurface
{
    UIElement Element { get; }
    void Refresh();
}

public interface IRenderPage : IRenderSurface
{
}

public abstract class RenderPageBase : UserControl, IRenderPage
{
    protected RenderPageBase(UIElement surface)
    {
        Content = surface;
        Background = new SolidColorBrush(Color.FromRgb(9, 17, 29));
    }

    public UIElement Element => this;

    public abstract void Refresh();
}

public abstract class InteractiveRenderSurface : FrameworkElement, IRenderSurface
{
    protected readonly DrawingDocument Document;
    private Point? _dragStart;

    protected InteractiveRenderSurface(DrawingDocument document)
    {
        Document = document;
        Focusable = true;
        ClipToBounds = true;
    }

    public UIElement Element => this;

    public abstract void Refresh();

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        Document.Transform.ZoomAt(
            e.GetPosition(this),
            e.Delta > 0 ? 1.15 : 0.87,
            RenderSize);

        Refresh();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragStart is not Point start)
        {
            return;
        }

        var current = e.GetPosition(this);
        Document.Transform.Pan(current - start);
        _dragStart = current;
        Refresh();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _dragStart = null;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        var position = Document.Transform.ToWorld(e.GetPosition(this), RenderSize);
        Document.Shapes.Add(new DrawShape(
            ShapeKind.Ellipse,
            position,
            $"Tag-{DateTime.Now:HHmmss}",
            42));

        Refresh();
        e.Handled = true;
    }
}
