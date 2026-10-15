using System.Windows;
using System.Windows.Controls;
using TVisionUpperDemp.Models;
using TVisionUpperDemp.Rendering;

namespace TVisionUpperDemp;

public partial class MainWindow : Window
{
    private readonly DrawingDocument _document = new();
    private IRenderPage? _page;

    public MainWindow()
    {
        InitializeComponent();
        CreatePage(RendererKind.Canvas);
    }

    private void Renderer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string name } &&
            Enum.TryParse(name, out RendererKind kind))
        {
            CreatePage(kind);
        }
    }

    private void CoordinateMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || CoordinateModeBox.SelectedIndex < 0)
        {
            return;
        }

        _document.Transform.AxisMode = (AxisMode)CoordinateModeBox.SelectedIndex;
        _page?.Refresh();
        UpdateStatus();
    }

    private void CreatePage(RendererKind kind)
    {
        _page = RendererFactory.CreatePage(kind, _document);
        SurfaceHost.Content = _page.Element;
        RendererLabel.Text = kind.ToString();
        _page.Refresh();
        UpdateStatus();
    }

    private void AddShape_Click(object sender, RoutedEventArgs e)
    {
        var kind = (ShapeKind)Math.Max(0, ShapeBox.SelectedIndex);
        _document.Add(kind);
        _page?.Refresh();
        UpdateStatus($"已添加 {_document.Shapes[^1].Tag}");
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _document.Shapes.Clear();
        _page?.Refresh();
        UpdateStatus("已清空图形");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _document.Transform.Reset();
        _page?.Refresh();
        UpdateStatus("已重置视图");
    }

    private void UpdateStatus(string message = "就绪")
    {
        Status.Text = $"{message} · {RendererLabel.Text} · {CoordinateModeBox.SelectedItem} · 图形 {_document.Shapes.Count} · 缩放 {_document.Transform.Scale:0.00}x";
    }
}
