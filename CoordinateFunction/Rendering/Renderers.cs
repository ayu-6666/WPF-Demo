using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using TVisionUpperDemp.Models;

namespace TVisionUpperDemp.Rendering;

public sealed class FrameworkElementSurface : InteractiveRenderSurface
{
    public FrameworkElementSurface(DrawingDocument document)
        : base(document)
    {
    }

    public override void Refresh() => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        WpfCoordinateTools.DrawScene(drawingContext, Document, RenderSize);
    }
}

public sealed class DrawingVisualSurface : InteractiveRenderSurface
{
    private readonly DrawingVisual _visual = new();

    public DrawingVisualSurface(DrawingDocument document)
        : base(document)
    {
        AddVisualChild(_visual);
        AddLogicalChild(_visual);
    }

    public override void Refresh()
    {
        using var context = _visual.RenderOpen();
        WpfCoordinateTools.DrawScene(context, Document, RenderSize);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _visual;
}

public sealed class CanvasSurface : Canvas, IRenderSurface
{
    private readonly FrameworkElementSurface _renderer;

    public CanvasSurface(DrawingDocument document)
    {
        Background = new SolidColorBrush(Color.FromRgb(9, 17, 29));
        _renderer = new FrameworkElementSurface(document)
        {
            IsHitTestVisible = false
        };

        Children.Add(_renderer);
        SizeChanged += (_, _) => ResizeRenderer();
    }

    public UIElement Element => this;

    public void Refresh()
    {
        ResizeRenderer();
        _renderer.Refresh();
    }

    private void ResizeRenderer()
    {
        _renderer.Width = ActualWidth;
        _renderer.Height = ActualHeight;
    }
}

public sealed class SkiaSharpSurface : SKElement, IRenderSurface
{
    private readonly DrawingDocument _document;

    public SkiaSharpSurface(DrawingDocument document)
    {
        _document = document;
        PaintSurface += OnPaintSurface;
    }

    public UIElement Element => this;

    public void Refresh() => InvalidateVisual();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs args)
    {
        var canvas = args.Surface.Canvas;
        canvas.Clear(new SKColor(9, 17, 29));

        var size = new Size(args.Info.Width, args.Info.Height);
        var origin = _document.Transform.ToScreen(new Point(), size);

        using var axisPen = new SKPaint
        {
            Color = new SKColor(76, 164, 232),
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        canvas.DrawLine(0, (float)origin.Y, args.Info.Width, (float)origin.Y, axisPen);
        canvas.DrawLine((float)origin.X, 0, (float)origin.X, args.Info.Height, axisPen);

        foreach (var shape in _document.Shapes)
        {
            var point = _document.Transform.ToScreen(shape.Position, size);
            var radius = (float)(shape.Size * _document.Transform.Scale);

            using var shapePen = new SKPaint
            {
                Color = new SKColor(77, 205, 185),
                StrokeWidth = 2,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            canvas.DrawCircle((float)point.X, (float)point.Y, Math.Max(3, radius), shapePen);

            using var textPen = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 13,
                IsAntialias = true
            };

            canvas.DrawText(
                shape.Tag,
                (float)point.X + radius + 5,
                (float)point.Y - radius,
                textPen);
        }
    }
}

public sealed class WriteableBitmapSurface : Image, IRenderSurface
{
    private readonly DrawingDocument _document;

    public WriteableBitmapSurface(DrawingDocument document)
    {
        _document = document;
        Stretch = Stretch.Fill;
        SizeChanged += (_, _) => Refresh();
    }

    public UIElement Element => this;

    public void Refresh()
    {
        if (ActualWidth < 1 || ActualHeight < 1)
        {
            return;
        }

        var width = Math.Max(1, (int)ActualWidth);
        var height = Math.Max(1, (int)ActualHeight);
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            WpfCoordinateTools.DrawScene(context, _document, new Size(width, height));
        }

        var source = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        source.Render(visual);

        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
        var pixels = new byte[width * height * 4];
        source.CopyPixels(pixels, width * 4, 0);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        Source = bitmap;
    }
}

public static class RendererFactory
{
    public static IRenderPage CreatePage(RendererKind kind, DrawingDocument document)
    {
        IRenderSurface surface = kind switch
        {
            RendererKind.Canvas => new CanvasSurface(document),
            RendererKind.FrameworkElement => new FrameworkElementSurface(document),
            RendererKind.DrawingVisual => new DrawingVisualSurface(document),
            RendererKind.SkiaSharp => new SkiaSharpSurface(document),
            RendererKind.WriteableBitmap => new WriteableBitmapSurface(document),
            _ => new FrameworkElementSurface(document)
        };

        return new RendererPage(surface);
    }
}

public sealed class RendererPage : RenderPageBase
{
    private readonly IRenderSurface _surface;

    public RendererPage(IRenderSurface surface)
        : base(surface.Element)
    {
        _surface = surface;
    }

    public override void Refresh() => _surface.Refresh();
}
