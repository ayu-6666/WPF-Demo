using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WxChatApp.Services;
using WxChatApp.ViewModels;

namespace WxChatApp.Views;

public partial class PublishMomentWindow : Window
{
    private const int MaxImages = 9;
    private static readonly string[] AllowedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    private readonly ObservableCollection<string> _images = new();
    private readonly MomentItemVm? _editing;

    public PublishMomentWindow() : this(null)
    {
    }

    public PublishMomentWindow(MomentItemVm? editing)
    {
        InitializeComponent();
        _editing = editing;
        ImageList.ItemsSource = _images;
        if (_editing != null)
        {
            HeaderText.Text = Loc.T("Publish.EditTitle");
            Title = Loc.T("Publish.EditTitle");
            ContentBox.Text = _editing.Content;
            PublishButton.Content = Loc.T("Common.Save");
            foreach (var url in _editing.Images)
            {
                _images.Add(url);
            }
        }
        UpdateCount();
        Loaded += (_, _) => ContentBox.Focus();
    }

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("Publish.ChooseImages"),
            Filter = Loc.T("Publish.ImageFilter"),
            Multiselect = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var rejected = new List<string>();
        foreach (var file in dialog.FileNames)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                rejected.Add(Path.GetFileName(file));
                continue;
            }
            if (_images.Count >= MaxImages)
            {
                ErrorText.Text = Loc.T("Publish.MaxImages", MaxImages);
                break;
            }
            if (!_images.Contains(file, StringComparer.OrdinalIgnoreCase))
            {
                _images.Add(file);
            }
        }

        if (rejected.Count > 0)
        {
            ErrorText.Text = Loc.T("Publish.UnsupportedFormat")
                + string.Join(Loc.T("Publish.NameSeparator"), rejected);
        }
        else if (_images.Count >= MaxImages)
        {
            ErrorText.Text = Loc.T("Publish.MaxImages", MaxImages);
        }
        else
        {
            ErrorText.Text = "";
        }
        UpdateCount();
    }

    private void RemoveImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string path })
        {
            _images.Remove(path);
        }
        ErrorText.Text = "";
        UpdateCount();
    }

    private void Preview_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string path })
        {
            var items = _images
                .Select(p => new MediaItem { FileName = Path.GetFileName(p), Url = p })
                .ToList();
            var start = _images.IndexOf(path);
            var viewer = new ViewerWindow(items, start < 0 ? 0 : start) { Owner = this };
            viewer.ShowDialog();
        }
    }

    private void ClearImages_Click(object sender, RoutedEventArgs e)
    {
        _images.Clear();
        ErrorText.Text = "";
        UpdateCount();
    }

    private void UpdateCount() => CountText.Text = Loc.T("Publish.Count", _images.Count, MaxImages);

    private async void Publish_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var content = ContentBox.Text.Trim();
        if (content.Length == 0 && _images.Count == 0)
        {
            ErrorText.Text = Loc.T("Publish.Empty");
            return;
        }
        PublishButton.IsEnabled = false;
        try
        {
            var urls = new List<string>();
            foreach (var file in _images)
            {
                urls.Add(file.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? file
                    : await AppServices.Api.UploadImageAsync(file));
            }
            if (_editing != null)
            {
                await AppServices.Api.UpdateMomentAsync(_editing.Id, content, urls);
            }
            else
            {
                await AppServices.Api.PublishMomentAsync(content, urls);
            }
            DialogResult = true;
        }
        catch (ApiException ex)
        {
            ErrorText.Text = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorText.Text = (_editing != null ? Loc.T("Publish.SaveFail") : Loc.T("Publish.PublishFail")) + ex.Message;
        }
        finally
        {
            PublishButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
