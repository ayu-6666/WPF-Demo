using System.Data;

namespace GenaralDBTools.Models;

public sealed record ColumnInfo(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsAutoIncrement,
    string? DefaultValue)
{
    public bool IsBinary => DataType.Contains("blob", StringComparison.OrdinalIgnoreCase)
        || DataType.Contains("binary", StringComparison.OrdinalIgnoreCase)
        || DataType.Contains("bytea", StringComparison.OrdinalIgnoreCase)
        || DataType.Contains("image", StringComparison.OrdinalIgnoreCase)
        || DataType.Contains("raw", StringComparison.OrdinalIgnoreCase);

    public string DisplayType => DataType + (IsPrimaryKey ? " · PK" : "") + (IsAutoIncrement ? " · 自增" : "")
        + (IsNullable ? "" : " · 非空");
}

public sealed record QueryResult(DataTable? Table, int AffectedRows, TimeSpan Elapsed)
{
    public bool HasTable => Table is { Columns.Count: > 0 };
    public int RowCount => Table?.Rows.Count ?? 0;
    public int ColumnCount => Table?.Columns.Count ?? 0;

    public string Summary => HasTable
        ? $"返回 {RowCount} 行 × {ColumnCount} 列，耗时 {Elapsed.TotalMilliseconds:0} ms"
        : $"执行成功，影响 {AffectedRows} 行，耗时 {Elapsed.TotalMilliseconds:0} ms";
}

public sealed record TablePage(DataTable Table, long TotalRows, int Offset, int Limit, IReadOnlyList<ColumnInfo> Columns);

public sealed record ColumnDefinition(
    string Name,
    string DataType,
    bool IsPrimaryKey,
    bool IsNullable,
    bool IsAutoIncrement,
    string? DefaultValue);

public sealed record CreateTableRequest(string TableName, IReadOnlyList<ColumnDefinition> Columns);
