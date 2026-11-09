using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GenaralDBTools.Models;
using GenaralDBTools.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GenaralDBTools;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;

        ConnectionTree.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(TreeItem_Expanded));
        SourceInitialized += (_, _) => ApplyDarkTitleBar();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void ApplyDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;

        var enabled = 1;
        // 20 为 Windows 10 1809+，19 兼容更早版本；失败时保持系统默认标题栏。
        if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void MainWindow_Closed(object sender, EventArgs e)
    {
        await _viewModel.ShutdownAsync();
    }

    private void ConnectionTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _viewModel.SelectNode(e.NewValue as DatabaseNode);
    }

    private void TreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item && item.DataContext is DatabaseNode node)
            _ = _viewModel.ExpandNodeAsync(node);
    }

    private void ConnectionTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (IsInsideExpander(e.OriginalSource as DependencyObject)) return;
        if (ConnectionTree.SelectedItem is DatabaseNode node)
            _ = _viewModel.OpenNodeAsync(node);
    }

    private static bool IsInsideExpander(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current != null)
        {
            if (current is ToggleButton) return true;
            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return false;
    }
}
