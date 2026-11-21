using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WxChatApp.Helpers;

/// <summary>用 WPF MediaPlayer 抓取网络视频的首帧，失败时由界面回退到占位封面。</summary>
public static class MediaThumbnailLoader
{
    private static readonly Dictionary<string, BitmapSource?> Cache = new();
    private static readonly HashSet<string> Pending = new();
    private static readonly object Gate = new();

    public static void Request(string url, Action<ImageSource?> onReady)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            onReady(null);
            return;
        }

        lock (Gate)
        {
            if (Cache.TryGetValue(url, out var cached))
            {
                onReady(cached);
                return;
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            onReady(null);
            return;
        }
        dispatcher.BeginInvoke(new Action(() => Start(url, onReady)), DispatcherPriority.Background);
    }

    private static void Start(string url, Action<ImageSource?> onReady)
    {
        lock (Gate)
        {
            // 同一地址只抓一次，避免同屏多条消息重复解码
            if (!Pending.Add(url))
            {
                onReady(null);
                return;
            }
        }

        MediaPlayer? player = null;
        DispatcherTimer? timer = null;
        var finished = false;
        var attempts = 0;

        void Finish(BitmapSource? bitmap)
        {
            if (finished)
            {
                return;
            }
            finished = true;
            try { timer?.Stop(); } catch { /* ignore */ }
            try { player?.Close(); } catch { /* ignore */ }
            lock (Gate)
            {
                Cache[url] = bitmap;
                Pending.Remove(url);
            }
            onReady(bitmap);
        }

        try
        {
            player = new MediaPlayer { Volume = 0, ScrubbingEnabled = true };
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            var deadline = DateTime.UtcNow.AddSeconds(8);

            timer.Tick += (_, _) =>
            {
                try
                {
                    if (DateTime.UtcNow > deadline)
                    {
                        Finish(null);
                        return;
                    }
                    attempts++;
                    if (attempts == 2)
                    {
                        player.Pause();
                    }
                    if (attempts == 3)
                    {
                        player.Position = TimeSpan.FromSeconds(0.3);
                    }
                    if (attempts >= 6 && player.NaturalVideoWidth > 0)
                    {
                        var width = player.NaturalVideoWidth;
                        var height = player.NaturalVideoHeight;
                        var scale = Math.Min(1.0, 480.0 / Math.Max(width, height));
                        var w = Math.Max(1, (int)(width * scale));
                        var h = Math.Max(1, (int)(height * scale));

                        var visual = new DrawingVisual();
                        using (var dc = visual.RenderOpen())
                        {
                            dc.DrawVideo(player, new Rect(0, 0, w, h));
                        }
                        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(visual);
                        bitmap.Freeze();
                        Finish(bitmap);
                    }
                }
                catch
                {
                    Finish(null);
                }
            };

            player.MediaOpened += (_, _) =>
            {
                try
                {
                    player.Play();
                }
                catch
                {
                    Finish(null);
                }
            };
            player.MediaFailed += (_, _) => Finish(null);
            player.Open(new Uri(url, UriKind.Absolute));
            timer.Start();
        }
        catch
        {
            Finish(null);
        }
    }
}
