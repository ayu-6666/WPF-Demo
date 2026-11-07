using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WxChatApp.Helpers;
using WxChatApp.Services;
using WxChatApp.Views;
using WxChatApp.ViewModels;

namespace WxChatApp.Views;

public partial class MomentsPane : UserControl
{
    public MomentsPane()
    {
        InitializeComponent();
        AppBackground.Changed += ApplyBackground;
        MomentsCoverStore.Changed += ApplyCover;
        Loaded += (_, _) =>
        {
            ApplyBackground();
            // 启动时校验封面路径：失效/损坏则回退默认并提示
            var coverWarning = MomentsCoverStore.ValidateConfigured();
            ApplyCover();
            if (coverWarning != null)
            {
                Vm?.Notify(coverWarning);
            }
        };
    }

    /// <summary>应用封面图（只影响 Row0 封面，不影响下方动态列表背景）。</summary>
    private void ApplyCover()
    {
        var image = MomentsCoverStore.Current;
        var hasImage = image != null;
        MomentsCoverImage.Source = image;
        MomentsCoverImage.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        MomentsCoverShade.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        CoverDefault.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;
        CoverDecor.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Cover_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = Loc.T("Moments.SetCoverTitle"),
            Filter = Loc.T("Cover.Filter"),
            CheckFileExists = true
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }
        if (!MomentsCoverStore.TrySet(dlg.FileName, out var error))
        {
            Dialogs.Warning(Window.GetWindow(this), error, Loc.T("Moments.SetCoverFail"));
            return;
        }
        ApplyCover();
        Vm?.Notify(Loc.T("Moments.CoverUpdated"));
    }

    private void ResetCover_Click(object sender, RoutedEventArgs e)
    {
        MomentsCoverStore.Reset();
        ApplyCover();
        Vm?.Notify(Loc.T("Moments.CoverReset"));
    }

    /// <summary>应用朋友圈时间线背景；无背景时恢复默认白底。</summary>
    private void ApplyBackground()
    {
        var image = AppBackground.Current;
        var hasImage = image != null;
        MomentsBgImage.Source = image;
        MomentsBgImage.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        MomentsBgOverlay.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        Background = hasImage ? System.Windows.Media.Brushes.Transparent : System.Windows.Media.Brushes.White;
    }

    private MomentsViewModel? Vm => DataContext as MomentsViewModel;

    private async void Publish_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        var dialog = new PublishMomentWindow
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true)
        {
            await Vm.LoadAsync();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (Vm != null) await Vm.LoadAsync();
    }

    private async void DeleteMoment_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null || sender is not FrameworkElement { DataContext: MomentItemVm moment })
        {
            return;
        }
        var owner = Window.GetWindow(this);
        if (!Dialogs.Confirm(owner, Loc.T("Moments.DeleteConfirm"), Loc.T("Moments.DeleteTitle")))
        {
            return;
        }
        if (await Vm.DeleteAsync(moment))
        {
            Vm.Notify(Loc.T("Moments.Deleted"));
        }
    }

    private async void EditMoment_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null || sender is not FrameworkElement { DataContext: MomentItemVm moment })
        {
            return;
        }
        var dialog = new PublishMomentWindow(moment)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true)
        {
            await Vm.LoadAsync();
            Vm.Notify(Loc.T("Moments.Edited"));
        }
    }

    private void Image_Click(object sender, MouseButtonEventArgs e)
    {
        var image = sender as Image;
        if (image?.DataContext is not string currentUrl) return;
        // 向上爬找到 MomentItemVm（UniformGrid 的父级 DataTemplate 根元素）
        var walker = image.Parent;
        List<string>? allUrls = null;
        while (walker != null)
        {
            if (walker is FrameworkElement fe && fe.DataContext is MomentItemVm moment)
            {
                allUrls = moment.Images?.ToList();
                break;
            }
            walker = (walker as DependencyObject) is { } dep ? System.Windows.Media.VisualTreeHelper.GetParent(dep) : null;
        }
        if (allUrls == null || allUrls.Count == 0) return;

        var index = allUrls.IndexOf(currentUrl);
        if (index < 0) index = 0;

        var items = allUrls.Select(url => new MediaItem
        {
            Url = url,
            FileName = Path.GetFileName(new Uri(url).AbsolutePath)
        }).ToList();

        var owner = Window.GetWindow(this);
        var viewer = new ViewerWindow(items, index) { Owner = owner };
        viewer.ShowDialog();
    }
}
