using System.Text.Json;
using System.IO;
using BitTomrrorApp.Models;

namespace BitTomrrorApp.Services;

public static class DownloadStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Options) ?? new AppSettings();
                // 落盘时为 DPAPI 密文，读回时解密为内存明文
                settings.FtpPassword = SecureText.Unprotect(settings.FtpPassword);
                settings.TelegramToken = SecureText.Unprotect(settings.TelegramToken);
                return settings;
            }
        }
        catch
        {
            // 配置损坏时回退到默认值，避免应用无法启动
        }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings)
    {
        AppPaths.Ensure();
        // 保存副本并对敏感字段做 DPAPI 加密；内存中的 settings 实例保持明文
        var snapshot = new AppSettings
        {
            DefaultSavePath = settings.DefaultSavePath,
            FfmpegPath = settings.FfmpegPath,
            DefaultDownloadLimitBytesPerSec = settings.DefaultDownloadLimitBytesPerSec,
            DefaultUploadLimitBytesPerSec = settings.DefaultUploadLimitBytesPerSec,
            DefaultSeedRatioLimit = settings.DefaultSeedRatioLimit,
            DefaultSeedTimeLimitMinutes = settings.DefaultSeedTimeLimitMinutes,
            FtpHost = settings.FtpHost,
            FtpPort = settings.FtpPort,
            FtpUser = settings.FtpUser,
            FtpPassword = SecureText.Protect(settings.FtpPassword),
            FtpRemotePath = settings.FtpRemotePath,
            FtpUseTls = settings.FtpUseTls,
            TelegramToken = SecureText.Protect(settings.TelegramToken),
            TelegramChatId = settings.TelegramChatId,
            LockPasswordHash = settings.LockPasswordHash,
            LockPasswordSalt = settings.LockPasswordSalt
        };
        WriteAtomic(AppPaths.SettingsFile, JsonSerializer.Serialize(snapshot, Options));
    }

    public static List<DownloadTask> LoadTasks()
    {
        try
        {
            if (File.Exists(AppPaths.TasksFile))
                return JsonSerializer.Deserialize<List<DownloadTask>>(File.ReadAllText(AppPaths.TasksFile), Options) ?? new List<DownloadTask>();
        }
        catch
        {
            // 任务文件损坏时忽略，避免应用无法启动
        }
        return new List<DownloadTask>();
    }

    public static void SaveTasks(IEnumerable<DownloadTask> tasks)
    {
        AppPaths.Ensure();
        WriteAtomic(AppPaths.TasksFile, JsonSerializer.Serialize(tasks.ToList(), Options));
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }
}
