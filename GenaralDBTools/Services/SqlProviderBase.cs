using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text.RegularExpressions;
using GenaralDBTools.Models;

namespace GenaralDBTools.Services;

/// <summary>
/// ADO.NET 关系型数据库的公共实现：查询执行、分页读取、行级增删改、建表/删表。
/// 各数据库只需提供连接创建、标识符引用、分页 SQL、建表 SQL、元数据查询等方言细节。
/// </summary>
public abstract class SqlProviderBase : IRelationalTableProvider
{
    public abstract DatabaseKind Kind { get; }
    public abstract string DisplayName { get; }
    public bool SupportsSql => true;

    protected abstract DbConnection CreateConnection(ConnectionProfile profile);

    protected abstract string Quote(string identifier);

    /// <summary>参数占位符前缀，Oracle 使用 ':'，其余使用 '@'。</summary>
    protected virtual string ParameterPrefix => "@";

    protected virtual string Qualify(string? schema, string table) =>
        string.IsNullOrWhiteSpace(schema) ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";

    protected abstract string PagedSql(string qualifiedTable, string? orderBy, string filter, int limit, int offset);

    protected abstract string BuildCreateTableSql(string qualifiedTable, IReadOnlyList<ColumnDefinition> columns);

    public abstract Task<IReadOnlyList<DatabaseNode>> GetObjectsAsync(ConnectionProfile profile, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<DatabaseNode>> GetChildObjectsAsync(ConnectionProfile profile, DatabaseNode node, CancellationToken ct = default);
    public abstract Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(ConnectionProfile profile, string? schema, string table, CancellationToken ct = default);

    public virtual Task CloseAsync(ConnectionProfile profile) => Task.CompletedTask;

    public async Task TestConnectionAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(ct);
    }

    public async Task<QueryResult> ExecuteAsync(ConnectionProfile profile, string sql, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("SQL 不能为空。");

        var watch = Stopwatch.StartNew();
        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;

        DataTable? table = null;
        int affected;
        var reader = await command.ExecuteReaderAsync(ct);
        try
        {
            do
            {
                if (reader.FieldCount > 0 && table == null)
                {
                    // 不用 DataTable.Load：它会在填充后关闭 reader，导致无法继续读取后续结果集。
                    table = new DataTable("结果集");
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var name = reader.GetName(i);
                        if (table.Columns.Contains(name)) name = $"{name}_{i}";
                        table.Columns.Add(name, reader.GetFieldType(i));
                    }
                    while (await reader.ReadAsync(ct))
                    {
                        var values = new object[reader.FieldCount];
                        reader.GetValues(values);
                        table.Rows.Add(values);
                    }
                }
                else
                {
                    // 其余结果集不展示，但需要读完才能拿到 RecordsAffected。
                    while (await reader.ReadAsync(ct))
                    {
                    }
                }
            }
            while (!reader.IsClosed && await reader.NextResultAsync(ct));

            affected = reader.RecordsAffected;
        }
        finally
        {
            await reader.DisposeAsync();
        }

