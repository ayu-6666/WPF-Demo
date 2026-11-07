using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WxChatApp.Helpers;

public static class AvatarHelper
{
    private static readonly string[] Palette =
    {
        "#4C9BD6", "#5FB878", "#D4824C", "#8E72C7", "#C96B7A",
        "#3FA9A0", "#6A8FC7", "#C29A4B", "#7A9A5B", "#B06A9E"
    };

    private static readonly Dictionary<string, SolidColorBrush> Cache = new();
    private static readonly Dictionary<string, BitmapImage> ImgCache = new();
    private static readonly FontFamily IconFont = new("Segoe MDL2 Assets");

    public static SolidColorBrush BrushFor(string key)
    {
        var hash = 0;
        foreach (var ch in key)
        {
            hash = (hash * 31 + ch) & 0x7FFFFFFF;
        }
        var color = Palette[hash % Palette.Length];
        lock (Cache)
        {
            if (!Cache.TryGetValue(color, out var brush))
            {
                brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                brush.Freeze();
                Cache[color] = brush;
            }
            return brush;
        }
    }

    public static string Initial(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }
        var trimmed = name.Trim();
        return trimmed.Length >= 1 ? trimmed[..1].ToUpperInvariant() : "?";
    }

    public static FontFamily Icons => IconFont;

    /// <summary>少数字符图标的矢量字体名。</summary>
    public static string Glyph(string code) => code;

    /// <summary>是否有自定义头像 URL。</summary>
    public static bool HasAvatar(string? url) => !string.IsNullOrWhiteSpace(url);

    /// <summary>清空头像图片缓存（资料/头像变更后调用，避免继续显示旧图）。</summary>
    public static void ClearCache()
    {
        lock (ImgCache)
        {
            ImgCache.Clear();
        }
    }

    private static readonly System.Net.Http.HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    /// <summary>从 URL 加载 BitmapImage（带内存缓存，UI 线程外调用返回冻结图片）。</summary>
    public static BitmapImage? FromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        lock (ImgCache)
        {
            if (ImgCache.TryGetValue(url, out var cached)) return cached;
        }
        try
        {
            // 先下载字节再解码：避免 WPF 按 URI 异步下载时的时序问题，
            // 同时绕过 WPF 全局图片缓存，保证头像更新后不会显示旧图。
            var bytes = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
            using var stream = new MemoryStream(bytes);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = stream;
            bi.EndInit();
            bi.Freeze();
            lock (ImgCache)
            {
                ImgCache[url] = bi;
            }
            return bi;
        }
        catch
        {
            // 头像下载失败时退回文字/纯色头像，不阻断界面
            return null;
        }
    }
}
