using System.Data;
using System.Data.Common;
using GenaralDBTools.Models;
using MySqlConnector;

namespace GenaralDBTools.Services;

public sealed class MySqlDatabaseProvider : SqlProviderBase
{
    public override DatabaseKind Kind => DatabaseKind.MySql;
    public override string DisplayName => "MySQL";

    protected override DbConnection CreateConnection(ConnectionProfile profile)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = profile.Host,
            Port = profile.Port == 0 ? 3306 : (uint)profile.Port,
            Database = profile.Database,
            UserID = profile.Username,
            Password = profile.Password,
            SslMode = profile.UseSsl ? MySqlSslMode.Required : MySqlSslMode.Preferred,
            ConnectionTimeout = 8,
            DefaultCommandTimeout = 60,
            AllowUserVariables = true
        };
        return new MySqlConnection(builder.ConnectionString);
    }

    protected override string Quote(string identifier) => $"`{identifier.Replace("`", "``")}`";

    protected override string PagedSql(string qualifiedTable, string? orderBy, string filter, int limit, int offset)
    {
        var where = filter is null ? "" : $" WHERE {filter}";
        var order = orderBy is null ? "" : $" ORDER BY {orderBy}";
        return $"SELECT * FROM {qualifiedTable}{where}{order} LIMIT {limit} OFFSET {offset}";
    }

    protected override string BuildCreateTableSql(string qualifiedTable, IReadOnlyList<ColumnDefinition> columns)
    {
        if (columns.Count(c => c.IsAutoIncrement) > 1)
            throw new InvalidOperationException("一张表最多只能有一个自增字段。");

        var parts = new List<string>();
        foreach (var column in columns)
        {
            var clause = $"{Quote(column.Name)} {column.DataType}";
            if (!column.IsNullable) clause += " NOT NULL";
            if (column.IsAutoIncrement)
            {
                if (!column.IsPrimaryKey)
                    throw new InvalidOperationException("MySQL 的自增字段必须同时设为主键。");
                clause += " AUTO_INCREMENT";
            }
            if (!string.IsNullOrWhiteSpace(column.DefaultValue)) clause += $" DEFAULT {column.DefaultValue.Trim()}";
            parts.Add(clause);
        }

        var pkColumns = columns.Where(c => c.IsPrimaryKey).Select(c => Quote(c.Name)).ToList();
        if (pkColumns.Count > 0)
            parts.Add($"PRIMARY KEY ({string.Join(", ", pkColumns)})");

        return $"CREATE TABLE {qualifiedTable} (\n  {string.Join(",\n  ", parts)}\n)";
    }

    private static string RequireSchema(ConnectionProfile profile) =>
        string.IsNullOrWhiteSpace(profile.Database)
            ? throw new InvalidOperationException("MySQL 连接未指定数据库。请编辑连接并填写“数据库名称”，或先选择要浏览的 Schema。")
            : profile.Database;

    public override async Task<IReadOnlyList<DatabaseNode>> GetObjectsAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(profile.Database))
        {
            const string schemaSql = """
                SELECT schema_name FROM information_schema.schemata
                WHERE schema_name NOT IN ('information_schema','mysql','performance_schema','sys')
                ORDER BY schema_name;
                """;
            await using var c0 = CreateConnection(profile);
            await c0.OpenAsync(ct);
            var schemas = await QueryAsync(c0, schemaSql, ct);
            return schemas.Rows.Cast<DataRow>().Select(row => new DatabaseNode
            {
                Name = Convert.ToString(row[0]) ?? "",
                NodeType = NodeTypes.Schema,
                ProfileId = profile.Id,
                SchemaName = Convert.ToString(row[0]),
                ObjectName = Convert.ToString(row[0]),
                Icon = "▤"
            }).ToList();
        }
        return await LoadTablesAsync(profile, profile.Database, ct);
    }

    private async Task<IReadOnlyList<DatabaseNode>> LoadTablesAsync(ConnectionProfile profile, string schema, CancellationToken ct)
    {
        const string sql = """
            SELECT table_name, table_type FROM information_schema.tables
            WHERE table_schema = @schema
            ORDER BY CASE table_type WHEN 'BASE TABLE' THEN 0 ELSE 1 END, table_name;
            """;
        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@schema";
        parameter.Value = schema;
        command.Parameters.Add(parameter);
        var data = await ReadTableAsync(command, ct);

        var nodes = new List<DatabaseNode>();
        foreach (DataRow row in data.Rows)
        {
            var name = Convert.ToString(row["table_name"]) ?? "";
            var isView = !string.Equals(Convert.ToString(row["table_type"]), "BASE TABLE", StringComparison.OrdinalIgnoreCase);
            nodes.Add(new DatabaseNode
            {
                Name = name,
                NodeType = isView ? NodeTypes.View : NodeTypes.Table,
                ProfileId = profile.Id,
                SchemaName = schema,
                ObjectName = name,
                Icon = isView ? "◫" : "▦"
            });
        }
        if (nodes.Count == 0)
            nodes.Add(new DatabaseNode { Name = "（该数据库没有表）", NodeType = NodeTypes.Hint, Icon = "·" });
        return nodes;
    }

    public override async Task<IReadOnlyList<DatabaseNode>> GetChildObjectsAsync(ConnectionProfile profile, DatabaseNode node, CancellationToken ct = default)
    {
        if (node.NodeType == NodeTypes.Schema && !string.IsNullOrWhiteSpace(node.SchemaName))
        {
            var clone = profile.Clone();
            clone.Database = node.SchemaName;
            return await LoadTablesAsync(clone, node.SchemaName, ct);
        }

        if (!node.IsTableLike || string.IsNullOrWhiteSpace(node.ObjectName))
            return Array.Empty<DatabaseNode>();

        var schema = string.IsNullOrWhiteSpace(node.SchemaName) ? RequireSchema(profile) : node.SchemaName;
        var columns = await GetColumnsAsync(profile, schema, node.ObjectName, ct);
        if (columns.Count == 0)
            return new[] { new DatabaseNode { Name = "（无字段信息）", NodeType = NodeTypes.Hint, Icon = "·" } };

        return columns.Select(c => new DatabaseNode
        {
            Name = $"{c.Name}  {c.DataType}{(c.IsPrimaryKey ? " · PK" : "")}{(!c.IsNullable ? " · 非空" : "")}",
            NodeType = NodeTypes.Column,
            ProfileId = profile.Id,
            ObjectName = node.ObjectName,
            SchemaName = schema,
            Icon = c.IsPrimaryKey ? "◇" : "◻"
        }).ToList();
    }

    public override async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(ConnectionProfile profile, string? schema, string table, CancellationToken ct = default)
    {
        ValidateName(table, "表名");
        var effectiveSchema = string.IsNullOrWhiteSpace(schema) ? RequireSchema(profile) : schema;

        const string sql = """
            SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_KEY, EXTRA, COLUMN_DEFAULT
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION;
            """;
        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        var schemaParameter = command.CreateParameter();
        schemaParameter.ParameterName = "@schema";
        schemaParameter.Value = effectiveSchema;
        command.Parameters.Add(schemaParameter);
        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@table";
        tableParameter.Value = table;
        command.Parameters.Add(tableParameter);

        var data = await ReadTableAsync(command, ct);
        var list = new List<ColumnInfo>();
        foreach (DataRow row in data.Rows)
        {
            var name = Convert.ToString(row["COLUMN_NAME"]) ?? "";
            var type = Convert.ToString(row["COLUMN_TYPE"]) ?? "";
            var nullable = string.Equals(Convert.ToString(row["IS_NULLABLE"]), "YES", StringComparison.OrdinalIgnoreCase);
            var isPk = string.Equals(Convert.ToString(row["COLUMN_KEY"]), "PRI", StringComparison.OrdinalIgnoreCase);
            var extra = Convert.ToString(row["EXTRA"]) ?? "";
            var isAuto = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase);
            var defaultValue = row["COLUMN_DEFAULT"] is DBNull or null ? null : Convert.ToString(row["COLUMN_DEFAULT"]);
            list.Add(new ColumnInfo(name, type, nullable && !isPk, isPk, isAuto, defaultValue));
        }
        return list;
    }

    private static async Task<DataTable> ReadTableAsync(DbCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        var table = new DataTable("结果集");
        table.Load(reader);
        return table;
    }
}
