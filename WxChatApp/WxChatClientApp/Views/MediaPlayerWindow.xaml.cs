using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using WxChatApp.Services;

namespace WxChatApp.Views;

/// <summary>音乐/视频播放窗口：时间线、播放暂停、音量控制。</summary>
public partial class MediaPlayerWindow : Window
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".flac"
    };

    private readonly MediaItem _item;
    private readonly DispatcherTimer _timer;
    private bool _ready;
    private bool _isPlaying;
    private bool _dragging;

    public MediaPlayerWindow(MediaItem item)
    {
        _item = item;
        InitializeComponent();
        TitleText.Text = string.IsNullOrWhiteSpace(item.FileName)
            ? Path.GetFileName(item.Url)
            : item.FileName;
        AudioName.Text = TitleText.Text;

        var ext = Path.GetExtension(TitleText.Text);
        if (string.IsNullOrEmpty(ext))
        {
            ext = Path.GetExtension(item.Url);
        }
        if (AudioExtensions.Contains(ext))
        {
            Player.Visibility = Visibility.Collapsed;
            AudioPanel.Visibility = Visibility.Visible;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => UpdatePosition();

        Progress.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => _dragging = true));
        Progress.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
        {
            _dragging = false;
            Seek(Progress.Value);
        }));
        Progress.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!_ready || e.OriginalSource is Thumb)
            {
                return;
            }
            var width = Progress.ActualWidth;
            if (width > 0)
            {
                Seek(e.GetPosition(Progress).X / width * Progress.Maximum);
            }
        };
        Volume.ValueChanged += (_, e) => Player.Volume = e.NewValue;
        Player.Volume = Volume.Value;

        Loaded += async (_, _) => await LoadAsync();
        Closed += (_, _) =>
        {
            _timer.Stop();
            try
            {
                Player.Stop();
                Player.Close();
            }
            catch
            {
                // 关闭时忽略播放器异常
            }
        };
    }

    private async Task LoadAsync()
    {
        try
        {
            var source = _item.Url;
            if (!source.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && !File.Exists(source))
            {
                var ext = Path.GetExtension(_item.FileName);
                if (string.IsNullOrEmpty(ext))
                {
                    ext = Path.GetExtension(new Uri(source).AbsolutePath);
                }
                var dir = Path.Combine(Path.GetTempPath(), "WxChatMedia");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "play_" + Guid.NewGuid().ToString("N") + ext);
                await AppServices.Api.DownloadFileAsync(source, path);
                source = path;
            }

            Player.Source = new Uri(source, UriKind.Absolute);
            Player.Play();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void Player_MediaOpened(object sender, RoutedEventArgs e)
    {
        _ready = true;
        LoadingPanel.Visibility = Visibility.Collapsed;
        if (Player.NaturalDuration.HasTimeSpan)
        {
            Progress.Maximum = Math.Max(1, Player.NaturalDuration.TimeSpan.TotalSeconds);
            TotalText.Text = Format(Player.NaturalDuration.TimeSpan);
        }
        Player.Volume = Volume.Value;
        Player.Play();
        _isPlaying = true;
        PlayGlyph.Text = "\uE769";
        PlayButton.ToolTip = Loc.T("Media.Pause");
        _timer.Start();
    }

    private void Player_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        ShowError(e.ErrorException?.Message ?? Loc.T("Media.LoadFail", ""));
    }

    private void Player_MediaEnded(object sender, RoutedEventArgs e)
    {
        _isPlaying = false;
        PlayGlyph.Text = "\uE768";
        PlayButton.ToolTip = Loc.T("Media.Play");
        _timer.Stop();
        UpdatePosition();
    }

    private void ShowError(string reason)
    {
        _ready = false;
        LoadingPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        ErrorText.Text = Loc.T("Media.LoadFail", reason);
        _timer.Stop();
    }

    private void UpdatePosition()
    {
        if (!_ready)
        {
            return;
        }
        var position = Player.Position;
        CurrentText.Text = Format(position);
        if (!_dragging && Progress.Maximum > 0)
        {
            Progress.Value = Math.Min(position.TotalSeconds, Progress.Maximum);
        }
    }

    private void Seek(double seconds)
    {
        if (!_ready)
        {
            return;
        }
        var value = Math.Clamp(seconds, 0, Progress.Maximum);
        Player.Position = TimeSpan.FromSeconds(value);
        CurrentText.Text = Format(Player.Position);
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e) => TogglePlay();

    private void Video_Click(object sender, MouseButtonEventArgs e) => TogglePlay();

    private void TogglePlay()
    {
        if (!_ready)
        {
            return;
        }
        if (_isPlaying)
        {
            Player.Pause();
            _isPlaying = false;
            PlayGlyph.Text = "\uE768";
            PlayButton.ToolTip = Loc.T("Media.Play");
        }
        else
        {
            Player.Play();
            _isPlaying = true;
            PlayGlyph.Text = "\uE769";
            PlayButton.ToolTip = Loc.T("Media.Pause");
            _timer.Start();
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.Space)
        {
            TogglePlay();
            e.Handled = true;
        }
    }

    private static string Format(TimeSpan value)
    {
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";
    }
}
