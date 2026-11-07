using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WxChatApp.Services;

public sealed class StickerItem
{
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsBuiltin { get; init; }
}

/// <summary>
/// 本机表情包存储：%LOCALAPPDATA%\WxChatApp\stickers。
/// 首次运行时程序生成一批内置表情包 PNG（纯色底 + emoji 字形），
/// 自定义表情包通过"添加"复制进目录，删除只删文件本身。
/// </summary>
public static class StickerStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WxChatApp", "stickers");
    private static readonly string BuiltinDir = Path.Combine(Dir, "builtin");
    private static bool _builtinChecked;

    /// <summary>内置表情包定义：(文件名, emoji, 底色)。</summary>
    private static readonly (string File, string Emoji, string Color)[] BuiltIns =
    {
        ("s1", "😀", "#FFFEDE5B"),
        ("s2", "😂", "#FFF9C74F"),
        ("s3", "🥰", "#FFF8AFAF"),
        ("s4", "😎", "#FF90CDF4"),
        ("s5", "🤔", "#FFA0D8EF"),
        ("s6", "😭", "#FF90B8F8"),
        ("s7", "🥳", "#FFF6A6C1"),
        ("s8", "👍", "#FF95EC69"),
        ("s9", "🎉", "#FFCBA0F0"),
        ("s10", "🌹", "#FFF49F9F"),
        ("s11", "☕", "#FFC9A98C"),
        ("s12", "🍀", "#FF86D98F")
    };

    public static IReadOnlyList<StickerItem> LoadAll()
    {
        EnsureBuiltIns();
        var list = new List<StickerItem>();
        if (Directory.Exists(BuiltinDir))
        {
            foreach (var file in Directory.GetFiles(BuiltinDir, "*.png").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var name = BuiltIns.Any(b =>
                    fileName.Equals(b.File, StringComparison.OrdinalIgnoreCase))
                    ? Loc.T("Sticker." + fileName)
                    : fileName;
                list.Add(new StickerItem { Path = file, Name = name, IsBuiltin = true });
            }
        }
        if (Directory.Exists(Dir))
        {
            foreach (var file in Directory.GetFiles(Dir, "*.png")
                         .Concat(Directory.GetFiles(Dir, "*.jpg"))
                         .Concat(Directory.GetFiles(Dir, "*.jpeg"))
                         .Concat(Directory.GetFiles(Dir, "*.gif"))
                         .OrderBy(f => File.GetCreationTime(f)))
            {
                list.Add(new StickerItem
                {
                    Path = file,
                    Name = Path.GetFileNameWithoutExtension(file),
                    IsBuiltin = false
                });
            }
        }
        return list;
    }

    public static string AddFromFile(string sourcePath)
    {
        Directory.CreateDirectory(Dir);
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "sticker";
        }
        // 避免与内置目录/既有文件重名
        name = "user_" + string.Join("", name.Where(char.IsLetterOrDigit));
        if (name.Length > 24)
        {
            name = name[..24];
        }
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant() is { Length: > 1 } e ? e : ".png";
        var dest = Path.Combine(Dir, name + ext);
        var i = 1;
        while (File.Exists(dest))
        {
            dest = Path.Combine(Dir, $"{name}_{i++}{ext}");
        }
        File.Copy(sourcePath, dest);
        return dest;
    }

    public static void Delete(string path)
    {
        try
        {
            if (File.Exists(path) && path.StartsWith(Dir, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 文件被占用时删除失败忽略
        }
    }

    /// <summary>首次调用时生成内置表情包 PNG。</summary>
    private static void EnsureBuiltIns()
    {
        if (_builtinChecked)
        {
            return;
        }
        _builtinChecked = true;
        try
        {
            Directory.CreateDirectory(BuiltinDir);
            foreach (var (file, emoji, color) in BuiltIns)
            {
                var dest = Path.Combine(BuiltinDir, file + ".png");
                if (File.Exists(dest))
                {
                    continue;
                }
                RenderStickerPng(dest, emoji, color);
            }
        }
        catch
        {
            // 生成失败不影响聊天功能
        }
    }

    private static void RenderStickerPng(string destPath, string emoji, string bgColor)
    {
        const int size = 240;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bg = (Color)ColorConverter.ConvertFromString(bgColor);
            dc.DrawRoundedRectangle(new SolidColorBrush(bg), null,
                new Rect(4, 4, size - 8, size - 8), 36, 36);

            var typeface = new Typeface(new FontFamily("Segoe UI Emoji"), FontStyles.Normal,
                FontWeights.Normal, FontStretches.Normal);
            var ft = new FormattedText(emoji, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 132, Brushes.Black, 1.25);
            dc.DrawText(ft, new Point((size - ft.Width) / 2, (size - ft.Height) / 2 + 4));
        }
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(destPath);
        encoder.Save(fs);
    }
}
