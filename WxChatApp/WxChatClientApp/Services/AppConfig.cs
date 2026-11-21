using System.IO;
using System.Text.Json;

namespace WxChatApp.Services;

public static class AppConfig
{
    public const string DefaultServerBaseUrl = "http://127.0.0.1:5210";
    /// <summary>单个上传文件的最大字节数（与服务端保持一致）。</summary>
    public const long MaxUploadBytes = 200L * 1024 * 1024;

    /// <summary>是否由环境变量锁定服务端地址（锁定后设置页不可修改）。</summary>
    public static bool IsServerUrlLocked =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WXCHAT_SERVER"));

    /// <summary>服务端地址：环境变量 WXCHAT_SERVER 优先，其次使用本机保存的地址。</summary>
    public static string ServerBaseUrl
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("WXCHAT_SERVER");
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env.Trim().TrimEnd('/');
            }
            var saved = LocalStore.Settings.ServerBaseUrl;
            return string.IsNullOrWhiteSpace(saved) ? DefaultServerBaseUrl : saved.Trim().TrimEnd('/');
        }
    }

    public static string HubUrl => ServerBaseUrl + "/hub";

    /// <summary>规范化用户输入的服务端地址；支持只填 IP:端口。</summary>
    public static bool TryNormalizeServerUrl(string? input, out string url, out string error)
    {
        url = "";
        error = "";
        var raw = (input ?? "").Trim();
        if (raw.Length == 0)
        {
            error = Loc.T("Server.UrlEmpty");
            return false;
        }
        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            raw = "http://" + raw;
        }
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            error = Loc.T("Server.UrlInvalid");
            return false;
        }
        var builder = new UriBuilder(uri) { Query = "", Fragment = "" };
        url = builder.Uri.AbsoluteUri.TrimEnd('/');
        return true;
    }

    /// <summary>保存服务端地址并立即切换 API 客户端，同时持久化到本机设置。</summary>
    public static void SaveServerBaseUrl(string url)
    {
        if (IsServerUrlLocked)
        {
            return;
        }
        var normalized = url.Trim().TrimEnd('/');
        if (!string.Equals(LocalStore.Settings.ServerBaseUrl, normalized, StringComparison.OrdinalIgnoreCase))
        {
            LocalStore.Settings.ServerBaseUrl = normalized;
            LocalStore.Save();
        }
        AppServices.Api.SetServerBaseUrl(normalized);
    }
}

/// <summary>本机设置（只保存上次账号，不保存任何密码）。</summary>
public sealed class LocalSettings
{
    /// <summary>上次使用的服务端地址（可跨机器配置）。</summary>
    public string ServerBaseUrl { get; set; } = "";
    public string LastAccount { get; set; } = "";
    public bool RememberAccount { get; set; } = true;
    public bool NotificationsEnabled { get; set; } = true;
    public bool AutoStart { get; set; }
    /// <summary>缓存目录（可清理）。</summary>
    public string CacheDir { get; set; } = "";
    /// <summary>发送图片/文件时本机留存的目录。</summary>
    public string FileDir { get; set; } = "";
    /// <summary>快捷键：send=发送消息，openChat=聚焦输入框。</summary>
    public Dictionary<string, string> Shortcuts { get; set; } = new();
    /// <summary>聊天/朋友圈背景图路径（空字符串表示默认背景）。</summary>
    public string BackgroundImage { get; set; } = "";
    /// <summary>朋友圈顶部封面图路径（空字符串表示使用默认渐变封面）。</summary>
    public string MomentsCover { get; set; } = "";
    /// <summary>每个会话在本机清空记录的时间点（服务端历史仍保留）。</summary>
    public Dictionary<string, string> ClearedBefore { get; set; } = new();
    /// <summary>界面语言：zh-Hans / en-US。</summary>
    public string Language { get; set; } = "zh-Hans";
}

public static class LocalStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WxChatApp");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");
    private static LocalSettings? _settings;

    public static LocalSettings Settings => _settings ??= Load();

    public static string DefaultCacheDir => Path.Combine(Dir, "cache");
    public static string DefaultFileDir => Path.Combine(Dir, "files");

    private static LocalSettings Load()
    {
        LocalSettings settings;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                settings = JsonSerializer.Deserialize<LocalSettings>(json) ?? new LocalSettings();
            }
            else
            {
                settings = new LocalSettings();
            }
        }
        catch
        {
            // 本地配置损坏时退回默认值，不影响登录
            settings = new LocalSettings();
        }
        if (string.IsNullOrWhiteSpace(settings.CacheDir))
        {
            settings.CacheDir = DefaultCacheDir;
        }
        if (string.IsNullOrWhiteSpace(settings.FileDir))
        {
            settings.FileDir = DefaultFileDir;
        }
        if (settings.Shortcuts.Count == 0)
        {
            settings.Shortcuts["send"] = "Ctrl+Enter";
            settings.Shortcuts["openChat"] = "Ctrl+F";
        }
        return settings;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Settings));
        }
        catch
        {
            // 本机设置写入失败不阻断业务流程
        }
    }

    /// <summary>目录是否可写（实际写一个临时文件验证）。</summary>
    public static bool IsWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".write-probe-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>统计目录占用字节数。</summary>
    public static long GetDirSize(string dir)
    {
        try
        {
            if (!Directory.Exists(dir))
            {
                return 0;
            }
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>把旧缓存目录中的文件复制到新目录，返回复制的文件数（-1 表示失败）。</summary>
    public static int MigrateDir(string from, string to, out string? error)
    {
        error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) ||
                !Directory.Exists(from) ||
                string.Equals(Path.GetFullPath(from), Path.GetFullPath(to), StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            Directory.CreateDirectory(to);
            var copied = 0;
            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(from, file);
                var target = Path.Combine(to, relative);
                var targetDir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
                File.Copy(file, target, true);
                copied++;
            }
            return copied;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return -1;
        }
    }

    /// <summary>清空缓存目录，返回删除的文件数。</summary>
    public static int ClearDir(string dir)
    {
        try
        {
            if (!Directory.Exists(dir))
            {
                return 0;
            }
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                File.Delete(file);
                count++;
            }
            return count;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>把待发送的图片/文件复制到配置的存放目录，失败抛出可读异常。</summary>
    public static string StageOutgoingFile(string sourcePath)
    {
        var dir = Settings.FileDir;
        if (!IsWritable(dir))
        {
            throw new IOException(Loc.T("Settings.FilesDirNotWritable", dir));
        }
        var name = DateTime.Now.ToString("yyyyMMdd-HHmmss-") + Guid.NewGuid().ToString("N")[..6] +
                   Path.GetExtension(sourcePath);
        var target = Path.Combine(dir, name);
        File.Copy(sourcePath, target, true);
        return target;
    }

    public static DateTime? GetClearedBefore(string convId)
    {
        return Settings.ClearedBefore.TryGetValue(convId, out var raw) &&
               DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : null;
    }

    public static void SetClearedBefore(string convId, DateTime utc)
    {
        Settings.ClearedBefore[convId] = utc.ToString("o");
        Save();
    }

    public static void ClearAllClearedMarkers()
    {
        Settings.ClearedBefore.Clear();
        Save();
    }
}
