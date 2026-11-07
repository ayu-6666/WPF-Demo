using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WxChatApp.Helpers;
using WxChatApp.Services;

namespace WxChatApp.Views;

public partial class ViewerWindow : Window
{
    private readonly List<MediaItem> _items;
    private int _index;
    private bool _isDragging;
    private bool _videoPlaying;
    private Point _dragStart;
    private Point _translateStart;

    public bool HasMultiple => _items.Count > 1;
    public int TotalCount => _items.Count;

    public ViewerWindow(IEnumerable<MediaItem> items, int startIndex = 0)
    {
        InitializeComponent();
        _items = items.ToList();
        _index = Math.Clamp(startIndex, 0, _items.Count - 1);
        DataContext = this;
        Loaded += (_, _) =>
        {
            ShowCurrent();
            Focus();
        };
        Closed += (_, _) =>
        {
            // 关闭后把焦点还原给主窗口，避免键盘事件落在已关闭的预览窗口
            if (Owner != null)
            {
                Owner.Activate();
            }
        };
    }

    private void ShowCurrent()
    {
        if (_items.Count == 0)
        {
            Close();
            return;
        }
        ResetTransforms();
        ErrorPanel.Visibility = Visibility.Collapsed;
        var item = _items[_index];
        InfoText.Text = $"{_index + 1} / {_items.Count}   {item.FileName}";

        PrevBtn.IsEnabled = _index > 0;
        NextBtn.IsEnabled = _index < _items.Count - 1;

        var isVideo = IsVideoUrl(item.Url);
        PreviewImage.Visibility = isVideo ? Visibility.Collapsed : Visibility.Visible;
        PreviewVideo.Visibility = isVideo ? Visibility.Visible : Visibility.Collapsed;
        VideoHint.Visibility = Visibility.Collapsed;

        if (isVideo)
        {
            _videoPlaying = true;
            try
            {
                PreviewVideo.Source = new Uri(item.Url, UriKind.Absolute);
                PreviewVideo.Play();
            }
            catch (Exception ex)
            {
                ShowError(Loc.T("Viewer.VideoLoadFail") + ex.Message);
            }
        }
        else
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.UriSource = new Uri(item.Url, UriKind.Absolute);
                bi.DownloadFailed += (_, _) => ShowError(Loc.T("Viewer.ImageDownloadFail"));
                bi.EndInit();
                PreviewImage.Source = bi;
            }
            catch (Exception ex)
            {
                ShowError(Loc.T("Viewer.ImageLoadFail") + ex.Message);
            }
        }
    }

    private void ShowError(string message)
    {
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewVideo.Visibility = Visibility.Collapsed;
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private static bool IsVideoUrl(string url)
    {
        var ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
        return ext is ".mp4" or ".wmv" or ".avi" or ".mov" or ".webm";
    }

    private void ResetTransforms()
    {
        ImageScale.ScaleX = 1;
        ImageScale.ScaleY = 1;
        ImageTranslate.X = 0;
        ImageTranslate.Y = 0;
    }

    // ============ 图片缩放/拖拽 ============

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (PreviewImage.Visibility != Visibility.Visible) return;
        var factor = e.Delta > 0 ? 1.15 : 0.87;
        var newScale = Math.Clamp(ImageScale.ScaleX * factor, 0.2, 8.0);
        ImageScale.ScaleX = newScale;
        ImageScale.ScaleY = newScale;
        e.Handled = true;
    }

    private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(this);
        _translateStart = new Point(ImageTranslate.X, ImageTranslate.Y);
        PreviewImage.CaptureMouse();
    }

    private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        PreviewImage.ReleaseMouseCapture();
    }

    private void Image_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var p = e.GetPosition(this);
        ImageTranslate.X = _translateStart.X + (p.X - _dragStart.X);
        ImageTranslate.Y = _translateStart.Y + (p.Y - _dragStart.Y);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        var s = Math.Clamp(ImageScale.ScaleX * 1.25, 0.2, 8.0);
        ImageScale.ScaleX = s; ImageScale.ScaleY = s;
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        var s = Math.Clamp(ImageScale.ScaleX * 0.8, 0.2, 8.0);
        ImageScale.ScaleX = s; ImageScale.ScaleY = s;
    }

    private void ResetZoom_Click(object sender, RoutedEventArgs e) => ResetTransforms();

    // ============ 视频 ============

    private void Video_Click(object sender, MouseButtonEventArgs e)
    {
        if (_videoPlaying)
        {
            PreviewVideo.Pause();
            _videoPlaying = false;
            ShowVideoHint(Loc.T("Viewer.Paused"));
        }
        else
        {
            PreviewVideo.Play();
            _videoPlaying = true;
            VideoHint.Visibility = Visibility.Collapsed;
        }
    }

    private void Video_MediaOpened(object sender, RoutedEventArgs e)
    {
        _videoPlaying = true;
        ShowVideoHint(Loc.T("Viewer.ClickHint"));
    }

    private void Video_MediaEnded(object sender, RoutedEventArgs e)
    {
        _videoPlaying = false;
        ShowVideoHint(Loc.T("Viewer.PlayEnded"));
    }

    private void Video_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _videoPlaying = false;
        ShowError(Loc.T("Viewer.VideoLoadFail") + e.ErrorException.Message);
    }

    private void ShowVideoHint(string text)
    {
        VideoHint.Text = text;
        VideoHint.Visibility = Visibility.Visible;
    }

    // ============ 导航 ============

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_index > 0) { _index--; ShowCurrent(); }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_index < _items.Count - 1) { _index++; ShowCurrent(); }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.Left: Prev_Click(sender, e); break;
            case Key.Right: Next_Click(sender, e); break;
            case Key.Add or Key.OemPlus: ZoomIn_Click(sender, e); break;
            case Key.Subtract or Key.OemMinus: ZoomOut_Click(sender, e); break;
            case Key.D0: ResetZoom_Click(sender, e); break;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>点击遮罩层（非图片/非按钮区域）关闭预览。</summary>
    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Grid grid && ReferenceEquals(grid, RootGrid))
        {
            Close();
        }
    }

    // ============ 另存为 ============

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0) return;
        var item = _items[_index];
        var dlg = new SaveFileDialog
        {
            FileName = item.FileName,
            Filter = IsVideoUrl(item.Url) ? Loc.T("Viewer.VideoFilter") : Loc.T("Viewer.SaveImgFilter")
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var http = new HttpClient();
            var bytes = await http.GetByteArrayAsync(item.Url);
            File.WriteAllBytes(dlg.FileName, bytes);
            Dialogs.Info(this, Loc.T("Viewer.SaveOk") + dlg.FileName, Loc.T("Viewer.SaveOkTitle"));
        }
        catch (Exception ex)
        {
            Dialogs.Error(this, Loc.T("Viewer.SaveFail") + ex.Message, Loc.T("Viewer.SaveFailTitle"));
        }
    }
}

/// <summary>被预览的媒体项（图片或视频）。</summary>
public sealed class MediaItem
{
    public string Url { get; init; } = "";
    public string FileName { get; init; } = "";
}
