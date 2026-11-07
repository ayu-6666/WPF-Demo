using System.IO;

namespace BitTomrrorApp.Models;

public class AppSettings
{
    public string DefaultSavePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public string FfmpegPath { get; set; } = "";
    public long DefaultDownloadLimitBytesPerSec { get; set; }
    public long DefaultUploadLimitBytesPerSec { get; set; }
    public double DefaultSeedRatioLimit { get; set; }
    public int DefaultSeedTimeLimitMinutes { get; set; }

    // ---- FTP 默认连接信息（个人设置中心；新建 FTP 任务时自动填充） ----
    public string FtpHost { get; set; } = "";
    public int FtpPort { get; set; } = 21;
    public string FtpUser { get; set; } = "";
    public string FtpPassword { get; set; } = "";
    public string FtpRemotePath { get; set; } = "";
    public bool FtpUseTls { get; set; }

    // ---- Telegram 认证信息（个人设置中心；新建 Telegram 任务时自动填充 Token/chat_id） ----
    public string TelegramToken { get; set; } = "";
    public string TelegramChatId { get; set; } = "";

    // ---- 软件安全（启动密码；哈希与盐，不含明文） ----
    public string LockPasswordHash { get; set; } = "";
    public string LockPasswordSalt { get; set; } = "";
}
