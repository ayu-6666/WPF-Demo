using System.Data;
using GenaralDBTools.Models;

namespace GenaralDBTools.Services;

public interface IDatabaseProvider
{
    DatabaseKind Kind { get; }
    string DisplayName { get; }
    bool SupportsSql { get; }

    Task TestConnectionAsync(ConnectionProfile profile, CancellationToken ct = default);

    Task<QueryResult> ExecuteAsync(ConnectionProfile profile, string sql, CancellationToken ct = default);

    Task<IReadOnlyList<DatabaseNode>> GetObjectsAsync(ConnectionProfile profile, CancellationToken ct = default);

    Task<IReadOnlyList<DatabaseNode>> GetChildObjectsAsync(ConnectionProfile profile, DatabaseNode node, CancellationToken ct = default);

    /// <summary>释放该连接的持久会话（若 Provider 无持久会话则为空实现）。</summary>
    Task CloseAsync(ConnectionProfile profile);
}

public interface IRelationalTableProvider : IDatabaseProvider
{
    Task<TablePage> LoadTablePageAsync(ConnectionProfile profile, string? schema, string table, bool isView,
        string? filter, int offset, int limit, CancellationToken ct = default);

    Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(ConnectionProfile profile, string? schema, string table, CancellationToken ct = default);

    Task<int> InsertRowAsync(ConnectionProfile profile, string? schema, string table,
        IReadOnlyDictionary<string, object?> values, CancellationToken ct = default);

    Task<int> UpdateRowAsync(ConnectionProfile profile, string? schema, string table,
        IReadOnlyDictionary<string, object?> values, IReadOnlyDictionary<string, object?> keys, CancellationToken ct = default);

    Task<int> DeleteRowAsync(ConnectionProfile profile, string? schema, string table,
        IReadOnlyDictionary<string, object?> keys, CancellationToken ct = default);

    Task CreateTableAsync(ConnectionProfile profile, string? schema, CreateTableRequest request, CancellationToken ct = default);

    Task DropTableAsync(ConnectionProfile profile, string? schema, string table, CancellationToken ct = default);
}

public interface IRedisBrowser
{
    Task<IReadOnlyList<RedisKeySummary>> ScanKeysAsync(ConnectionProfile profile, string pattern, int maxKeys, CancellationToken ct = default);
    Task<RedisKeyDetail> GetKeyDetailAsync(ConnectionProfile profile, string key, CancellationToken ct = default);
    Task SaveKeyAsync(ConnectionProfile profile, string key, string type, string value, TimeSpan? ttl, CancellationToken ct = default);
    Task DeleteKeyAsync(ConnectionProfile profile, string key, CancellationToken ct = default);
    Task<bool> SetExpireAsync(ConnectionProfile profile, string key, TimeSpan? ttl, CancellationToken ct = default);
    Task<string> ExecuteCommandAsync(ConnectionProfile profile, string commandLine, CancellationToken ct = default);
}

public sealed class DatabaseProviderRegistry
{
    private readonly IReadOnlyDictionary<DatabaseKind, IDatabaseProvider> _providers;

    public DatabaseProviderRegistry(IEnumerable<IDatabaseProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Kind);
    }

    public IDatabaseProvider Get(DatabaseKind kind) =>
        _providers.TryGetValue(kind, out var provider)
            ? provider
            : throw new NotSupportedException($"{ConnectionProfile.KindName(kind)} Provider 尚未注册。");

    public IRelationalTableProvider GetRelational(DatabaseKind kind) =>
        Get(kind) as IRelationalTableProvider
        ?? throw new NotSupportedException($"{ConnectionProfile.KindName(kind)} 不支持表级操作。");

    public IRedisBrowser GetRedis(DatabaseKind kind) =>
        Get(kind) as IRedisBrowser
        ?? throw new NotSupportedException($"{ConnectionProfile.KindName(kind)} 不支持 Redis 键级操作。");

    public bool SupportsSql(DatabaseKind kind) => Get(kind).SupportsSql;
}
