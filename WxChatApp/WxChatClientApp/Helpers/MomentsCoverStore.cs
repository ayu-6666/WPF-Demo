using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WxChatApp.Services;

namespace WxChatApp.Helpers;

/// <summary>
/// 朋友圈顶部封面图（只影响封面上半部分，不参与下方动态列表的背景）。
/// 图片丢失或解码失败时自动回退到默认渐变封面。
/// </summary>
public static class MomentsCoverStore
{
    public static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

    private static string _loadedPath = "";
    private static ImageSource? _image;

    /// <summary>封面变化后通知界面重新应用。</summary>
    public static event Action? Changed;

    public static string ConfiguredPath => LocalStore.Settings.MomentsCover;

    public static ImageSource? Current
    {
        get
        {
            var path = ConfiguredPath;
            if (path.Length == 0)
            {
                _image = null;
                _loadedPath = "";
                return null;
            }
            if (_image != null && _loadedPath == path)
            {
                return _image;
            }
            _image = Load(path);
            _loadedPath = path;
            return _image;
        }
    }

    public static bool IsSupported(string path) =>
        File.Exists(path) &&
        AllowedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>校验并保存新的封面图；失败时返回可读原因且不改动配置。</summary>
    public static bool TrySet(string path, out string error)
    {
        error = "";
        if (!File.Exists(path))
        {
            error = Loc.T("Image.FileNotFound", path);
            return false;
        }
        if (!IsSupported(path))
        {
            error = Loc.T("Cover.Unsupported");
            return false;
        }
        if (Load(path) == null)
        {
            error = Loc.T("Image.DecodeFail");
            return false;
        }
        LocalStore.Settings.MomentsCover = path;
        LocalStore.Save();
        _image = null;
        _loadedPath = "";
        Changed?.Invoke();
        return true;
    }

    /// <summary>恢复默认渐变封面。</summary>
    public static void Reset()
    {
        LocalStore.Settings.MomentsCover = "";
        LocalStore.Save();
        _image = null;
        _loadedPath = "";
        Changed?.Invoke();
    }

    /// <summary>启动进入朋友圈时校验已保存的封面；失效则清空配置并返回原因。</summary>
    public static string? ValidateConfigured()
    {
        var path = ConfiguredPath;
        if (path.Length == 0)
        {
            return null;
        }
        if (IsSupported(path) && Load(path) != null)
        {
            return null;
        }
        LocalStore.Settings.MomentsCover = "";
        LocalStore.Save();
        _image = null;
        _loadedPath = "";
        return Loc.T("Cover.Invalid");
    }

    private static ImageSource? Load(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
