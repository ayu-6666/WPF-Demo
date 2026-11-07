using System.Security.Cryptography;
using System.Text;

namespace BitTomrrorApp.Services;

/// <summary>
/// 敏感信息保护工具：
/// 1) FTP 密码 / Telegram Token 等凭据使用 Windows DPAPI（当前用户作用域）加密后落盘，
///    密文以 "dpapi:v1:" 前缀标识，无法跨用户或跨机器解密；
/// 2) 启动密码使用 PBKDF2-SHA256（10 万次迭代 + 随机盐）存储哈希，不保存明文。
/// </summary>
public static class SecureText
{
    private const string Prefix = "dpapi:v1:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BitTomrrorApp.SecureText.v1");

    public static bool IsProtected(string stored) =>
        stored != null && stored.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(bytes);
        }
        catch
        {
            // 加密失败时不落盘明文，宁可留空
            return "";
        }
    }

    public static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        if (!IsProtected(stored)) return stored; // 兼容历史明文配置
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(stored[Prefix.Length..]), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // 换用户/换机器导致无法解密时视为空凭据
            return "";
        }
    }

    public static (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToHexString(hash), Convert.ToBase64String(salt));
    }

    public static bool VerifyPassword(string password, string expectedHash, string saltBase64)
    {
        if (string.IsNullOrEmpty(expectedHash) || string.IsNullOrEmpty(saltBase64)) return false;
        try
        {
            var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(saltBase64), 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(expectedHash));
        }
        catch
        {
            return false;
        }
    }
}
