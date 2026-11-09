using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using GenaralDBTools.Models;

namespace GenaralDBTools.Dialogs;

public partial class ColumnRow : ObservableObject
{
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string dataType = "";
    [ObservableProperty] private bool isPrimaryKey;
    [ObservableProperty] private bool isNullable = true;
    [ObservableProperty] private bool isAutoIncrement;
    [ObservableProperty] private string defaultValue = "";
}

public partial class CreateTableDialog : Window
{
    public CreateTableRequest? Result { get; private set; }
    public string[] Types { get; }
    private readonly ObservableCollection<ColumnRow> _rows = new();

    public CreateTableDialog(DatabaseKind kind, string? schema)
    {
        InitializeComponent();
        KindText.Text = $"{ConnectionProfile.KindName(kind)}" + (string.IsNullOrWhiteSpace(schema) ? "" : $" · Schema {schema}");
        Types = kind switch
        {
            DatabaseKind.Sqlite => new[] { "INTEGER", "TEXT", "REAL", "BLOB", "NUMERIC", "BOOLEAN", "DATE", "DATETIME", "VARCHAR(255)" },
            DatabaseKind.MySql => new[] { "INT", "BIGINT", "VARCHAR(255)", "TEXT", "DECIMAL(10,2)", "DATETIME", "DATE", "BOOLEAN", "BLOB", "JSON" },
            DatabaseKind.Oracle => new[] { "NUMBER", "NUMBER(10)", "VARCHAR2(255)", "DATE", "TIMESTAMP", "CLOB", "BLOB", "CHAR(1)" },
            _ => new[] { "TEXT" }
        };
        DataContext = this;

        _rows.Add(new ColumnRow
        {
            Name = "id", DataType = kind == DatabaseKind.Oracle ? "NUMBER(10)" : "INTEGER",
            IsPrimaryKey = true, IsNullable = false, IsAutoIncrement = true
        });
        _rows.Add(new ColumnRow { Name = "name", DataType = Types.First(t => t.Contains("VARCHAR") || t == "TEXT"), IsNullable = true });
        RowsGrid.ItemsSource = _rows;
        TableNameBox.Text = "new_table";
        TableNameBox.Focus();
        TableNameBox.SelectAll();
    }

    private void AddColumn_Click(object sender, RoutedEventArgs e)
    {
        _rows.Add(new ColumnRow { Name = $"column_{_rows.Count + 1}", DataType = "TEXT", IsNullable = true });
    }

    private void RemoveColumn_Click(object sender, RoutedEventArgs e)
    {
        if (RowsGrid.SelectedItem is ColumnRow row)
            _rows.Remove(row);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var tableName = TableNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            ErrorText.Text = "请输入表名。";
            return;
        }
        if (_rows.Count == 0)
        {
            ErrorText.Text = "至少需要定义一列。";
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                ErrorText.Text = "字段名不能为空。";
                return;
            }
            if (!seen.Add(row.Name.Trim()))
            {
                ErrorText.Text = $"字段名“{row.Name}”重复。";
                return;
            }
            if (string.IsNullOrWhiteSpace(row.DataType))
            {
                ErrorText.Text = $"字段“{row.Name}”缺少数据类型。";
                return;
            }
        }

        var autoCount = _rows.Count(r => r.IsAutoIncrement);
        if (autoCount > 1)
        {
            ErrorText.Text = "最多只能有一个自增字段。";
            return;
        }
        var auto = _rows.FirstOrDefault(r => r.IsAutoIncrement);
        if (auto is { IsPrimaryKey: false })
        {
            ErrorText.Text = "自增字段必须同时勾选“主键”。";
            return;
        }

        Result = new CreateTableRequest(tableName, _rows.Select(row => new ColumnDefinition(
            row.Name.Trim(), row.DataType.Trim(), row.IsPrimaryKey, row.IsNullable,
            row.IsAutoIncrement, string.IsNullOrWhiteSpace(row.DefaultValue) ? null : row.DefaultValue.Trim())).ToList());
        DialogResult = true;
    }
}
