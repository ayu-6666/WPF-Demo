using System.Text.Json.Serialization;

namespace GenaralDBTools.Models;

public enum DatabaseKind { Oracle, MySql, Sqlite, Redis, MongoDb }

public sealed class ConnectionProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Connection";
    public DatabaseKind Kind { get; set; } = DatabaseKind.Sqlite;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; }
    public string Database { get; set; } = "";
    public string Username { get; set; } = "";
    [JsonIgnore] public string Password { get; set; } = "";
    public string FilePath { get; set; } = "GenaralDBTools.db";
    public bool UseSsl { get; set; }
    public string ProtectedPassword { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ConnectionProfile Clone() => new()
    {
        Id = Id, Name = Name, Kind = Kind, Host = Host, Port = Port, Database = Database,
        Username = Username, Password = Password, FilePath = FilePath, UseSsl = UseSsl,
        ProtectedPassword = ProtectedPassword, UpdatedAt = UpdatedAt
    };

    public static int DefaultPort(DatabaseKind kind) => kind switch
    {
        DatabaseKind.Oracle => 1521,
        DatabaseKind.MySql => 3306,
        DatabaseKind.Redis => 6379,
        DatabaseKind.MongoDb => 27017,
        _ => 0
    };

    public static string KindName(DatabaseKind kind) => kind switch
    {
        DatabaseKind.Oracle => "Oracle",
        DatabaseKind.MySql => "MySQL",
        DatabaseKind.Sqlite => "SQLite",
        DatabaseKind.Redis => "Redis",
        _ => "MongoDB"
    };

    /// <summary>用于界面展示的连接摘要，不会泄漏密码。</summary>
    public string Describe()
    {
        return Kind switch
        {
            DatabaseKind.Sqlite => $"SQLite · {FilePath}",
            DatabaseKind.Redis => $"Redis · {Host}:{(Port == 0 ? 6379 : Port)} · DB {Database}",
            _ => $"{KindName(Kind)} · {Host}:{(Port == 0 ? DefaultPort(Kind) : Port)}" +
                 (string.IsNullOrWhiteSpace(Database) ? "" : $" · {Database}")
        };
    }
}
