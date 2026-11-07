using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WxChatApp.Helpers;
using WxChatApp.Services;
using WxChatApp.ViewModels;

namespace WxChatApp.Views;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new ShellViewModel();
        DataContext = _vm;
        AppServices.MainWindow = this;
        _vm.ChatPane.BackRequested += _vm.CloseChat;
        _vm.PropertyChanged += OnVmPropertyChanged;
        AppServices.Chat.IncomingCall += OnIncomingCall;
        Loaded += OnLoaded;
        StateChanged += (_, _) => UpdateMaxButton();
        Closed += OnClosed;
        UpdateNavSelection();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.InitializeAsync();
        }
        catch (Exception ex)
        {
            Dialogs.Warning(null, Loc.T("Main.InitFail", ex.Message), Loc.T("App.Name"));
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.ActiveTab) or nameof(ShellViewModel.IsMeTab))
        {
            UpdateLeftPanel();
            UpdateNavSelection();
        }
    }

    private void UpdateNavSelection()
    {
        if (ChatNav == null || ContactsNav == null || DiscoverNav == null)
        {
            return;
        }
        ChatNav.IsChecked = _vm.ActiveTab == ShellTab.Chat;
        ContactsNav.IsChecked = _vm.ActiveTab == ShellTab.Contacts;
        DiscoverNav.IsChecked = _vm.ActiveTab == ShellTab.Discover;
    }

    private void UpdateLeftPanel()
    {
        if (LeftPanelColumn == null)
        {
            return;
        }
        LeftPanelColumn.Width = _vm.IsMeTab ? new GridLength(0) : new GridLength(208);
    }

    private void UpdateMaxButton()
    {
        MaxButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void MeAvatar_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectTabCommand.Execute("me");

    private void Unsupported_Click(object sender, RoutedEventArgs e) =>
        _vm.ShowToast(Loc.T("Common.Unsupported"));

    /// <summary>头像加载失败时隐藏图片，露出底色与首字母占位，避免出现空白。</summary>
    private void Avatar_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Image image)
        {
            image.Visibility = Visibility.Collapsed;
        }
    }

    // ---------------- 音视频通话 ----------------

    private void OnIncomingCall(string callId, string fromId, string fromName, string kind)
    {
        // ChatConnection 已保证在 Dispatcher 上触发，直接调用即可
        CallWindow.ShowIncoming(callId, fromId, fromName, kind);
    }

    private async void CallContactVoice_Click(object sender, RoutedEventArgs e) =>
        await StartContactCallAsync("voice");

    private async void CallContactVideo_Click(object sender, RoutedEventArgs e) =>
        await StartContactCallAsync("video");

    private async Task StartContactCallAsync(string kind)
    {
        var contact = _vm.SelectedContact;
        if (contact == null)
        {
            _vm.ShowToast(Loc.T("Contact.SelectFirst"));
            return;
        }
        await CallWindow.StartOutgoingAsync(kind, contact.Id, contact.DisplayName);
    }

    // ---------------- 公众号 ----------------

    private void OfficialAll_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.SelectedOfficialAccount = null;
        _ = _vm.LoadOfficialMessagesAsync(null);
    }

    private void OfficialMessage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.SelectedOfficialMessage = OfficialMessageList.SelectedItem as Models.OfficialMessageDto;
    }

    private void OfficialDetailBack_Click(object sender, RoutedEventArgs e)
    {
        _vm.SelectedOfficialMessage = null;
        OfficialMessageList.SelectedItem = null;
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm?.DoSearchCommand != null && _vm.DoSearchCommand.CanExecute(null))
        {
            _vm.DoSearchCommand.Execute(null);
        }
    }

    private async void SearchRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ShellViewModel.SearchResultItem item })
        {
            await _vm.OpenSearchResultAsync(item);
        }
    }

    private async void EditContactTag_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedContact == null) return;
        var dlg = new InputDialog(Loc.T("Contact.SetTagTitle"),
            Loc.T("Contact.SetTagMessage", _vm.SelectedContact.DisplayName),
            Loc.T("Contact.SetTagHint"),
            _vm.SelectedContact.Tag)
        {
            Owner = this
        };
        if (dlg.ShowDialog() != true) return;
        var tag = (dlg.Value ?? "").Trim();
        if (tag.Length > 30)
        {
            _vm.ShowToast(Loc.T("Contact.TagTooLong"));
            return;
        }
        await _vm.UpdateContactTagAsync(_vm.SelectedContact.Id, tag);
    }

    private void EditContactProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedContact == null) return;
        var dlg = new EditContactWindow(_vm.SelectedContact, _vm) { Owner = this };
        dlg.ShowDialog();
    }

    private void DiscoverMoments_Click(object sender, RoutedEventArgs e) =>
        _ = _vm.OpenDiscoverMomentsAsync();

    private void DiscoverOfficial_Click(object sender, RoutedEventArgs e) =>
        _ = _vm.OpenDiscoverOfficialAsync();

    private void AddFriend_Click(object sender, RoutedEventArgs e) => PromptAddFriend();

    private void PromptAddFriend()
    {
        var dialog = new InputDialog(Loc.T("Contact.AddFriendTitle"), Loc.T("Contact.AddFriendMessage"),
            Loc.T("Contact.AddFriendHint"));
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        _ = _vm.AddFriendAsync(dialog.Value);
    }

    private void PlusMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem(Loc.T("Main.NewGroup"), () =>
        {
            var dialog = new CreateGroupWindow(_vm);
            dialog.ShowDialog();
        }));
        menu.Items.Add(CreateMenuItem(Loc.T("Contact.AddFriendTitle"), PromptAddFriend));
        menu.Items.Add(CreateMenuItem(Loc.T("Discover.Scan"), () => _vm.ShowToast(Loc.T("Discover.ScanTip"))));
        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header, Padding = new Thickness(14, 6, 24, 6) };
        item.Click += (_, _) => action();
        return item;
    }

    private void SendMessageToContact_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedContact != null)
        {
            _ = _vm.StartChatWithContactAsync(_vm.SelectedContact);
        }
    }

    private bool _profileDialogOpen;
    private bool _settingsDialogOpen;

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsDialogOpen)
        {
            return;
        }
        _settingsDialogOpen = true;
        try
        {
            var dialog = new SettingsWindow(_vm) { Owner = this };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            Dialogs.Error(this, Loc.T("Settings.OpenFail", ex.Message), Loc.T("Settings.Title"));
        }
        finally
        {
            _settingsDialogOpen = false;
        }
    }

    /// <summary>处理本机快捷键（发送消息 / 聚焦输入框），可在设置里录制修改。</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var send = LocalStore.Settings.Shortcuts.TryGetValue("send", out var s) ? s : "Ctrl+Enter";
        if (_vm.ChatPane.IsOpen && Matches(send, e))
        {
            e.Handled = true;
            _vm.ChatPane.SendCommand.Execute(null);
            return;
        }
        var openChat = LocalStore.Settings.Shortcuts.TryGetValue("openChat", out var o) ? o : "Ctrl+F";
        if (_vm.ChatPane.IsOpen && Matches(openChat, e))
        {
            e.Handled = true;
            ChatPaneHost.FocusInput();
        }
    }

    private static bool Matches(string combo, KeyEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(combo))
        {
            return false;
        }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var parts = combo.Split('+');
        if (parts[^1] != key.ToString())
        {
            return false;
        }
        var mods = Keyboard.Modifiers;
        var ctrl = parts.Contains("Ctrl", StringComparer.OrdinalIgnoreCase);
        var alt = parts.Contains("Alt", StringComparer.OrdinalIgnoreCase);
        var shift = parts.Contains("Shift", StringComparer.OrdinalIgnoreCase);
        var win = parts.Contains("Win", StringComparer.OrdinalIgnoreCase);
        return ((mods & ModifierKeys.Control) != 0) == ctrl
               && ((mods & ModifierKeys.Alt) != 0) == alt
               && ((mods & ModifierKeys.Shift) != 0) == shift
               && ((mods & ModifierKeys.Windows) != 0) == win;
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_profileDialogOpen)
        {
            return;
        }
        _profileDialogOpen = true;
        try
        {
            var dialog = new EditProfileWindow(_vm) { Owner = this };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            Dialogs.Warning(this, Loc.T("Me.ProfileOpenFail", ex.Message), Loc.T("Me.Profile"));
        }
        finally
        {
            _profileDialogOpen = false;
        }
    }

    private void ContactPreviewImage_Click(object sender, MouseButtonEventArgs e)
    {
        var images = _vm.SelectedContactImages;
        if (images == null || images.Count == 0) return;
        var current = sender is Image img && img.DataContext is string u ? u : null;
        var items = images.Select(url => new MediaItem
        {
            Url = url,
            FileName = Path.GetFileName(new Uri(url).AbsolutePath)
        }).ToList();
        var idx = current != null ? items.FindIndex(i => i.Url == current) : 0;
        if (idx < 0) idx = 0;
        var viewer = new ViewerWindow(items, idx) { Owner = this };
        viewer.ShowDialog();
    }

    private void MyMoments_Click(object sender, RoutedEventArgs e)
    {
        _vm.SelectTabCommand.Execute("discover");
        _vm.Moments.OnlyMine = true;
        _ = _vm.Moments.LoadAsync();
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (!Dialogs.Confirm(this, Loc.T("Me.ClearHistoryConfirm"), Loc.T("Me.ClearHistory"), Loc.T("Me.ClearAction")))
        {
            return;
        }
        await _vm.ClearChatHistoryAsync();
    }

    private async void SignOut_Click(object sender, RoutedEventArgs e)
    {
        if (!Dialogs.Confirm(this, Loc.T("Me.SignOutConfirm"), Loc.T("Me.SignOut"), Loc.T("Me.SignOutAction")))
        {
            return;
        }
        await _vm.SignOutAsync();
        App.ReturnToLogin(this);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        AppServices.Chat.IncomingCall -= OnIncomingCall;
        _vm.Dispose();
        _ = AppServices.Chat.DisconnectAsync();
        if (!Application.Current.Windows.OfType<LoginWindow>().Any())
        {
            Application.Current.Shutdown();
        }
    }
}
