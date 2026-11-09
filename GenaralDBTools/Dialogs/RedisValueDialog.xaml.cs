using System.Globalization;
using System.Windows;

namespace GenaralDBTools.Dialogs;

public sealed record RedisValueInput(string Key, string Type, string Value, TimeSpan? Ttl);

public partial class RedisValueDialog : Window
{
    public RedisValueInput? Result { get; private set; }
    private readonly bool _editMode;

    public RedisValueDialog(string? key, string? type, string? value, TimeSpan? ttl, bool editMode)
    {
        InitializeComponent();
        _editMode = editMode;
        Title = editMode ? "编辑 Key" : "新增 Key";
        CaptionText.Text = Title;
        TypeBox.ItemsSource = new[] { "string", "hash", "list", "set", "zset" };
        KeyBox.Text = key ?? "";
        TypeBox.SelectedItem = type ?? "string";
        TypeBox.IsEnabled = !editMode;
        KeyBox.IsReadOnly = editMode;
        ValueBox.Text = value ?? "";
        if (ttl is { TotalSeconds: > 0 })
            TtlBox.Text = ((int)ttl.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        HintText.Text = editMode
            ? "保存时会先删除原 Key 再写入新内容（保留原 TTL），请确认覆盖。"
            : "格式：string 直接输入文本；hash 每行 field=value；list/set 每行一个元素；zset 每行 member=score。";
        ValueBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var key = KeyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            ErrorText.Text = "Key 不能为空。";
            return;
        }

        var type = TypeBox.SelectedItem as string ?? "string";
        var ttlText = TtlBox.Text.Trim();
        TimeSpan? ttl = null;
        if (!string.IsNullOrWhiteSpace(ttlText))
        {
            if (!int.TryParse(ttlText, out var seconds) || seconds < 0)
            {
                ErrorText.Text = "TTL 必须是不小于 0 的整数秒（0 表示不设置过期）。";
                return;
            }
            if (seconds > 0) ttl = TimeSpan.FromSeconds(seconds);
        }

        Result = new RedisValueInput(key, type, ValueBox.Text, ttl);
        DialogResult = true;
    }
}
