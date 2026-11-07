using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WxChatApp.Helpers;
using WxChatApp.Services;
using WxChatApp.ViewModels;

namespace WxChatApp.Views;

public partial class ChatPaneView : UserControl
{
    private ChatPaneViewModel? _vm;
    private readonly NotifyCollectionChangedEventHandler _messagesHandler;

    public ChatPaneView()
    {
        InitializeComponent();
        _messagesHandler = (_, _) => ScrollToEnd();
        DataContextChanged += (_, _) => AttachViewModel();
        Loaded += (_, _) =>
        {
            AttachViewModel();
            ScrollToEnd();
        };
        SizeChanged += (_, _) => ScrollToEnd();
        Loaded += (_, _) =>
        {
            var reason = AppBackground.ValidateConfigured();
            ApplyBackground();
            if (reason != null)
            {
                _vm?.Shell.ShowToast(reason);
            }
        };
        AppBackground.Changed += ApplyBackground;
    }

    /// <summary>应用聊天区自定义背景；无背景时恢复默认底色。</summary>
    private void ApplyBackground()
    {
        var image = AppBackground.Current;
        var hasImage = image != null;
        ChatBgImage.Source = image;
        ChatBgImage.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        ChatBgOverlay.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        MessagesScroll.Background = hasImage
            ? Brushes.Transparent
            : (Brush)FindResource("WxPageBrush");
    }

    private void AttachViewModel()
    {
        if (_vm != null)
        {
            _vm.Messages.CollectionChanged -= _messagesHandler;
        }
        _vm = DataContext as ChatPaneViewModel;
        if (_vm != null)
        {
            _vm.Messages.CollectionChanged += _messagesHandler;
        }
    }

    private int _scrollPending;

    private void ScrollToEnd()
    {
        // 合并同一时刻的多次滚动请求，避免批量加载时反复滚动造成闪烁
        if (System.Threading.Interlocked.Exchange(ref _scrollPending, 1) == 1)
        {
            return;
        }
        Dispatcher.BeginInvoke(new Action(() =>
        {
            System.Threading.Interlocked.Exchange(ref _scrollPending, 0);
            MessagesScroll.ScrollToEnd();
        }), DispatcherPriority.Background);
    }

