using System.Windows;
using WxChatApp.Services;
using WxChatApp.Views;

namespace WxChatApp.Helpers;

/// <summary>自定义弹框的统一入口（替代 MessageBox，保证不出现原生系统弹框）。</summary>
public static class Dialogs
{
    private static Window? PickOwner(Window? owner)
    {
        if (owner != null)
        {
            return owner;
        }
        var app = Application.Current;
        if (app == null)
        {
            return null;
        }
        foreach (Window w in app.Windows)
        {
            if (w.IsActive)
            {
                return w;
            }
        }
        return null;
    }

    private static bool Show(Window? owner, DialogKind kind, string? title, string message,
        string? okText = null, string? cancelText = null)
    {
        var dlg = new AppDialog(kind, title ?? Loc.T("Dialog.InfoTitle"), message, PickOwner(owner),
            okText ?? Loc.T("Common.Ok"), cancelText ?? Loc.T("Common.Cancel"));
        dlg.ShowDialog();
        return dlg.Result;
    }

    public static void Info(Window? owner, string message, string? title = null, string? okText = null)
        => Show(owner, DialogKind.Info, title ?? Loc.T("Dialog.InfoTitle"), message,
            okText ?? Loc.T("Common.Know"));

    public static void Success(Window? owner, string message, string? title = null, string? okText = null)
        => Show(owner, DialogKind.Success, title ?? Loc.T("Dialog.SuccessTitle"), message,
            okText ?? Loc.T("Common.Know"));

    public static void Warning(Window? owner, string message, string? title = null, string? okText = null)
        => Show(owner, DialogKind.Warning, title ?? Loc.T("Dialog.WarningTitle"), message,
            okText ?? Loc.T("Common.Know"));

    public static void Error(Window? owner, string message, string? title = null, string? okText = null)
        => Show(owner, DialogKind.Error, title ?? Loc.T("Dialog.ErrorTitle"), message,
            okText ?? Loc.T("Common.Know"));

    public static bool Confirm(Window? owner, string message, string? title = null,
        string? okText = null, string? cancelText = null)
        => Show(owner, DialogKind.Confirm, title ?? Loc.T("Dialog.ConfirmTitle"), message,
            okText ?? Loc.T("Common.Ok"), cancelText ?? Loc.T("Common.Cancel"));
}
