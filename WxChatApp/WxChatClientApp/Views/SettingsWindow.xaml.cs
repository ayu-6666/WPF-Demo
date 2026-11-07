using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WxChatApp.Helpers;
using WxChatApp.Services;
using WxChatApp.ViewModels;

namespace WxChatApp.Views;

/// <summary>设置窗口：缓存、图片/文件目录、背景、通用、快捷键五组，全部持久化到本机 settings.json。</summary>
public partial class SettingsWindow : Window
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WxChatApp";

    private readonly ShellViewModel _shell;
    private readonly LocalSettings _settings = LocalStore.Settings;
    private readonly string _snapCacheDir;
    private readonly string _snapFileDir;
    private readonly string _snapBackground;
    private readonly bool _snapNotify;
    private readonly bool _snapAutoStart;
    private readonly Dictionary<string, string> _snapShortcuts;
    private bool _loading;

    public SettingsWindow(ShellViewModel shell)
    {
        // 打开时先做快照：点击“取消”或 Esc/X 关闭时回滚
        _snapCacheDir = _settings.CacheDir;
        _snapFileDir = _settings.FileDir;
        _snapBackground = _settings.BackgroundImage;
        _snapNotify = shell.NotificationsEnabled;
        _snapAutoStart = IsAutoStartEnabled();
        _snapShortcuts = new Dictionary<string, string>(_settings.Shortcuts);

        InitializeComponent();
        _shell = shell;
        _loading = true;
        CachePathBox.Text = _settings.CacheDir;
        FilesPathBox.Text = _settings.FileDir;
        NotifyCheck.IsChecked = _shell.NotificationsEnabled;
        AutoStartCheck.IsChecked = IsAutoStartEnabled();
        ShortcutSendBox.Text = ShortcutFor("send");
        ShortcutChatBox.Text = ShortcutFor("openChat");
        LanguageCombo.SelectedIndex = Loc.IsEnglish ? 1 : 0;
        _loading = false;
        RefreshCacheSize();
        RefreshBackgroundPreview();
    }

    private string ShortcutFor(string action) =>
        _settings.Shortcuts.TryGetValue(action, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v
            : action == "send" ? "Ctrl+Enter" : "Ctrl+F";

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageCombo.SelectedIndex < 0)
        {
            return;
        }
        Loc.SetLanguage(LanguageCombo.SelectedIndex == 1 ? AppLanguage.EnUs : AppLanguage.ZhHans, persist: true);
    }

    private void RefreshCacheSize()
    {
        var bytes = LocalStore.GetDirSize(_settings.CacheDir);
        CacheSizeText.Text = bytes < 1024
            ? $"{bytes} B"
            : bytes < 1024 * 1024
                ? $"{bytes / 1024.0:F1} KB"
                : $"{bytes / 1024.0 / 1024.0:F2} MB";
    }

    private void BrowseCache_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder(Loc.T("Settings.ChooseCacheDir"), _settings.CacheDir);
        if (folder == null)
        {
            return;
        }
        if (!LocalStore.IsWritable(folder))
        {
            Dialogs.Error(this, Loc.T("Settings.DirNotWritable"), Loc.T("Settings.CacheDir"));
            return;
        }
        // 只更新待保存的路径，点“保存”时才迁移并落盘
        CachePathBox.Text = folder;
        RefreshCacheSize();
    }

    private void OpenCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_settings.CacheDir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_settings.CacheDir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.Error(this, Loc.T("Settings.OpenDirFail", ex.Message), Loc.T("Settings.CacheDir"));
        }
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        if (!Dialogs.Confirm(this, Loc.T("Settings.ClearConfirm", _settings.CacheDir),
                Loc.T("Settings.ClearCache"), Loc.T("Common.Clean")))
        {
            return;
        }
        var deleted = LocalStore.ClearDir(_settings.CacheDir);
        RefreshCacheSize();
        if (deleted < 0)
        {
            Dialogs.Error(this, Loc.T("Settings.ClearFailed"), Loc.T("Settings.ClearCache"));
        }
        else
        {
            Dialogs.Success(this, Loc.T("Settings.Cleared", deleted), Loc.T("Settings.ClearDone"));
        }
    }

    private void BrowseFiles_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder(Loc.T("Settings.ChooseFilesDir"), _settings.FileDir);
        if (folder == null)
        {
            return;
        }
        if (!LocalStore.IsWritable(folder))
        {
            Dialogs.Error(this, Loc.T("Settings.DirNotWritable"), Loc.T("Settings.StorageDir"));
            return;
        }
        // 旧目录不自动搬移，明确提示策略
        var old = _settings.FileDir;
        _settings.FileDir = folder;
        LocalStore.Save();
        FilesPathBox.Text = folder;
        Dialogs.Info(this, Loc.T("Settings.FilesDirChanged", old), Loc.T("Settings.DirUpdated"));
    }

    private void Notify_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        _shell.NotificationsEnabled = NotifyCheck.IsChecked == true;
        LocalStore.Save();
    }

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        var enable = AutoStartCheck.IsChecked == true;
        try
        {
            WriteAutoStart(enable);
            _settings.AutoStart = enable;
            LocalStore.Save();
        }
        catch (Exception ex)
        {
            AutoStartCheck.IsChecked = !enable;
            Dialogs.Error(this, Loc.T("Settings.AutoStartFail", ex.Message), Loc.T("Settings.AutoStart"));
        }
    }

    private static void WriteAutoStart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (key == null)
        {
            throw new IOException(Loc.T("Settings.AutoStartWriteFail"));
        }
        if (enable)
        {
            var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (exe.Length == 0)
            {
                throw new IOException(Loc.T("Settings.PathNotFound"));
            }
            key.SetValue(RunValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(RunValueName, false);
        }
    }

    // ==================== 背景图 ====================

    private void RefreshBackgroundPreview()
    {
        var image = AppBackground.Current;
        BgPreview.Source = image;
        BgPreviewHint.Visibility = image == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseBackground_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = Loc.T("Settings.ChooseBg"),
            Filter = Loc.T("Settings.ImageFilter"),
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) != true)
        {
            return;
        }
        if (!AppBackground.TrySet(dlg.FileName, out var error))
        {
            Dialogs.Warning(this, error, Loc.T("Settings.BackgroundImage"));
            return;
        }
        RefreshBackgroundPreview();
    }

    private void ResetBackground_Click(object sender, RoutedEventArgs e)
    {
        AppBackground.Reset();
        RefreshBackgroundPreview();
    }

    private void Shortcut_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        ShortcutError.Text = "";
        if (e.Key == Key.Escape)
        {
            return;
        }
        var combo = ToCombo(e);
        if (combo.Length == 0)
        {
            return;
        }
        var box = (System.Windows.Controls.TextBox)sender;
        var action = ReferenceEquals(box, ShortcutSendBox) ? "send" : "openChat";
        var other = action == "send" ? "openChat" : "send";
        if (string.Equals(combo, ShortcutFor(other), StringComparison.OrdinalIgnoreCase))
        {
            ShortcutError.Text = Loc.T("Settings.ShortcutOccupied", combo);
            return;
        }
        box.Text = combo;
        _settings.Shortcuts[action] = combo;
        LocalStore.Save();
    }

    private static string ToCombo(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.None or Key.DeadCharProcessed or Key.ImeProcessed)
        {
            return "";
        }
        var mods = Keyboard.Modifiers;
        var parts = new List<string>();
        if ((mods & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((mods & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((mods & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((mods & ModifierKeys.Windows) != 0) parts.Add("Win");
        if (parts.Count == 0)
        {
            return "";
        }
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private void ResetSend_Click(object sender, RoutedEventArgs e)
    {
        _settings.Shortcuts["send"] = "Ctrl+Enter";
        ShortcutSendBox.Text = "Ctrl+Enter";
        LocalStore.Save();
        ShortcutError.Text = "";
    }

    private void ResetChat_Click(object sender, RoutedEventArgs e)
    {
        _settings.Shortcuts["openChat"] = "Ctrl+F";
        ShortcutChatBox.Text = "Ctrl+F";
        LocalStore.Save();
        ShortcutError.Text = "";
    }

    private static string? PickFolder(string title, string initial)
    {
        var dlg = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = Directory.Exists(initial) ? initial : "",
            Multiselect = false
        };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValueName) is string s && s.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        RollbackAndClose();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var newCacheDir = CachePathBox.Text.Trim();
        if (newCacheDir.Length == 0)
        {
            Dialogs.Error(this, Loc.T("Settings.CacheDirEmpty"), Loc.T("Settings.CacheDir"));
            return;
        }
        if (!string.Equals(newCacheDir, _settings.CacheDir, StringComparison.OrdinalIgnoreCase))
        {
            if (!LocalStore.IsWritable(newCacheDir))
            {
                Dialogs.Error(this, Loc.T("Settings.CacheDirNotWritable"), Loc.T("Settings.CacheDir"));
                return;
            }
            var oldDir = _settings.CacheDir;
            var copied = LocalStore.MigrateDir(oldDir, newCacheDir, out var error);
            if (copied < 0)
            {
                Dialogs.Error(this, Loc.T("Settings.CacheMigrateFail", error), Loc.T("Settings.CacheDir"));
                return;
            }
            _settings.CacheDir = newCacheDir;
            LocalStore.Save();
            Dialogs.Success(this, Loc.T("Settings.CacheMigrated", copied, newCacheDir),
                Loc.T("Settings.CacheDirUpdated"));
        }
        else
        {
            _settings.CacheDir = newCacheDir;
            LocalStore.Save();
        }
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        RollbackAndClose();
    }

    /// <summary>回滚到打开设置时的快照（含路径、通知、自启、快捷键、背景），再关闭。</summary>
    private void RollbackAndClose()
    {
        _settings.CacheDir = _snapCacheDir;
        _settings.FileDir = _snapFileDir;
        _settings.BackgroundImage = _snapBackground;
        _settings.AutoStart = _snapAutoStart;
        _settings.Shortcuts.Clear();
        foreach (var pair in _snapShortcuts)
        {
            _settings.Shortcuts[pair.Key] = pair.Value;
        }
        LocalStore.Save();

        if (IsAutoStartEnabled() != _snapAutoStart)
        {
            try
            {
                WriteAutoStart(_snapAutoStart);
            }
            catch
            {
                // 回滚失败不阻断关闭
            }
        }
        if (_shell.NotificationsEnabled != _snapNotify)
        {
            _shell.NotificationsEnabled = _snapNotify;
        }
        AppBackground.Refresh();
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            RollbackAndClose();
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
