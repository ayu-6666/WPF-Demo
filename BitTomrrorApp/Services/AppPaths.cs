using System.IO;
using BitTomrrorApp.Models;

namespace BitTomrrorApp.Services;

public static class AppPaths
{
    // 允许通过环境变量覆盖数据目录（便于便携运行与隔离环境验证），默认仍在 %LOCALAPPDATA%\BitTomrrorApp
    public static string Root { get; } = Environment.GetEnvironmentVariable("BITTOMRROR_APP_DATA")
        is string overrideRoot && !string.IsNullOrWhiteSpace(overrideRoot)
            ? overrideRoot
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitTomrrorApp");

    public static string TasksFile => Path.Combine(Root, "tasks.json");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string BtCacheDirectory => Path.Combine(Root, "bt-cache");
    public static string TorrentsDirectory => Path.Combine(Root, "torrents");
    public static string TempDirectory => Path.Combine(Root, "temp");

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(BtCacheDirectory);
        Directory.CreateDirectory(TorrentsDirectory);
        Directory.CreateDirectory(TempDirectory);
    }

    public static string CreateTaskTempDirectory(DownloadTask task)
    {
        var dir = Path.Combine(TempDirectory, task.Id);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
