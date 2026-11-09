using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using GenaralDBTools.Models;
using StackExchange.Redis;

namespace GenaralDBTools.Services;

public sealed class RedisDatabaseProvider : IDatabaseProvider, IRedisBrowser
{
    private readonly ConcurrentDictionary<string, Lazy<Task<ConnectionMultiplexer>>> _multiplexers = new();
    private const int MaxDisplayItems = 500;
    private const int MaxDisplayChars = 20000;

    public DatabaseKind Kind => DatabaseKind.Redis;
    public string DisplayName => "Redis";
    public bool SupportsSql => false;

    private static ConfigurationOptions Options(ConnectionProfile profile)
    {
        var endpoint = $"{profile.Host}:{(profile.Port == 0 ? 6379 : profile.Port)}";
        var options = ConfigurationOptions.Parse(endpoint);
        options.Password = string.IsNullOrEmpty(profile.Password) ? null : profile.Password;
        options.User = string.IsNullOrWhiteSpace(profile.Username) ? null : profile.Username;
        options.Ssl = profile.UseSsl;
        options.AbortOnConnectFail = true;
        options.ConnectTimeout = 8000;
        options.ConnectRetry = 1;
        options.SyncTimeout = 15000;
        options.AsyncTimeout = 15000;
        options.DefaultDatabase = DatabaseIndex(profile);
        return options;
    }