        if (affected < 0) affected = 0;
        watch.Stop();
        return new QueryResult(table, affected, watch.Elapsed);
    }

    public async Task<TablePage> LoadTablePageAsync(ConnectionProfile profile, string? schema, string table, bool isView,
        string? filter, int offset, int limit, CancellationToken ct = default)
    {
        if (offset < 0) offset = 0;
        if (limit <= 0) limit = 50;

        var columns = await GetColumnsAsync(profile, schema, table, ct);
        var qualified = Qualify(schema, table);
        var safeFilter = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);

        var countSql = $"SELECT COUNT(*) FROM {qualified}" + (safeFilter is null ? "" : $" WHERE {safeFilter}");
        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = countSql;
            countCommand.CommandTimeout = 60;
            var scalar = await countCommand.ExecuteScalarAsync(ct);
            var total = scalar is null or DBNull ? 0L : Convert.ToInt64(scalar, System.Globalization.CultureInfo.InvariantCulture);
            var orderBy = ResolveOrderBy(columns, isView);
            var sql = PagedSql(qualified, orderBy, safeFilter, limit, offset);
            var data = await QueryAsync(connection, sql, ct);
            return new TablePage(data, total, offset, limit, columns);
        }
    }

    protected virtual string? ResolveOrderBy(IReadOnlyList<ColumnInfo> columns, bool isView)
    {
        var pk = columns.Where(c => c.IsPrimaryKey).Select(c => Quote(c.Name)).ToList();
        return pk.Count == 0 ? null : string.Join(", ", pk);
    }

    protected async Task<DataTable> QueryAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var table = new DataTable("结果集");
        table.Load(reader);
        return table;
    }

    public async Task<int> InsertRowAsync(ConnectionProfile profile, string? schema, string table,
        IReadOnlyDictionary<string, object?> values, CancellationToken ct = default)
    {
        if (values.Count == 0)
            throw new InvalidOperationException("没有可写入的字段值。");

        ValidateName(table, "表名");
        var qualified = Qualify(schema, table);
        var columns = string.Join(", ", values.Keys.Select(Quote));
        var parameters = string.Join(", ", values.Keys.Select((_, i) => $"{ParameterPrefix}p{i}"));
        var sql = $"INSERT INTO {qualified} ({columns}) VALUES ({parameters})";

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        var index = 0;
        foreach (var pair in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"{ParameterPrefix}p{index++}";
            parameter.Value = pair.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> UpdateRowAsync(ConnectionProfile profile, string? schema, string table,
        IReadOnlyDictionary<string, object?> values, IReadOnlyDictionary<string, object?> keys, CancellationToken ct = default)
    {
        if (values.Count == 0)
            throw new InvalidOperationException("没有勾选任何需要更新的字段。");
        if (keys.Count == 0)
            throw new InvalidOperationException("该表没有主键，无法安全定位要更新的记录。");

        ValidateName(table, "表名");
        var qualified = Qualify(schema, table);
        var setClause = string.Join(", ", values.Keys.Select((k, i) => $"{Quote(k)} = {ParameterPrefix}v{i}"));
        var whereClause = string.Join(" AND ", keys.Keys.Select((k, i) => $"{Quote(k)} = {ParameterPrefix}k{i}"));
        var sql = $"UPDATE {qualified} SET {setClause} WHERE {whereClause}";

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;

        var index = 0;
        foreach (var pair in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"{ParameterPrefix}v{index++}";
            parameter.Value = pair.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        index = 0;
        foreach (var pair in keys)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"{ParameterPrefix}k{index++}";
            parameter.Value = pair.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> DeleteRowAsync(ConnectionProfile profile, string? schema, string table,
        IReadOnlyDictionary<string, object?> keys, CancellationToken ct = default)
    {
        if (keys.Count == 0)
            throw new InvalidOperationException("该表没有主键，无法安全删除记录。");

        ValidateName(table, "表名");
        var qualified = Qualify(schema, table);
        var whereClause = string.Join(" AND ", keys.Keys.Select((k, i) => $"{Quote(k)} = {ParameterPrefix}k{i}"));
        var sql = $"DELETE FROM {qualified} WHERE {whereClause}";

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        var index = 0;
        foreach (var pair in keys)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"{ParameterPrefix}k{index++}";
            parameter.Value = pair.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task CreateTableAsync(ConnectionProfile profile, string? schema, CreateTableRequest request, CancellationToken ct = default)
    {
        if (request.Columns.Count == 0)
            throw new InvalidOperationException("至少需要定义一列。");
        ValidateName(request.TableName, "表名");
        foreach (var column in request.Columns)
        {
            ValidateName(column.Name, "字段名");
            if (string.IsNullOrWhiteSpace(column.DataType))
                throw new InvalidOperationException($"字段 {column.Name} 缺少数据类型。");
            if (column.DataType.Contains(';') || column.DataType.Contains('\r') || column.DataType.Contains('\n'))
                throw new InvalidOperationException($"字段 {column.Name} 的数据类型包含非法字符。");
        }

        var qualified = Qualify(schema, request.TableName);
        var sql = BuildCreateTableSql(qualified, request.Columns);

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DropTableAsync(ConnectionProfile profile, string? schema, string table, CancellationToken ct = default)
    {
        ValidateName(table, "表名");
        var qualified = Qualify(schema, table);
        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {qualified}";
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static readonly Regex ValidIdentifier = new(@"^[A-Za-z0-9_\u4e00-\u9fa5#$.@\-]+$", RegexOptions.Compiled);

    protected static void ValidateName(string name, string label)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException($"{label}不能为空。");
        if (name.Length > 128)
            throw new InvalidOperationException($"{label}长度超过 128 个字符。");
        if (!ValidIdentifier.IsMatch(name))
            throw new InvalidOperationException($"{label}“{name}”包含非法字符（不允许空格、引号、分号等）。");
    }

    protected static string BuildColumnClause(string name, string dataType, bool isNullable, string? defaultValue)
    {
        var clause = $"{name} {dataType}".TrimEnd();
        if (!isNullable) clause += " NOT NULL";
        if (!string.IsNullOrWhiteSpace(defaultValue)) clause += $" DEFAULT {defaultValue.Trim()}";
        return clause;
    }
}
