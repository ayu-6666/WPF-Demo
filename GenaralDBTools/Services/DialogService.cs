using System.Windows;
using GenaralDBTools.Dialogs;
using GenaralDBTools.Models;

namespace GenaralDBTools.Services;

public static class DialogService
{
    private static Window? ActiveOwner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
        ?? Application.Current?.MainWindow;

    public static void Info(string message, string title = "提示") =>
        Show(title, message, null, MessageDialogKind.Info, false);

    public static void Warn(string message, string title = "注意") =>
        Show(title, message, null, MessageDialogKind.Warning, false);

    public static void Error(string title, Exception exception) =>
        Error(title, exception.Message, exception.ToString());

    public static void Error(string title, string message, string? details = null) =>
        Show(title, message, details, MessageDialogKind.Error, false);

    public static bool Confirm(string title, string message, string? details = null, string confirmText = "确定") =>
        Show(title, message, details, MessageDialogKind.Confirm, true, confirmText);

    private static bool Show(string title, string message, string? details, MessageDialogKind kind, bool confirm,
        string confirmText = "确定")
    {
        var dialog = new MessageDialog(title, message, details, kind, confirm, confirmText);
        var owner = ActiveOwner;
        if (owner != null && owner != dialog) dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    public static Dictionary<string, object?>? EditRow(string caption, IReadOnlyList<ColumnInfo> columns,
        IReadOnlyDictionary<string, object?>? initialValues, bool insertMode)
    {
        var dialog = new RowEditorDialog(caption, columns, initialValues, insertMode);
        var owner = ActiveOwner;
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public static CreateTableRequest? CreateTable(DatabaseKind kind, string? schema)
    {
        var dialog = new CreateTableDialog(kind, schema);
        var owner = ActiveOwner;
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public static RedisValueInput? EditRedisValue(string? key, string? type, string? value, TimeSpan? ttl, bool editMode)
    {
        var dialog = new RedisValueDialog(key, type, value, ttl, editMode);
        var owner = ActiveOwner;
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
}