    private static int DatabaseIndex(ConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Database)) return 0;
        if (!int.TryParse(profile.Database.Trim(), out var index) || index is < 0 or > 15)
            throw new InvalidOperationException("Redis 数据库编号必须是 0 - 15 之间的整数。");
        return index;
    }

    private async Task<ConnectionMultiplexer> GetMultiplexerAsync(ConnectionProfile profile, CancellationToken ct)
    {
        var lazy = _multiplexers.GetOrAdd(profile.Id,
            _ => new Lazy<Task<ConnectionMultiplexer>>(() => ConnectionMultiplexer.ConnectAsync(Options(profile))));
        try
        {
            return await lazy.Value.WaitAsync(ct);
        }
        catch
        {
            _multiplexers.TryRemove(profile.Id, out _);
            throw;
        }
    }

    private async Task<IDatabase> GetDatabaseAsync(ConnectionProfile profile, CancellationToken ct) =>
        (await GetMultiplexerAsync(profile, ct)).GetDatabase(DatabaseIndex(profile));

    public async Task CloseAsync(ConnectionProfile profile)
    {
        if (_multiplexers.TryRemove(profile.Id, out var lazy) && lazy.IsValueCreated && lazy.Value.IsCompletedSuccessfully)
        {
            try { await lazy.Value.Result.DisposeAsync(); }
            catch { /* 关闭会话失败不影响断开流程 */ }
        }
    }

    public async Task TestConnectionAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var multiplexer = await GetMultiplexerAsync(profile, ct);
        var database = multiplexer.GetDatabase(DatabaseIndex(profile));
        var elapsed = await database.PingAsync();
        if (elapsed <= TimeSpan.Zero)
            throw new InvalidOperationException("Redis PING 返回异常耗时，连接可能不稳定。");
    }

    public Task<QueryResult> ExecuteAsync(ConnectionProfile profile, string sql, CancellationToken ct = default) =>
        throw new NotSupportedException("Redis 是键值数据库，不支持 SQL。请在 Redis 浏览器中使用命令或键管理功能。");

    public Task<IReadOnlyList<DatabaseNode>> GetObjectsAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var index = DatabaseIndex(profile);
        IReadOnlyList<DatabaseNode> nodes = new List<DatabaseNode>
        {
            new DatabaseNode
            {
                Name = $"DB {index}",
                NodeType = NodeTypes.RedisDatabase,
                ProfileId = profile.Id,
                ObjectName = index.ToString(CultureInfo.InvariantCulture),
                Icon = "▣"
            }
        };
        return Task.FromResult(nodes);
    }

    public async Task<IReadOnlyList<DatabaseNode>> GetChildObjectsAsync(ConnectionProfile profile, DatabaseNode node, CancellationToken ct = default)
    {
        if (node.NodeType != NodeTypes.RedisDatabase) return Array.Empty<DatabaseNode>();
        var keys = await ScanKeysAsync(profile, "*", 200, ct);
        if (keys.Count == 0)
            return new[] { new DatabaseNode { Name = "（该数据库没有 key）", NodeType = NodeTypes.Hint, Icon = "·" } };

        return keys.Select(k => new DatabaseNode
        {
            Name = $"{k.Key}   [{k.Type}]",
            NodeType = NodeTypes.RedisKey,
            ProfileId = profile.Id,
            ObjectName = k.Key,
            Icon = "◆"
        }).ToList();
    }

    public async Task<IReadOnlyList<RedisKeySummary>> ScanKeysAsync(ConnectionProfile profile, string pattern, int maxKeys, CancellationToken ct = default)
    {
        if (maxKeys <= 0) maxKeys = 200;
        var database = await GetDatabaseAsync(profile, ct);
        var safePattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();
        var keys = new List<string>();
        var cursor = "0";
        var rounds = 0;

        do
        {
            ct.ThrowIfCancellationRequested();
            var result = (RedisResult[])(await database.ExecuteAsync("SCAN", cursor, "MATCH", safePattern, "COUNT", 100))!;
            if (result.Length != 2)
                throw new InvalidOperationException("SCAN 返回结果格式异常。");
            cursor = (string)result[0]!;
            var batch = (RedisResult[])result[1]!;
            foreach (var item in batch)
                keys.Add((string)item!);
            rounds++;
        }
        while (cursor != "0" && keys.Count < maxKeys && rounds < 50);

        keys = keys.Take(maxKeys).Distinct(StringComparer.Ordinal).ToList();
        if (keys.Count == 0) return Array.Empty<RedisKeySummary>();

        var typeTasks = keys.Select(key => database.ExecuteAsync("TYPE", key)).ToArray();
        var ttlTasks = keys.Select(key => database.ExecuteAsync("TTL", key)).ToArray();
        await Task.WhenAll(typeTasks.Cast<Task>().Concat(ttlTasks.Cast<Task>())).WaitAsync(ct);

        var types = new List<string>(keys.Count);
        for (var i = 0; i < keys.Count; i++)
            types.Add(((string?)typeTasks[i].Result) ?? "none");

        var lengthBatch = database.CreateBatch();
        var lengthTasks = keys.Select((k, i) => LengthTask(lengthBatch, k, types[i])).ToArray();
        lengthBatch.Execute();
        await Task.WhenAll(lengthTasks).WaitAsync(ct);

        var summaries = new List<RedisKeySummary>(keys.Count);
        for (var i = 0; i < keys.Count; i++)
        {
            var ttlSeconds = (long)ttlTasks[i].Result;
            var ttl = ttlSeconds == -2 ? (TimeSpan?)null : TimeSpan.FromSeconds(ttlSeconds);
            summaries.Add(new RedisKeySummary(keys[i], types[i], lengthTasks[i].Result, ttl));
        }
        return summaries;
    }

    private static Task<long> LengthTask(IBatch batch, string key, string type) => type switch
    {
        "string" => batch.StringLengthAsync(key),
        "hash" => batch.HashLengthAsync(key),
        "list" => batch.ListLengthAsync(key),
        "set" => batch.SetLengthAsync(key),
        "zset" => batch.SortedSetLengthAsync(key),
        "stream" => batch.StreamLengthAsync(key),
        _ => Task.FromResult(0L)
    };

    public async Task<RedisKeyDetail> GetKeyDetailAsync(ConnectionProfile profile, string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Key 不能为空。");

        var database = await GetDatabaseAsync(profile, ct);
        var type = ((string?)await database.ExecuteAsync("TYPE", key)) ?? "none";
        var ttlSeconds = (long)await database.ExecuteAsync("TTL", key);
        var ttl = ttlSeconds == -2 ? (TimeSpan?)null : TimeSpan.FromSeconds(ttlSeconds);

        switch (type)
        {
            case "none":
                throw new InvalidOperationException($"Key “{key}” 不存在，可能已被删除。");
            case "string":
            {
                var value = await database.StringGetAsync(key);
                var text = value.IsNull ? "" : value.ToString();
                return new RedisKeyDetail(key, type, ttl, Truncate(text), text.Length);
            }
            case "hash":
            {
                var entries = await database.HashGetAllAsync(key);
                var builder = new StringBuilder();
                foreach (var entry in entries.Take(MaxDisplayItems))
                {
                    if (builder.Length > 0) builder.AppendLine();
                    builder.Append(entry.Name).Append('=').Append(entry.Value);
                }
                if (entries.Length > MaxDisplayItems)
                    builder.AppendLine().Append($"# 仅显示前 {MaxDisplayItems} 项，共 {entries.Length} 项");
                return new RedisKeyDetail(key, type, ttl, Truncate(builder.ToString()), entries.Length);
            }
            case "list":
            {
                var items = await database.ListRangeAsync(key, 0, MaxDisplayItems - 1);
                var length = await database.ListLengthAsync(key);
                var text = string.Join("\n", items.Select(v => v.ToString()));
                if (length > items.Length)
                    text += $"\n# 仅显示前 {items.Length} 项，共 {length} 项";
                return new RedisKeyDetail(key, type, ttl, Truncate(text), length);
            }
            case "set":
            {
                var members = await database.SetMembersAsync(key);
                var length = members.Length;
                var text = string.Join("\n", members.Take(MaxDisplayItems).Select(v => v.ToString()));
                if (length > MaxDisplayItems)
                    text += $"\n# 仅显示前 {MaxDisplayItems} 项，共 {length} 项";
                return new RedisKeyDetail(key, type, ttl, Truncate(text), length);
            }
            case "zset":
            {
                var entries = await database.SortedSetRangeByRankWithScoresAsync(key, 0, MaxDisplayItems - 1);
                var length = await database.SortedSetLengthAsync(key);
                var text = string.Join("\n", entries.Select(e =>
                    $"{e.Element}={e.Score.ToString(CultureInfo.InvariantCulture)}"));
                if (length > entries.Length)
                    text += $"\n# 仅显示前 {entries.Length} 项，共 {length} 项";
                return new RedisKeyDetail(key, type, ttl, Truncate(text), length);
            }
            case "stream":
            {
                var length = await database.StreamLengthAsync(key);
                return new RedisKeyDetail(key, type, ttl,
                    "（Stream 类型暂不支持在此编辑，仅展示长度）", length);
            }
            default:
                throw new NotSupportedException($"暂不支持编辑 Redis 类型：{type}");
        }
    }

    public async Task SaveKeyAsync(ConnectionProfile profile, string key, string type, string value, TimeSpan? ttl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Key 不能为空。");
        if (key.Length > 512)
            throw new InvalidOperationException("Key 长度不能超过 512 个字符。");

        var database = await GetDatabaseAsync(profile, ct);
        // 覆盖写入：先删除旧 Key，再用批次按类型写入，最后单独设置 TTL。
        await database.KeyDeleteAsync(key);

        var batch = database.CreateBatch();
        Task writeTask;
        switch (type)
        {
            case "string":
                writeTask = batch.StringSetAsync(key, value);
                break;
            case "hash":
            {
                var entries = ParsePairs(value, "hash 需要每行一个 field=value");
                if (entries.Length == 0) throw new InvalidOperationException("hash 至少需要一个 field=value。");
                writeTask = batch.HashSetAsync(key, entries);
                break;
            }
            case "list":
            {
                var items = ParseLines(value);
                if (items.Length == 0) throw new InvalidOperationException("list 至少需要一个元素（每行一个）。");
                writeTask = batch.ListRightPushAsync(key, items.Select(item => (RedisValue)item).ToArray());
                break;
            }
            case "set":
            {
                var members = ParseLines(value);
                if (members.Length == 0) throw new InvalidOperationException("set 至少需要一个元素（每行一个）。");
                writeTask = batch.SetAddAsync(key, members.Select(item => (RedisValue)item).ToArray());
                break;
            }
            case "zset":
            {
                var entries = ParseScoredMembers(value);
                if (entries.Length == 0) throw new InvalidOperationException("zset 需要每行一个 member=score。");
                writeTask = batch.SortedSetAddAsync(key, entries);
                break;
            }
            default:
                throw new InvalidOperationException($"不支持写入类型：{type}");
        }

        batch.Execute();
        await writeTask.WaitAsync(ct);

        if (ttl is { TotalSeconds: > 0 })
            await database.KeyExpireAsync(key, ttl).WaitAsync(ct);
    }

    public async Task DeleteKeyAsync(ConnectionProfile profile, string key, CancellationToken ct = default)
    {
        var database = await GetDatabaseAsync(profile, ct);
        var removed = await database.KeyDeleteAsync(key);
        if (!removed)
            throw new InvalidOperationException($"Key “{key}” 已不存在，可能已被其他客户端删除。");
    }

    public async Task<bool> SetExpireAsync(ConnectionProfile profile, string key, TimeSpan? ttl, CancellationToken ct = default)
    {
        var database = await GetDatabaseAsync(profile, ct);
        return ttl is { TotalSeconds: > 0 }
            ? await database.KeyExpireAsync(key, ttl)
            : await database.KeyPersistAsync(key);
    }

    public async Task<string> ExecuteCommandAsync(ConnectionProfile profile, string commandLine, CancellationToken ct = default)
    {
        var parts = SplitCommandLine(commandLine);
        if (parts.Count == 0)
            throw new InvalidOperationException("请输入要执行的 Redis 命令，例如 PING 或 GET mykey。");

        var database = await GetDatabaseAsync(profile, ct);
        var command = parts[0];
        var arguments = parts.Skip(1).Select(x => (object)x).ToArray();
        var result = await database.ExecuteAsync(command, arguments);
        return FormatResult(result);
    }

    internal static string FormatResult(RedisResult result)
    {
        try
        {
            if (result.IsNull) return "(nil)";
            switch (result.Resp2Type)
            {
                case ResultType.BulkString:
                case ResultType.VerbatimString:
                    return result.ToString() ?? "(empty)";
                case ResultType.Integer:
                    return ((long)result).ToString(CultureInfo.InvariantCulture);
                case ResultType.SimpleString:
                    return result.ToString() ?? "(empty)";
                case ResultType.Error:
                    return "(error) " + result;
                case ResultType.Array:
                {
                    var items = (RedisResult[])result!;
                    return string.Join("\n", items.Select((item, index) => $"{index + 1}) {FormatResult(item)}"));
                }
                case ResultType.Set:
                {
                    var items = (RedisResult[])result!;
                    return string.Join("\n", items.Select(FormatResult));
                }
                default:
                    return result.ToString() ?? "(empty)";
            }
        }
        catch (InvalidCastException)
        {
            return result.ToString() ?? "(无法格式化结果)";
        }
    }

    private static string Truncate(string text) =>
        text.Length <= MaxDisplayChars
            ? text
            : text[..MaxDisplayChars] + $"\n# 内容过长，已截断（共 {text.Length} 字符）";

    private static string[] ParseLines(string value) =>
        value.Replace("\r\n", "\n").Split('\n').Where(line => line.Length > 0).ToArray();

    private static HashEntry[] ParsePairs(string value, string error)
    {
        var list = new List<HashEntry>();
        foreach (var line in ParseLines(value))
        {
            var index = line.IndexOf('=');
            if (index <= 0) throw new InvalidOperationException($"{error}，当前行无效：{line}");
            list.Add(new HashEntry(line[..index], line[(index + 1)..]));
        }
        return list.ToArray();
    }

    private static SortedSetEntry[] ParseScoredMembers(string value)
    {
        var list = new List<SortedSetEntry>();
        foreach (var line in ParseLines(value))
        {
            var index = line.LastIndexOf('=');
            if (index <= 0) throw new InvalidOperationException($"zset 需要每行一个 member=score，当前行无效：{line}");
            var member = line[..index];
            if (!double.TryParse(line[(index + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
                throw new InvalidOperationException($"zset 分数必须是数字：{line}");
            list.Add(new SortedSetEntry(member, score));
        }
        return list.ToArray();
    }

    internal static List<string> SplitCommandLine(string input)
    {
        var result = new List<string>();
        var builder = new StringBuilder();
        var quote = '\0';
        var escaped = false;

        foreach (var ch in input)
        {
            if (escaped)
            {
                builder.Append(ch);
                escaped = false;
                continue;
            }
            if (ch == '\\')
            {
                escaped = true;
                continue;
            }
            if (quote != '\0')
            {
                if (ch == quote) quote = '\0';
                else builder.Append(ch);
                continue;
            }
            if (ch is '"' or '\'')
            {
                quote = ch;
                continue;
            }
            if (char.IsWhiteSpace(ch))
            {
                if (builder.Length > 0)
                {
                    result.Add(builder.ToString());
                    builder.Clear();
                }
                continue;
            }
            builder.Append(ch);
        }

        if (quote != '\0')
            throw new InvalidOperationException("命令中的引号没有闭合。");
        if (escaped)
            builder.Append('\\');
        if (builder.Length > 0)
            result.Add(builder.ToString());
        return result;
    }
}
