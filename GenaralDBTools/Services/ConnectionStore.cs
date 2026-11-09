using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenaralDBTools.Models;

namespace GenaralDBTools.Services;

public sealed class ConnectionStore
{
    private readonly string _file = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GenaralDBTools", "connections.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public string FilePath => _file;

    public async Task<List<ConnectionProfile>> LoadAsync()
    {
        if (!File.Exists(_file)) return new List<ConnectionProfile>();
        try
        {
            var json = await File.ReadAllTextAsync(_file);
            var items = JsonSerializer.Deserialize<List<ConnectionProfile>>(json, Options) ?? new List<ConnectionProfile>();
            foreach (var item in items)
                item.Password = Unprotect(item.ProtectedPassword);
            return items;
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"读取连接配置失败：{_file} 可能已损坏。原文件已保留，可修复或删除后重试。", ex);
        }
    }

    public async Task SaveAsync(IEnumerable<ConnectionProfile> profiles)
    {
        var folder = Path.GetDirectoryName(_file)!;
        Directory.CreateDirectory(folder);

        var list = profiles.Select(profile =>
        {
            var copy = profile.Clone();
            copy.ProtectedPassword = Protect(copy.Password);
            copy.Password = "";
            copy.UpdatedAt = DateTime.Now;
            return copy;
        }).ToList();

        var json = JsonSerializer.Serialize(list, Options);
        var temp = _file + ".tmp";
        await File.WriteAllTextAsync(temp, json, new UTF8Encoding(false));

        try
        {
            if (File.Exists(_file))
                File.Replace(temp, _file, null, ignoreMetadataErrors: true);
            else
                File.Move(temp, _file);
        }
        catch (IOException)
        {
            // 某些网络盘/文件系统不支持 File.Replace，退化为覆盖写入。
            File.Copy(temp, _file, overwrite: true);
            File.Delete(temp);
        }
    }

    private static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
    }

    private static string Unprotect(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // 配置文件由其他 Windows 用户复制而来时无法解密，按空密码处理，界面会提示连接失败原因。
            return "";
        }
    }
}