    private async void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            if (_vm != null)
            {
                await _vm.SendCommand.ExecuteAsync(null);
            }
        }
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null)
        {
            return;
        }
        var menu = new ContextMenu();
        if (_vm.IsGroup)
        {
            menu.Items.Add(CreateMenuItem(Loc.T("Chat.InviteMember"), () =>
            {
                var dialog = new InputDialog(Loc.T("Chat.InviteMember"), Loc.T("Chat.InviteHint"), Loc.T("Chat.AccountHint"));
                if (dialog.ShowDialog() == true)
                {
                    _ = _vm.Shell.InviteGroupMemberAsync(dialog.Value);
                }
            }));
        }
        menu.Items.Add(CreateMenuItem(Loc.T("Chat.ClearLocalHistory"), () => _ = _vm.Shell.ClearChatHistoryAsync()));
        menu.Items.Add(CreateMenuItem(Loc.T("Chat.CloseConversation"), _vm.Shell.CloseChat));
        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header, Padding = new Thickness(14, 6, 24, 6) };
        item.Click += (_, _) => action();
        return item;
    }

    private async void CallVoice_Click(object sender, RoutedEventArgs e) => await StartCallAsync("voice");

    private async void CallVideo_Click(object sender, RoutedEventArgs e) => await StartCallAsync("video");

    private async Task StartCallAsync(string kind)
    {
        if (_vm == null) return;
        if (_vm.IsGroup)
        {
            _vm.Shell.ShowToast(Loc.T("Chat.GroupCallUnsupported"));
            return;
        }
        await CallWindow.StartOutgoingAsync(kind, _vm.PeerId, _vm.Title);
    }

    private void Unsupported_Click(object sender, RoutedEventArgs e) =>
        _vm?.Shell.ShowToast(Loc.T("Common.Unsupported"));

    // ==================== 图片右键菜单 ====================

    /// <summary>ContextMenu 会继承 PlacementTarget 的 DataContext，菜单项可直接取到对应数据。</summary>
    private static T? MenuData<T>(object sender) where T : class
    {
        if (sender is not FrameworkElement fe)
        {
            return null;
        }
        if (fe.DataContext is T direct)
        {
            return direct;
        }
        var host = fe.Parent as ContextMenu ??
                   (fe is MenuItem mi ? ItemsControl.ItemsControlFromItemContainer(mi) as ContextMenu : null);
        return host?.PlacementTarget is FrameworkElement target ? target.DataContext as T : null;
    }

    private static ChatMessageVm? MenuMessage(object sender) => MenuData<ChatMessageVm>(sender);

    /// <summary>从消息 URL 推断扩展名（服务端为 guid.ext 形式）。</summary>
    private static string MediaExtension(string? url)
    {
        try
        {
            var ext = Path.GetExtension(new Uri(url!).AbsolutePath);
            return string.IsNullOrEmpty(ext) ? ".png" : ext;
        }
        catch
        {
            return ".png";
        }
    }

    private static string UniquePath(string dir, string fileName)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var i = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(dir, $"{stem}({i++}){ext}");
        }
        return path;
    }

    private async void SaveImageAs_Click(object sender, RoutedEventArgs e)
    {
        var vm = MenuMessage(sender);
        if (vm?.ImageUrl == null || _vm == null)
        {
            return;
        }
        var ext = MediaExtension(vm.ImageUrl);
        var dlg = new SaveFileDialog
        {
            Title = Loc.T("Viewer.SaveImageTitle"),
            FileName = "wxchat_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ext,
            Filter = ext == ".gif" ? Loc.T("Viewer.GifFilter") : Loc.T("Viewer.ImageFilter")
        };
        if (dlg.ShowDialog() != true)
        {
            return;
        }
        try
        {
            await AppServices.Api.DownloadFileAsync(vm.ImageUrl, dlg.FileName);
            _vm.Shell.ShowToast(Loc.T("Chat.ImageSaved", dlg.FileName));
        }
        catch (Exception ex)
        {
            _vm.ErrorText = Loc.T("Chat.SaveImageFail", ex.Message);
        }
    }

    private async void DownloadImage_Click(object sender, RoutedEventArgs e)
    {
        var vm = MenuMessage(sender);
        if (vm?.ImageUrl == null || _vm == null)
        {
            return;
        }
        try
        {
            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var ext = MediaExtension(vm.ImageUrl);
            var path = UniquePath(downloads, "wxchat_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ext);
            await AppServices.Api.DownloadFileAsync(vm.ImageUrl, path);
            _vm.Shell.ShowToast(Loc.T("Chat.DownloadedTo", path));
        }
        catch (Exception ex)
        {
            _vm.ErrorText = Loc.T("Chat.DownloadImageFail", ex.Message);
        }
    }

    private async void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        var vm = MenuMessage(sender);
        if (vm?.ImageUrl == null || _vm == null)
        {
            return;
        }
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "WxChatImage");
            var ext = MediaExtension(vm.ImageUrl);
            var path = UniquePath(dir, "img_" + vm.Id + ext);
            if (!File.Exists(path))
            {
                await AppServices.Api.DownloadFileAsync(vm.ImageUrl, path);
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _vm.ErrorText = Loc.T("Chat.OpenImageFail", ex.Message);
        }
    }

    // ==================== 文件气泡 ====================

    private async void FileBubble_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ChatMessageVm vm } || vm.ImageUrl == null || _vm == null)
        {
            return;
        }
        var dlg = new SaveFileDialog
        {
            Title = Loc.T("Chat.SaveFileTitle"),
            FileName = vm.FileName,
            Filter = Loc.T("Viewer.AllFilesFilter")
        };
        if (dlg.ShowDialog() != true)
        {
            return;
        }
        try
        {
            await AppServices.Api.DownloadFileAsync(vm.ImageUrl, dlg.FileName);
            _vm.Shell.ShowToast(Loc.T("Chat.FileSaved", dlg.FileName));
        }
        catch (Exception ex)
        {
            _vm.ErrorText = Loc.T("Chat.SaveFileFail", ex.Message);
        }
    }

    // ==================== 表情包右键菜单 ====================

    private static StickerItem? MenuSticker(object sender) => MenuData<StickerItem>(sender);

    private void StickerSend_Click(object sender, RoutedEventArgs e)
    {
        var item = MenuSticker(sender);
        if (item != null)
        {
            _vm?.SendStickerCommand.Execute(item.Path);
        }
    }

    private void StickerDelete_Click(object sender, RoutedEventArgs e)
    {
        var item = MenuSticker(sender);
        if (item != null)
        {
            _vm?.RemoveStickerCommand.Execute(item.Path);
        }
    }

    // ==================== 按住说话 ====================

    private void VoiceHold_Down(object sender, MouseButtonEventArgs e) =>
        _vm?.StartVoiceRecording();

    private async void VoiceHold_Up(object sender, MouseButtonEventArgs e)
    {
        if (_vm != null)
        {
            await _vm.FinishVoiceRecordingAsync();
        }
    }

    private void VoiceHold_Leave(object sender, MouseEventArgs e) =>
        _vm?.CancelVoiceRecording();

    // ==================== 消息通用菜单 ====================

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        var vm = MenuMessage(sender);
        if (vm != null)
        {
            var text = vm.MsgType == "text" ? vm.Content
                : vm.MsgType == "voice" ? Loc.T("Chat.CopyVoice", vm.VoiceDurationText)
                : vm.MsgType == "file" ? Loc.T("Chat.CopyFile", vm.FileName, vm.FileSizeText)
                : vm.MsgType == "merge" ? vm.MergePreviewText
                : vm.MsgType == "image" ? Loc.T("Chat.CopyImage") : vm.Content;
            Clipboard.SetText(text);
        }
    }

    private async void ForwardMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        var vm = MenuMessage(sender);
        if (vm == null) return;
        var dialog = new InputDialog(Loc.T("Chat.ForwardTitle"), Loc.T("Chat.ForwardHint"),
            Loc.T("Chat.ForwardHint2")) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;
        var keyword = dialog.Value.Trim();
        try
        {
            var users = await AppServices.Api.SearchUsersAsync(keyword);
            var friend = users.FirstOrDefault(u => u.IsFriend);
            if (friend == null && users.Count > 0) friend = users[0];
            if (friend != null)
            {
                await _vm.SendForwardAsync(vm, friend.Id);
                _vm.Shell.ShowToast(Loc.T("Chat.Forwarded", friend.NickName));
            }
            else
            {
                _vm.Shell.ShowToast(Loc.T("Common.UserNotFound"));
            }
        }
        catch (Exception ex)
        {
            _vm.ErrorText = Loc.T("Chat.ForwardFail", ex.Message);
        }
    }

    /// <summary>聚焦消息输入框（供“打开聊天”快捷键使用）。</summary>
    public void FocusInput()
    {
        InputBox.Focus();
        Keyboard.Focus(InputBox);
    }

    private async void MergeForward_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        if (_vm.SelectedCount == 0)
        {
            _vm.Shell.ShowToast(Loc.T("Chat.SelectFirstToMerge"));
            return;
        }
        var dialog = new InputDialog(Loc.T("Chat.MergeForwardTitle"),
            Loc.T("Chat.MergeForwardHint", _vm.SelectedCount),
            Loc.T("Chat.MergeForwardHint2")) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;
        var keyword = dialog.Value.Trim();
        try
        {
            var users = await AppServices.Api.SearchUsersAsync(keyword);
            var friend = users.FirstOrDefault(u => u.IsFriend) ?? users.FirstOrDefault();
            if (friend == null)
            {
                _vm.Shell.ShowToast(Loc.T("Common.UserNotFound"));
                return;
            }
            await _vm.MergeForwardSelectedAsync(friend.Id);
        }
        catch (Exception ex)
        {
            _vm.ErrorText = Loc.T("Chat.MergeForwardFail", ex.Message);
        }
    }

    private void MergeBubble_Click(object sender, MouseButtonEventArgs e)
    {
        var vm = MenuMessage(sender);
        if (vm == null || !vm.IsMerge) return;
        var lines = vm.MergeEntries.Select(x => Loc.T("Chat.MergeEntryFormat", x.SenderName, x.Content));
        Dialogs.Info(Window.GetWindow(this), string.Join("\n\n", lines), vm.MergeTitle);
    }

    private void ToggleMultiSelect_Click(object sender, RoutedEventArgs e)
    {
        _vm?.ToggleMultiSelectCommand.Execute(null);
    }

    private void DeleteMessage_Click(object sender, RoutedEventArgs e)
    {
        var vm = MenuMessage(sender);
        if (vm == null) return;
        var preview = vm.MsgType == "text"
            ? (vm.Content.Length > 40 ? vm.Content[..40] + "…" : vm.Content)
            : Loc.T("Chat.MsgTypePreview", vm.MsgType);
        var confirm = Dialogs.Confirm(
            Window.GetWindow(this),
            Loc.T("Chat.DeleteMessageConfirm", preview),
            Loc.T("Chat.DeleteMessageTitle"), Loc.T("Common.Delete"));
        if (confirm)
        {
            _vm?.DeleteMessageCommand.Execute(vm);
        }
    }
}
