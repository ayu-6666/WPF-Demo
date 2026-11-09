using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using GenaralDBTools.Models;

namespace GenaralDBTools.Dialogs;

public partial class RowField : ObservableObject
{
    public string Name { get; init; } = "";
    public string DataType { get; init; } = "";
    public bool IsPrimaryKey { get; init; }
    public bool CanEdit { get; init; } = true;
    public bool CanSetNull { get; init; } = true;
    public string? DefaultValue { get; init; }
    [ObservableProperty] private bool isNull;
    [ObservableProperty] private string value = "";
}

public partial class RowEditorDialog : Window
{
    public Dictionary<string, object?>? Result { get; private set; }
    private readonly List<RowField> _fields;
    private readonly bool _insertMode;

    public RowEditorDialog(string caption, IReadOnlyList<ColumnInfo> columns,
        IReadOnlyDictionary<string, object?>? initialValues, bool insertMode)
    {
        InitializeComponent();
        Title = caption;
        CaptionText.Text = caption;
        _insertMode = insertMode;

        _fields = columns.Select(column =>
        {
            var rawValue = initialValues != null && initialValues.TryGetValue(column.Name, out var value) ? value : null;
            var isBinary = column.IsBinary;
            var isAuto = column.IsAutoIncrement;
            var canEdit = !isBinary && !isAuto && !(column.IsPrimaryKey && !insertMode) && !(column.Name.Length == 0);
            var isNull = rawValue is null or DBNull;
            return new RowField
            {
                Name = column.Name,
                DataType = column.DisplayType,
                IsPrimaryKey = column.IsPrimaryKey,
                CanEdit = canEdit,
                CanSetNull = canEdit && column.IsNullable && !column.IsPrimaryKey,
                DefaultValue = column.DefaultValue,
                IsNull = isNull && canEdit && column.IsNullable,
                Value = isBinary
                    ? $"(二进制 {(rawValue is byte[] bytes ? bytes.Length : 0) } 字节，暂不支持编辑)"
                    : FormatValue(rawValue)
            };
        }).ToList();

        FieldsGrid.ItemsSource = _fields;
        HintText.Text = insertMode
            ? "提示：勾选“NULL”写入数据库 NULL；留空表示使用默认值或空字符串。自增列会自动生成。"
            : "提示：主键列不可修改；勾选“NULL”表示将该字段更新为 NULL。";
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null or DBNull => "",
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            byte[] bytes => $"(二进制 {bytes.Length} 字节)",
            bool boolean => boolean ? "1" : "0",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in _fields)
        {
            if (!field.CanEdit) continue;
            var hasValue = !field.IsNull && !string.IsNullOrEmpty(field.Value);

            if (_insertMode)
            {
                if (field.IsNull)
                {
                    if (!field.CanSetNull)
                    {
                        ErrorText.Text = $"字段 {field.Name} 不允许为 NULL。";
                        return;
                    }
                    result[field.Name] = null;
                }
                else if (hasValue)
                {
                    result[field.Name] = field.Value;
                }
                else if (!field.CanSetNull && string.IsNullOrWhiteSpace(field.DefaultValue))
                {
                    ErrorText.Text = $"字段 {field.Name} 不能为空（类型 {field.DataType}）。";
                    return;
                }
            }
            else
            {
                result[field.Name] = field.IsNull ? null : field.Value;
            }
        }

        if (result.Count == 0)
        {
            ErrorText.Text = _insertMode
                ? "没有填写任何可写入的字段。"
                : "没有可更新的字段。";
            return;
        }

        Result = result;
        DialogResult = true;
    }
}
