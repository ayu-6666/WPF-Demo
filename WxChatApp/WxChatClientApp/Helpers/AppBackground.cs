using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WxChatApp.Services;

namespace WxChatApp.Helpers;

/// <summary>
/// 聊天/朋友圈自定义背景（全局统一一张图）。图片解码失败或文件丢失时自动回退默认背景。
/// </summary>
public static class AppBackground
{
    public static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

    private static string _loadedPath = "";
    private static ImageSource? _image;

    /// <summary>背景变化后通知各界面重新应用（设置里切换 / 恢复默认时触发）。</summary>
    public static event Action? Changed;

    public static string ConfiguredPath => LocalStore.Settings.BackgroundImage;

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
            if (!ReferenceEquals(_image, null) && _loadedPath == path)
            {
                return _image;
            }
            _image = Load(path);
            _loadedPath = path;
            return _image;
        }
    }

    public static bool HasImage => Current != null;

    public static bool IsSupported(string path) =>
        File.Exists(path) &&
        AllowedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>校验并保存新的背景图；失败时返回可读原因且不改动配置。</summary>
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
            error = Loc.T("Image.Unsupported");
            return false;
        }
        if (Load(path) == null)
        {
            error = Loc.T("Image.DecodeFail");
            return false;
        }
        LocalStore.Settings.BackgroundImage = path;
        LocalStore.Save();
        _image = null;
        _loadedPath = "";
        Changed?.Invoke();
        return true;
    }

    public static void Reset()
    {
        LocalStore.Settings.BackgroundImage = "";
        LocalStore.Save();
        _image = null;
        _loadedPath = "";
        Changed?.Invoke();
    }

    /// <summary>丢弃已解码的缓存并通知界面重新应用（用于“取消”回滚或路径变化）。</summary>
    public static void Refresh()
    {
        _image = null;
        _loadedPath = "";
        Changed?.Invoke();
    }

    /// <summary>
    /// 启动/进入界面时校验已保存的背景图；失效则清空配置并返回原因。
    /// </summary>
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
        LocalStore.Settings.BackgroundImage = "";
        LocalStore.Save();
        _image = null;
        _loadedPath = "";
        return Loc.T("Background.Invalid");
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
