using System.Collections.Concurrent;
using GenaralDBTools.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GenaralDBTools.Services;

public sealed class MongoDatabaseProvider : IDatabaseProvider
{
    private readonly ConcurrentDictionary<string, MongoClient> _clients = new();

    public DatabaseKind Kind => DatabaseKind.MongoDb;
    public string DisplayName => "MongoDB";
    public bool SupportsSql => false;

    private MongoClient GetClient(ConnectionProfile profile)
    {
        return _clients.GetOrAdd(profile.Id, _ =>
        {
            var auth = string.IsNullOrWhiteSpace(profile.Username)
                ? ""
                : $"{Uri.EscapeDataString(profile.Username)}:{Uri.EscapeDataString(profile.Password)}@";
            var authDb = string.IsNullOrWhiteSpace(profile.Database) ? "admin" : profile.Database;
            var tls = profile.UseSsl ? "&tls=true" : "";
            var uri = $"mongodb://{auth}{profile.Host}:{(profile.Port == 0 ? 27017 : profile.Port)}/?authSource={Uri.EscapeDataString(authDb)}{tls}";
            return new MongoClient(uri);
        });
    }

    public Task CloseAsync(ConnectionProfile profile)
    {
        _clients.TryRemove(profile.Id, out _);
        return Task.CompletedTask;
    }

    public async Task TestConnectionAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var client = GetClient(profile);
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
    }

    public Task<QueryResult> ExecuteAsync(ConnectionProfile profile, string sql, CancellationToken ct = default) =>
        throw new NotSupportedException("MongoDB 使用 BSON 文档查询，不支持 SQL。");

    public async Task<IReadOnlyList<DatabaseNode>> GetObjectsAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var client = GetClient(profile);
        using var cursor = await client.ListDatabaseNamesAsync(cancellationToken: ct);
        var names = await cursor.ToListAsync(ct);
        return names.Select(name => new DatabaseNode
        {
            Name = name,
            NodeType = NodeTypes.MongoDatabase,
            ProfileId = profile.Id,
            ObjectName = name,
            Icon = "▣"
        }).ToList();
    }

    public async Task<IReadOnlyList<DatabaseNode>> GetChildObjectsAsync(ConnectionProfile profile, DatabaseNode node, CancellationToken ct = default)
    {
        if (node.NodeType != NodeTypes.MongoDatabase || string.IsNullOrWhiteSpace(node.ObjectName))
            return Array.Empty<DatabaseNode>();

        var client = GetClient(profile);
        var database = client.GetDatabase(node.ObjectName);
        using var cursor = await database.ListCollectionNamesAsync(cancellationToken: ct);
        var names = await cursor.ToListAsync(ct);
        if (names.Count == 0)
            return new[] { new DatabaseNode { Name = "（该数据库没有集合）", NodeType = NodeTypes.Hint, Icon = "·" } };

        return names.Select(name => new DatabaseNode
        {
            Name = name,
            NodeType = NodeTypes.Collection,
            ProfileId = profile.Id,
            ObjectName = name,
            SchemaName = node.ObjectName,
            Icon = "▤"
        }).ToList();
    }
}
