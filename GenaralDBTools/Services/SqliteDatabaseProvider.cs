using System.Data;
using System.Data.Common;
using GenaralDBTools.Models;
using Microsoft.Data.Sqlite;

namespace GenaralDBTools.Services;

public sealed class SqliteDatabaseProvider : SqlProviderBase
{
    public override DatabaseKind Kind => DatabaseKind.Sqlite;
    public override string DisplayName => "SQLite";

    protected override DbConnection CreateConnection(ConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FilePath))
            throw new InvalidOperationException("SQLite 连接缺少数据库文件路径。");
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = profile.FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        return new SqliteConnection(builder.ToString());
    }

    protected override string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    protected override string? ResolveOrderBy(IReadOnlyList<ColumnInfo> columns, bool isView)
    {
        var pk = columns.Where(c => c.IsPrimaryKey).Select(c => Quote(c.Name)).ToList();
        if (pk.Count > 0) return string.Join(", ", pk);
        return isView ? null : "rowid";
    }

    protected override string PagedSql(string qualifiedTable, string? orderBy, string filter, int limit, int offset)
    {
        var where = filter is null ? "" : $" WHERE {filter}";
        var order = orderBy is null ? "" : $" ORDER BY {orderBy}";
        return $"SELECT * FROM {qualifiedTable}{where}{order} LIMIT {limit} OFFSET {offset}";
    }

    protected override string BuildCreateTableSql(string qualifiedTable, IReadOnlyList<ColumnDefinition> columns)
    {
        var autoIncrement = columns.Count(c => c.IsAutoIncrement);
        if (autoIncrement > 1)
            throw new InvalidOperationException("一张表最多只能有一个自增字段。");
        var autoColumn = columns.FirstOrDefault(c => c.IsAutoIncrement);
        if (autoColumn != null && autoColumn.IsPrimaryKey == false)
            throw new InvalidOperationException("自增字段必须同时设为主键。");

        var parts = new List<string>();
        foreach (var column in columns)
        {
            if (column.IsAutoIncrement)
            {
                parts.Add($"{Quote(column.Name)} INTEGER PRIMARY KEY AUTOINCREMENT");
                continue;
            }
            parts.Add(BuildColumnClause(Quote(column.Name), column.DataType, column.IsNullable, column.DefaultValue));
        }

        var pkColumns = columns.Where(c => c.IsPrimaryKey && !c.IsAutoIncrement).Select(c => Quote(c.Name)).ToList();
        if (autoColumn == null && pkColumns.Count > 0)
            parts.Add($"PRIMARY KEY ({string.Join(", ", pkColumns)})");

        return $"CREATE TABLE {qualifiedTable} (\n  {string.Join(",\n  ", parts)}\n)";
    }

    public override async Task<IReadOnlyList<DatabaseNode>> GetObjectsAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        const string sql = """
            SELECT name, type FROM sqlite_master
            WHERE type IN ('table','view') AND name NOT LIKE 'sqlite_%'
            ORDER BY CASE type WHEN 'table' THEN 0 ELSE 1 END, name;
            """;
        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);
        var data = await QueryAsync(connection, sql, ct);

        var nodes = new List<DatabaseNode>();
        foreach (DataRow row in data.Rows)
        {
            var name = Convert.ToString(row["name"]) ?? "";
            var type = Convert.ToString(row["type"]) ?? "table";
            var isView = string.Equals(type, "view", StringComparison.OrdinalIgnoreCase);
            nodes.Add(new DatabaseNode
            {
                Name = name,
                NodeType = isView ? NodeTypes.View : NodeTypes.Table,
                ProfileId = profile.Id,
                ObjectName = name,
                Icon = isView ? "◫" : "▦"
            });
        }
        if (nodes.Count == 0)
        {
            nodes.Add(new DatabaseNode { Name = "（空数据库，尚无表）", NodeType = NodeTypes.Hint, Icon = "·" });
        }
        return nodes;
    }

    public override async Task<IReadOnlyList<DatabaseNode>> GetChildObjectsAsync(ConnectionProfile profile, DatabaseNode node, CancellationToken ct = default)
    {
        if (!node.IsTableLike) return Array.Empty<DatabaseNode>();
        if (string.IsNullOrWhiteSpace(node.ObjectName)) return Array.Empty<DatabaseNode>();

        var columns = await GetColumnsAsync(profile, null, node.ObjectName, ct);
        if (columns.Count == 0)
            return new[] { new DatabaseNode { Name = "（无字段信息）", NodeType = NodeTypes.Hint, Icon = "·" } };

        return columns.Select(c => new DatabaseNode
        {
            Name = $"{c.Name}  {c.DataType}{(c.IsPrimaryKey ? " · PK" : "")}{(!c.IsNullable ? " · 非空" : "")}",
            NodeType = NodeTypes.Column,
            ProfileId = profile.Id,
            ObjectName = node.ObjectName,
            Icon = c.IsPrimaryKey ? "◇" : "◻"
        }).ToList();
    }

    public override async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(ConnectionProfile profile, string? schema, string table, CancellationToken ct = default)
    {
        ValidateName(table, "表名");
        var literal = $"'{table.Replace("'", "''")}'";
        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);

        var info = await QueryAsync(connection, $"SELECT cid, name, type, \"notnull\", dflt_value, pk FROM pragma_table_info({literal});", ct);
        var autoIncrement = false;
        try
        {
            var createSql = await QueryAsync(connection,
                $"SELECT sql FROM sqlite_master WHERE type='table' AND name={literal};", ct);
            if (createSql.Rows.Count > 0)
            {
                var text = Convert.ToString(createSql.Rows[0][0]) ?? "";
                autoIncrement = text.Contains("AUTOINCREMENT", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (SqliteException)
        {
            // pragma 已成功时表定义读取失败不影响字段浏览，按非自增处理。
            autoIncrement = false;
        }

        var list = new List<ColumnInfo>();
        foreach (DataRow row in info.Rows)
        {
            var name = Convert.ToString(row["name"]) ?? "";
            var type = Convert.ToString(row["type"]) ?? "";
            var notNull = Convert.ToInt64(row["notnull"]) == 1;
            var pkIndex = Convert.ToInt64(row["pk"]);
            var isPk = pkIndex > 0;
            var isAuto = autoIncrement && isPk && type.Contains("INT", StringComparison.OrdinalIgnoreCase);
            var defaultValue = row["dflt_value"] is DBNull or null ? null : Convert.ToString(row["dflt_value"]);
            list.Add(new ColumnInfo(name, string.IsNullOrWhiteSpace(type) ? "(无类型)" : type,
                !notNull && !isPk, isPk, isAuto, defaultValue));
        }
        return list;
    }
}
