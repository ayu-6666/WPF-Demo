using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WxChatApp.Helpers;
using WxChatApp.Models;
using WxChatApp.Services;

namespace WxChatApp.ViewModels;

public enum ShellTab
{
    Chat,
    Contacts,
    Discover,
    Search,
    Me
}

public enum DiscoverSection
{
    Moments,
    Official
}

public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _conversationTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly List<ConversationItemVm> _allConversations = new();
    private bool _suppressSelection;
    private bool _disposed;

    public ShellViewModel()
    {
        ContactsView = CollectionViewSource.GetDefaultView(Contacts);
        ContactsView.Filter = FilterContact;
        ContactsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ContactItemVm.Initial)));
        Loc.LanguageChanged += OnLanguageChanged;

        ChatPane = new ChatPaneViewModel(this);
        ChatPane.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatPaneViewModel.IsOpen))
            {
                OnPropertyChanged(nameof(HasChatOpen));
            }
        };
        Moments = new MomentsViewModel(this);

        _conversationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _conversationTimer.Tick += async (_, _) =>
        {
            _conversationTimer.Stop();
            await RefreshConversationsAsync();
        };
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            Toast = "";
        };
    }

    public ObservableCollection<ConversationItemVm> Conversations { get; } = new();
    public ObservableCollection<ContactItemVm> Contacts { get; } = new();
    public ICollectionView ContactsView { get; }
    public ChatPaneViewModel ChatPane { get; }
    public MomentsViewModel Moments { get; }

    // ---------------- 公众号 ----------------
    public ObservableCollection<OfficialAccountDto> OfficialAccounts { get; } = new();
    public ObservableCollection<OfficialMessageDto> OfficialMessages { get; } = new();
    [ObservableProperty] private OfficialAccountDto? _selectedOfficialAccount;
    [ObservableProperty] private OfficialMessageDto? _selectedOfficialMessage;
    public bool HasSelectedOfficialMessage => SelectedOfficialMessage != null;
    public bool HasOfficialAccounts => OfficialAccounts.Count > 0;

    partial void OnSelectedOfficialMessageChanged(OfficialMessageDto? value) =>
        OnPropertyChanged(nameof(HasSelectedOfficialMessage));

    partial void OnSelectedOfficialAccountChanged(OfficialAccountDto? value)
    {
        _ = LoadOfficialMessagesAsync(value?.Id);
    }

    /// <summary>加载公众号列表与全部图文（进入公众号页时调用）。</summary>
    public async Task LoadOfficialAsync()
    {
        try
        {
            var accounts = await AppServices.Api.GetOfficialAccountsAsync();
            OfficialAccounts.Clear();
            foreach (var a in accounts) OfficialAccounts.Add(a);
            OnPropertyChanged(nameof(HasOfficialAccounts));
            await LoadOfficialMessagesAsync(null);
        }
        catch (Exception ex)
        {
            ShowToast(Loc.T("Official.LoadFail", ex.Message));
        }
    }

    /// <summary>按公众号筛选图文；accountId 为 null 时显示全部。</summary>
    public async Task LoadOfficialMessagesAsync(string? accountId)
    {
        try
        {
            var messages = await AppServices.Api.GetOfficialMessagesAsync(accountId);
            OfficialMessages.Clear();
            foreach (var m in messages) OfficialMessages.Add(m);
            SelectedOfficialMessage = null;
        }
        catch (Exception ex)
        {
            if (!IsOffline) ShowToast(Loc.T("Official.MessagesLoadFail", ex.Message));
        }
    }

    [ObservableProperty] private string _searchKeyword = "";
    [ObservableProperty] private bool _searchLoading;
    [ObservableProperty] private ObservableCollection<SearchResultItem> _searchResults = new();
    [ObservableProperty] private ObservableCollection<SearchResultItem> _searchContactResults = new();
    [ObservableProperty] private ObservableCollection<SearchResultItem> _searchMessageResults = new();
    [ObservableProperty] private ObservableCollection<SearchResultItem> _searchMomentResults = new();
    [ObservableProperty] private string _searchCategory = "all";
    [ObservableProperty] private bool _haveSearched;
    [ObservableProperty] private string _searchCategoryHint = "";

    /// <summary>搜一搜分类；“全部/联系人/聊天记录/朋友圈”已接通数据，其余分类给出明确提示。</summary>
    public static readonly string[] SupportedSearchCategories = { "all", "friend", "message", "moment" };

    public static readonly string[] UnsupportedSearchCategories =
        { "article", "official", "channels", "sticker", "mini" };

    public IReadOnlyList<string> SearchHotItems => new[]
    {
        Loc.T("Search.Hot1"), Loc.T("Search.Hot2"), Loc.T("Search.Hot3"),
        Loc.T("Search.Hot4"), Loc.T("Search.Hot5"), Loc.T("Search.Hot6")
    };

    public bool ShowSearchHot => !HaveSearched;
    public bool ShowSearchEmpty => HaveSearched && SearchResults.Count == 0 && SearchCategoryHint.Length == 0;
    public bool HasSearchContacts => SearchContactResults.Count > 0;
    public bool HasSearchMessages => SearchMessageResults.Count > 0;
    public bool HasSearchMoments => SearchMomentResults.Count > 0;
    public bool HasSearchCategoryHint => SearchCategoryHint.Length > 0;

    partial void OnSearchCategoryHintChanged(string value) =>
        OnPropertyChanged(nameof(HasSearchCategoryHint));

    [RelayCommand]
    private void SelectSearchCategory(string? key)
    {
        if (key == null) return;
        SearchCategory = key;
        SearchCategoryHint = "";
        if (SupportedSearchCategories.Contains(key))
        {
            if (HaveSearched && SearchKeyword.Trim().Length >= 2)
            {
                _ = DoSearchAsync();
            }
            else
            {
                NotifySearchState();
            }
            return;
        }
        // 未接通数据的分类：给出明确提示，而不是留下空白结果区。
        SearchResults = new ObservableCollection<SearchResultItem>();
        SearchContactResults = new ObservableCollection<SearchResultItem>();
        SearchMessageResults = new ObservableCollection<SearchResultItem>();
        SearchMomentResults = new ObservableCollection<SearchResultItem>();
        SearchCategoryHint = Loc.T("Search.CategoryUnsupported", CategoryTitle(key));
        HaveSearched = false;
        NotifySearchState();
    }

    [RelayCommand]
    private void PickSearchHot(string? keyword)
    {
        if (keyword == null) return;
        SearchKeyword = keyword;
        _ = DoSearchAsync();
    }

    private static string CategoryTitle(string key) => Loc.T(key switch
    {
        "friend" => "Search.CatFriend",
        "message" => "Search.CatMessage",
        "moment" => "Search.CatMoment",
        "article" => "Search.CatArticle",
        "official" => "Search.CatOfficial",
        "channels" => "Search.CatChannels",
        "sticker" => "Search.CatSticker",
        "mini" => "Search.CatMini",
        _ => "Search.CatAll"
    });

    [RelayCommand]
    private async Task DoSearchAsync()
    {
        var kw = SearchKeyword.Trim();
        if (kw.Length < 2)
        {
            ShowToast(Loc.T("Search.MinLength"));
            return;
        }
        if (!SupportedSearchCategories.Contains(SearchCategory))
        {
            SearchCategoryHint = Loc.T("Search.CategoryUnsupported", CategoryTitle(SearchCategory));
            return;
        }
        SearchLoading = true;
        SearchCategoryHint = "";
        try
        {
            var results = new List<SearchResultItem>();
            var wantFriends = SearchCategory is "all" or "friend";
            var wantMessages = SearchCategory is "all" or "message";
            var wantMoments = SearchCategory is "all" or "moment";

            // 1) 好友（本地过滤：昵称 / 备注 / 微信号 / 标签 / 账号）
            if (wantFriends)
            {
                foreach (var c in Contacts)
                {
                    if (!Match(c.NickName) && !Match(c.Remark) && !Match(c.WxId) &&
                        !Match(c.Tag) && !Match(c.Account)) continue;
                    var tagText = c.HasTag ? Loc.T("Search.TagSuffix", c.Tag) : "";
                    results.Add(new SearchResultItem
                    {
                        KindKey = "friend",
                        Id = c.Id,
                        Content = c.DisplayName,
                        Detail = Loc.T("Search.FriendDetail", c.WxIdText, tagText)
                    });
                }
            }

            // 2) 站内消息 + 3) 朋友圈（并行请求服务端）
            var msgTask = wantMessages ? AppServices.Api.SearchMessagesAsync(kw, 50) : null;
            var momentTask = wantMoments ? AppServices.Api.SearchMomentsAsync(kw, 50) : null;
            try
            {
                var tasks = new List<Task>();
                if (msgTask != null) tasks.Add(msgTask);
                if (momentTask != null) tasks.Add(momentTask);
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                ShowToast(Loc.T("Search.PartialFail", ex.Message));
            }

            if (msgTask != null && msgTask.Status == TaskStatus.RanToCompletion)
            {
                results.AddRange(msgTask.Result.Select(m => new SearchResultItem
                {
                    KindKey = "message",
                    Id = m.Id,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    Detail = Loc.T("Search.MessageDetail", m.SenderName,
                        m.IsMine ? Loc.T("Search.FromMine") : Loc.T("Search.FromOther"))
                }));
            }

            if (momentTask != null && momentTask.Status == TaskStatus.RanToCompletion)
            {
                results.AddRange(momentTask.Result.Select(p => new SearchResultItem
                {
                    KindKey = "moment",
                    Id = p.Id,
                    Content = string.IsNullOrWhiteSpace(p.Content) ? Loc.T("Chat.ImageTag") : p.Content,
                    CreatedAt = p.CreatedAt,
                    Detail = Loc.T("Search.MomentDetail", p.UserName)
                }));
            }

            SearchResults = new ObservableCollection<SearchResultItem>(results);
            SearchContactResults = new ObservableCollection<SearchResultItem>(results.Where(r => r.KindKey == "friend"));
            SearchMessageResults = new ObservableCollection<SearchResultItem>(results.Where(r => r.KindKey == "message"));
            SearchMomentResults = new ObservableCollection<SearchResultItem>(results.Where(r => r.KindKey == "moment"));
            HaveSearched = true;
            NotifySearchState();
            ShowToast(results.Count == 0 ? Loc.T("Search.NoResult") : Loc.T("Search.Found", results.Count));

            bool Match(string? s) =>
                !string.IsNullOrEmpty(s) && s.Contains(kw, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            ShowToast(Loc.T("Search.Fail", ex.Message));
        }
        finally
        {
            SearchLoading = false;
        }
    }

    private void NotifySearchState()
    {
        OnPropertyChanged(nameof(SearchCountText));
        OnPropertyChanged(nameof(ShowSearchHot));
        OnPropertyChanged(nameof(ShowSearchEmpty));
        OnPropertyChanged(nameof(HasSearchContacts));
        OnPropertyChanged(nameof(HasSearchMessages));
        OnPropertyChanged(nameof(HasSearchMoments));
        OnPropertyChanged(nameof(HasSearchCategoryHint));
    }

    /// <summary>双击搜索结果：好友 → 通讯录详情，朋友圈 → 发现页，消息 → 提示。</summary>
    public async Task OpenSearchResultAsync(SearchResultItem? item)
    {
        if (item == null) return;
        switch (item.KindKey)
        {
            case "friend":
                ActiveTab = ShellTab.Contacts;
                SelectedContact = Contacts.FirstOrDefault(c => c.Id == item.Id);
                break;
            case "moment":
                ActiveTab = ShellTab.Discover;
                await Moments.LoadAsync();
                break;
            default:
                ShowToast(Loc.T("Search.Located"));
                break;
        }
    }

    public sealed class SearchResultItem : ObservableObject
    {
        public string KindKey { get; set; } = "message";
        public string Id { get; set; } = "";
        public string Content { get; set; } = "";
        public string Detail { get; set; } = "";
        public string CreatedAt { get; set; } = "";

        public string KindText => Loc.T(KindKey switch
        {
            "friend" => "Search.KindFriend",
            "moment" => "Search.KindMoment",
            _ => "Search.KindMessage"
        });

        public void NotifyLanguage() => OnPropertyChanged(nameof(KindText));
    }

    [ObservableProperty] private UserDto? _me;
    [ObservableProperty] private ShellTab _activeTab = ShellTab.Chat;
    [ObservableProperty] private DiscoverSection _discoverSection = DiscoverSection.Moments;
    [ObservableProperty] private string _conversationSearch = "";
    [ObservableProperty] private string _contactSearch = "";
    [ObservableProperty] private ConversationItemVm? _selectedConversation;
    [ObservableProperty] private ContactItemVm? _selectedContact;
    [ObservableProperty] private bool _hasNewMoments;
    [ObservableProperty] private bool _isOffline;
    [ObservableProperty] private string _connectionText = "";
    [ObservableProperty] private string _toast = "";
    [ObservableProperty] private int _totalUnread;
    [ObservableProperty] private bool _isReady;
    [ObservableProperty] private IReadOnlyList<string> _selectedContactImages = Array.Empty<string>();
    [ObservableProperty] private bool _contactsEmpty;
    [ObservableProperty] private bool _notificationsEnabled = LocalStore.Settings.NotificationsEnabled;

    partial void OnNotificationsEnabledChanged(bool value)
    {
        LocalStore.Settings.NotificationsEnabled = value;
        LocalStore.Save();
        ShowToast(value ? Loc.T("Me.NotificationsOn") : Loc.T("Me.NotificationsOff"));
    }

    public bool IsChatTab => ActiveTab == ShellTab.Chat;
    public bool IsContactsTab => ActiveTab == ShellTab.Contacts;
    public bool IsDiscoverTab => ActiveTab == ShellTab.Discover;
    public bool IsSearchTab => ActiveTab == ShellTab.Search;
    public bool IsDiscoverMoments => ActiveTab == ShellTab.Discover && DiscoverSection == DiscoverSection.Moments;
    public bool IsDiscoverOfficial => ActiveTab == ShellTab.Discover && DiscoverSection == DiscoverSection.Official;
    public bool IsMeTab => ActiveTab == ShellTab.Me;
    public bool HasChatOpen => ChatPane.IsOpen;
    public bool HasSelectedContact => SelectedContact != null;
    public bool ShowContactEmpty => ActiveTab == ShellTab.Contacts && SelectedContact == null;
    public bool HasContacts => Contacts.Count > 0;
    public bool HasSelectedContactMoments => SelectedContactImages.Count > 0;

    public string MeName => Me?.NickName ?? "";
    public string MeWxId
    {
        get
        {
            if (Me == null) return Loc.T("Me.WxIdNotSet");
            var id = string.IsNullOrWhiteSpace(Me.WxId) ? Me.Account : Me.WxId;
            return string.IsNullOrWhiteSpace(id) ? Loc.T("Me.WxIdNotSet") : Loc.T("Me.WxIdPrefix", id);
        }
    }
    public string MePhone => string.IsNullOrWhiteSpace(Me?.Phone) ? "" : Loc.T("Me.PhonePrefix", Me!.Phone);
    public string MeSignature => Me?.Signature ?? "";
    public string MeAvatarText => AvatarHelper.Initial(MeName);
    public Brush MeAvatarBrush => AvatarHelper.BrushFor(Me?.Id ?? "me");
    public string MeAvatarUrl => Me?.Avatar ?? "";
    public bool MeHasAvatar => AvatarHelper.HasAvatar(Me?.Avatar);
    public string TotalUnreadText => TotalUnread > 99 ? "99+" : TotalUnread.ToString();

    partial void OnActiveTabChanged(ShellTab value)
    {
        OnPropertyChanged(nameof(IsChatTab));
        OnPropertyChanged(nameof(IsContactsTab));
        OnPropertyChanged(nameof(IsDiscoverTab));
        OnPropertyChanged(nameof(IsSearchTab));
        OnPropertyChanged(nameof(IsDiscoverMoments));
        OnPropertyChanged(nameof(IsDiscoverOfficial));
        OnPropertyChanged(nameof(IsMeTab));
        OnPropertyChanged(nameof(ShowContactEmpty));
    }

    partial void OnDiscoverSectionChanged(DiscoverSection value)
    {
        OnPropertyChanged(nameof(IsDiscoverMoments));
        OnPropertyChanged(nameof(IsDiscoverOfficial));
    }

    partial void OnMeChanged(UserDto? value)
    {
        OnPropertyChanged(nameof(MeName));
        OnPropertyChanged(nameof(MeWxId));
        OnPropertyChanged(nameof(MePhone));
        OnPropertyChanged(nameof(MeSignature));
        OnPropertyChanged(nameof(MeAvatarText));
        OnPropertyChanged(nameof(MeAvatarBrush));
        OnPropertyChanged(nameof(MeAvatarUrl));
        OnPropertyChanged(nameof(MeHasAvatar));
        Moments.NotifyOwnerChanged();
    }

    partial void OnSelectedContactImagesChanged(IReadOnlyList<string> value) =>
        OnPropertyChanged(nameof(HasSelectedContactMoments));

    partial void OnTotalUnreadChanged(int value) => OnPropertyChanged(nameof(TotalUnreadText));

    partial void OnConversationSearchChanged(string value) => RebuildConversations();

    partial void OnContactSearchChanged(string value)
    {
        ContactsView.Refresh();
        OnPropertyChanged(nameof(HasContacts));
    }

    partial void OnSelectedContactChanged(ContactItemVm? value)
    {
        foreach (var contact in Contacts)
        {
            contact.IsSelected = contact == value;
        }
        OnPropertyChanged(nameof(HasSelectedContact));
        OnPropertyChanged(nameof(ShowContactEmpty));
        RefreshContactMoments();
    }

    partial void OnSelectedConversationChanged(ConversationItemVm? value)
    {
        foreach (var conversation in Conversations)
        {
            conversation.IsSelected = conversation == value;
        }
        if (value != null)
        {
            value.Unread = 0;
            RecalculateUnread();
            if (!_suppressSelection && !ChatPane.Matches(value.Type, value.Id))
            {
                // 同一会话已打开时跳过，避免重复加载清空输入框造成闪烁
                _ = OpenChatAsync(value);
            }
        }
        else if (!_suppressSelection)
        {
            ChatPane.IsOpen = false;
        }
    }

    public async Task InitializeAsync()
    {
        Me = AppServices.CurrentUser ?? await AppServices.Api.GetMeAsync();
        AppServices.CurrentUser = Me;

        var chat = AppServices.Chat;

        // 先注册事件，再连接 SignalR —— 避免连接成功后触发事件但订阅者还没准备好
        chat.MessageReceived += ChatOnMessageReceived;
        chat.ReadReceipt += ChatOnReadReceipt;
        chat.ConversationUpdated += ChatOnConversationUpdated;
        chat.MomentChanged += ChatOnMomentChanged;
        chat.Reconnected += ChatOnReconnected;
        chat.StatusChanged += ChatOnStatusChanged;

        // SignalR 连接：最多等 15 秒（ChatConnection.ConnectAsync 内部有超时保护）
        try
        {
            await chat.ConnectAsync(AppServices.Token ?? "");
        }
        catch (TimeoutException ex)
        {
            IsOffline = true;
            ConnectionText = Loc.T("Connection.Timeout");
            ShowToast(Loc.T("Toast.ConnectTimeout"));
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WxChatApp", "crash.log"),
                $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss}  InitializeAsync.Connect.Timeout ===\n{ex}\n\n");
        }
        catch (Exception ex)
        {
            IsOffline = true;
            ConnectionText = Loc.T("Connection.Unavailable");
            ShowToast(Loc.T("Toast.ConnectFail"));
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WxChatApp", "crash.log"),
                $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss}  InitializeAsync.Connect ===\n{ex}\n\n");
        }

        UpdateConnectionState(chat.IsConnected ? "connected" : "disconnected");

        try
        {
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            ShowToast(Loc.T("Toast.DataLoadFail", ex.Message));
        }

        IsReady = true;
    }

    private async Task RefreshAllAsync()
    {
        await RefreshConversationsAsync();
        await RefreshContactsAsync();
        await Moments.LoadAsync();
    }

    public async Task RefreshConversationsAsync()
    {
        if (_disposed)
        {
            return;
        }
        try
        {
            var dtos = await AppServices.Api.GetConversationsAsync();
            var selectedId = SelectedConversation?.Id;
            // 按 Id 复用现有条目，仅更新可变字段，避免列表整体重建导致闪烁
            var byKey = new Dictionary<string, ConversationItemVm>();
            foreach (var existing in _allConversations)
            {
                byKey[existing.Type + "|" + existing.Id] = existing;
            }
            _allConversations.Clear();
            foreach (var dto in dtos)
            {
                if (!byKey.TryGetValue(dto.Type + "|" + dto.Id, out var item))
                {
                    item = new ConversationItemVm(dto);
                }
                else
                {
                    item.Update(dto);
                }
                _allConversations.Add(item);
            }
            _suppressSelection = true;
            RebuildConversations();
            SelectedConversation = Conversations.FirstOrDefault(c => c.Id == selectedId);
            _suppressSelection = false;
            RecalculateUnread();
        }
        catch (Exception ex)
        {
            if (!IsOffline)
            {
                ShowToast(Loc.T("Toast.ConversationSyncFail", ex.Message));
            }
        }
    }

    private void RebuildConversations()
    {
        var keyword = ConversationSearch.Trim();
        var selectedId = SelectedConversation?.Id;
        _suppressSelection = true;
        Conversations.Clear();
        foreach (var item in _allConversations)
        {
            if (keyword.Length > 0 &&
                !item.Title.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) &&
                !item.LastMessage.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }
            item.IsSelected = item.Id == selectedId;
            Conversations.Add(item);
        }
        _suppressSelection = false;
    }

    public async Task RefreshContactsAsync()
    {
        try
        {
            var dtos = await AppServices.Api.GetContactsAsync();
            var selectedId = SelectedContact?.Id;
            Contacts.Clear();
            foreach (var dto in dtos)
            {
                Contacts.Add(new ContactItemVm(dto));
            }
            ContactsEmpty = Contacts.Count == 0;
            ContactsView.Refresh();
            SelectedContact = Contacts.FirstOrDefault(c => c.Id == selectedId);
            OnPropertyChanged(nameof(HasContacts));
        }
        catch (Exception ex)
        {
            if (!IsOffline)
            {
                ShowToast(Loc.T("Toast.ContactsSyncFail", ex.Message));
            }
        }
    }

    private bool FilterContact(object obj)
    {
        if (obj is not ContactItemVm contact)
        {
            return false;
        }
        var keyword = ContactSearch.Trim();
        if (keyword.Length == 0)
        {
            return true;
        }
        return contact.DisplayName.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
               contact.Account.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
               contact.WxId.Contains(keyword, StringComparison.CurrentCultureIgnoreCase);
    }

    public void RefreshContactMoments()
    {
        var contact = SelectedContact;
        if (contact == null)
        {
            SelectedContactImages = Array.Empty<string>();
            return;
        }
        SelectedContactImages = Moments.AllItems
            .Where(m => m.UserId == contact.Id)
            .SelectMany(m => m.Images)
            .Take(4)
            .ToList();
    }

    private void RecalculateUnread()
    {
        TotalUnread = _allConversations.Sum(c => c.Unread);
    }

    private async Task OpenChatAsync(ConversationItemVm conversation)
    {
        var peerId = conversation.Type == "single" ? DerivePeerId(conversation.Id) : null;
        ActiveTab = ShellTab.Chat;
        await ChatPane.OpenAsync(conversation.Type, conversation.Id, conversation.Title,
            conversation.IsGroup, conversation.MemberCount, peerId, conversation.AvatarUrl);
    }

    private static string DerivePeerId(string convId)
    {
        var me = AppServices.CurrentUser?.Id ?? "";
        return convId.Split('_', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(x => x != me) ?? "";
    }

    public async Task OpenConversationByIdAsync(string type, string convId)
    {
        await RefreshConversationsAsync();
        var conversation = _allConversations.FirstOrDefault(c => c.Type == type && c.Id == convId);
        if (conversation != null)
        {
            SelectedConversation = conversation;
        }
    }

    public void CloseChat()
    {
        ChatPane.IsOpen = false;
        _suppressSelection = true;
        SelectedConversation = null;
        _suppressSelection = false;
        foreach (var conversation in Conversations)
        {
            conversation.IsSelected = false;
        }
    }

    [RelayCommand]
    private async Task SelectTab(string? tab)
    {
        var wantsOfficial = tab == "official";
        ActiveTab = tab switch
        {
            "contacts" => ShellTab.Contacts,
            "discover" => ShellTab.Discover,
            "search" => ShellTab.Search,
            "official" => ShellTab.Discover,
            "me" => ShellTab.Me,
            _ => ShellTab.Chat
        };
        if (ActiveTab == ShellTab.Discover)
        {
            DiscoverSection = wantsOfficial ? DiscoverSection.Official : DiscoverSection.Moments;
            if (DiscoverSection == DiscoverSection.Official)
            {
                await LoadOfficialAsync();
            }
            else
            {
                HasNewMoments = false;
                await Moments.LoadAsync();
            }
        }
        if (ActiveTab == ShellTab.Contacts)
        {
            await RefreshContactsAsync();
        }
    }

    public async Task OpenDiscoverMomentsAsync()
    {
        ActiveTab = ShellTab.Discover;
        DiscoverSection = DiscoverSection.Moments;
        HasNewMoments = false;
        await Moments.LoadAsync();
    }

    public async Task OpenDiscoverOfficialAsync()
    {
        ActiveTab = ShellTab.Discover;
        DiscoverSection = DiscoverSection.Official;
        await LoadOfficialAsync();
    }

    [RelayCommand]
    private void OpenConversation(ConversationItemVm? conversation)
    {
        if (conversation != null)
        {
            SelectedConversation = conversation;
        }
    }

    [RelayCommand]
    private void OpenContact(ContactItemVm? contact)
    {
        if (contact == null)
        {
            return;
        }
        ActiveTab = ShellTab.Contacts;
        SelectedContact = contact;
    }

    public async Task<bool> AddFriendAsync(string account)
    {
        try
        {
            await AppServices.Api.AddFriendAsync(account.Trim());
            ShowToast(Loc.T("Friend.Added"));
            await RefreshContactsAsync();
            await RefreshConversationsAsync();
            return true;
        }
        catch (ApiException ex)
        {
            ShowToast(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            ShowToast(Loc.T("Friend.AddFail", ex.Message));
            return false;
        }
    }

    public async Task<bool> CreateGroupAsync(string name, IReadOnlyList<string> memberIds)
    {
        try
        {
            var group = await AppServices.Api.CreateGroupAsync(name, memberIds);
            await RefreshConversationsAsync();
            await OpenConversationByIdAsync("group", group.Id);
            ShowToast(Loc.T("Group.Created"));
            return true;
        }
        catch (Exception ex)
        {
            ShowToast(Loc.T("Group.CreateFail", ex.Message));
            return false;
        }
    }

    public async Task<bool> InviteGroupMemberAsync(string account)
    {
        if (!ChatPane.IsGroup)
        {
            return false;
        }
        try
        {
            await AppServices.Api.AddGroupMemberAsync(ChatPane.ConvId, account.Trim());
            await ChatPane.ReloadMembersAsync();
            await RefreshConversationsAsync();
            ShowToast(Loc.T("Group.Invited"));
            return true;
        }
        catch (Exception ex)
        {
            ShowToast(Loc.T("Group.InviteFail", ex.Message));
            return false;
        }
    }

    /// <summary>更新好友标签并同步刷新 UI。</summary>
    public async Task<bool> UpdateContactTagAsync(string contactId, string tag)
    {
        try
        {
            await AppServices.Api.UpdateFriendTagAsync(contactId, tag.Trim());
            var target = Contacts.FirstOrDefault(c => c.Id == contactId);
            if (target != null) target.Tag = tag.Trim();
            ShowToast(Loc.T("Contact.TagUpdated"));
            return true;
        }
        catch (Exception ex)
        {
            ShowToast(Loc.T("Contact.TagUpdateFail", ex.Message));
            return false;
        }
    }

    /// <summary>更新好友备注/标签/分组：真实写入后端并回读校验，成功后刷新通讯录与会话标题。</summary>
    public async Task<bool> UpdateContactProfileAsync(string contactId, string remark, string tag, string group)
    {
        var rem = remark.Trim();
        var tg = tag.Trim();
        var grp = group.Trim();
        try
        {
            await AppServices.Api.UpdateFriendProfileAsync(contactId, rem, tg, grp);
            // 回读校验：以服务端最新通讯录为准，避免静默失败或本地显示与服务端不一致。
            var contacts = await AppServices.Api.GetContactsAsync();
            var readback = contacts.FirstOrDefault(c => c.Id == contactId);
            if (readback == null ||
                readback.Remark != rem ||
                (readback.Tag ?? "") != tg ||
                (readback.Group ?? "") != grp)
            {
                ShowToast(Loc.T("Contact.ProfileVerifyFail"));
                return false;
            }
            await RefreshContactsAsync();
            await RefreshConversationsAsync();
            ShowToast(Loc.T("Contact.ProfileUpdated"));
            return true;
        }
        catch (Exception ex)
        {
            ShowToast(Loc.T("Contact.ProfileUpdateFail", ex.Message));
            return false;
        }
    }

    /// <summary>最近一次个人资料保存失败的服务端提示，供编辑窗口直接展示。</summary>
    public string LastProfileError { get; private set; } = "";

    public async Task<bool> UpdateProfileAsync(string nickname, string signature, string wxid, string phone, string? avatarUrl = null)
    {
        LastProfileError = "";
        try
        {
            var updated = await AppServices.Api.UpdateProfileAsync(nickname.Trim(), signature.Trim(), wxid.Trim(), phone.Trim(), avatarUrl);
            AppServices.CurrentUser = updated;
            Me = updated;
            // 头像/资料变更后清掉内存图片缓存，并把通讯录、朋友圈、会话列表一起刷新，
            // 避免只在当前窗口生效。
            AvatarHelper.ClearCache();
            await RefreshContactsAsync();
            await RefreshConversationsAsync();
            await Moments.LoadAsync();
            if (ChatPane.IsOpen)
            {
                // 重新拉取消息，刷新聊天气泡上的头像
                await ChatPane.ReloadAsync();
            }
            ShowToast(Loc.T("Me.ProfileUpdated"));
            return true;
        }
        catch (Exception ex)
        {
            LastProfileError = ex.Message;
            ShowToast(Loc.T("Me.ProfileUpdateFail", ex.Message));
            return false;
        }
    }

    public async Task ClearChatHistoryAsync()
    {
        var now = DateTime.UtcNow;
        foreach (var conversation in _allConversations)
        {
            LocalStore.SetClearedBefore(conversation.Id, now);
        }
        if (ChatPane.IsOpen)
        {
            await ChatPane.ReloadAsync();
        }
        ShowToast(Loc.T("Me.HistoryCleared"));
    }

    public async Task StartChatWithContactAsync(ContactItemVm contact)
    {
        await RefreshConversationsAsync();
        var conversation = _allConversations.FirstOrDefault(c => c.Type == "single" && c.Id.Contains(contact.Id));
        if (conversation == null)
        {
            ShowToast(Loc.T("Chat.NoConversation"));
            await RefreshConversationsAsync();
            conversation = _allConversations.FirstOrDefault(c => c.Type == "single" && c.Id.Contains(contact.Id));
        }
        if (conversation != null)
        {
            SelectedConversation = conversation;
        }
    }

    public void ShowToast(string message)
    {
        Toast = message;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    public async Task SignOutAsync()
    {
        Dispose();
        await AppServices.SignOutAsync();
    }

    // ---------------- 实时事件 -------------

    private void ChatOnMessageReceived(MessageDto dto)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => _ = HandleIncomingAsync(dto)));
    }

    private async Task HandleIncomingAsync(MessageDto dto)
    {
        try
        {
            if (ChatPane.Matches(dto.ConvType, dto.ConvId))
            {
                ChatPane.AppendMessage(dto);
                await AppServices.Chat.MarkReadAsync(dto.ConvType, dto.ConvId);
            }
        }
        catch (Exception ex)
        {
            ShowToast(Loc.T("Toast.MessageHandleFail", ex.Message));
        }
        _conversationTimer.Stop();
        _conversationTimer.Start();
    }

    private void ChatOnReadReceipt(string convType, string convId, string readerId)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => ChatPane.HandleReadReceipt(convType, convId)));
    }

    private void ChatOnConversationUpdated()
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _conversationTimer.Stop();
            _conversationTimer.Start();
        }));
    }

    private void ChatOnMomentChanged()
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => _ = Moments.OnExternalChangeAsync()));
    }

    private void ChatOnReconnected()
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => _ = ResyncAfterReconnectAsync()));
    }

    private async Task ResyncAfterReconnectAsync()
    {
        try
        {
            ShowToast(Loc.T("Main.Reconnected"));
            await RefreshAllAsync();
            await ChatPane.ResyncAsync();
        }
        catch (Exception ex)
        {
            ShowToast(Loc.T("Main.ResyncFail", ex.Message));
        }
    }

    private void ChatOnStatusChanged(string state)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdateConnectionState(state)));
    }

    public string SearchCountText => Loc.T("Search.Count", SearchResults.Count);

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(MeWxId));
        OnPropertyChanged(nameof(MePhone));
        OnPropertyChanged(nameof(SearchCountText));
        ConnectionText = Loc.T(ConnectionStateKey switch
        {
            "connected" => "Connection.Connected",
            "reconnecting" => "Connection.Reconnecting",
            _ => "Connection.Disconnected"
        });
        foreach (var c in _allConversations)
        {
            c.NotifyLanguageChanged();
        }
        foreach (var r in SearchResults)
        {
            r.NotifyLanguage();
        }
        ChatPane.NotifyLanguageChanged();
        Moments.NotifyLanguageChanged();
        foreach (var c in Contacts)
        {
            c.NotifyLanguageChanged();
        }
    }

    private string ConnectionStateKey { get; set; } = "disconnected";

    private void UpdateConnectionState(string state)
    {
        ConnectionStateKey = state;
        IsOffline = state != "connected";
        ConnectionText = state switch
        {
            "connected" => Loc.T("Connection.Connected"),
            "reconnecting" => Loc.T("Connection.Reconnecting"),
            _ => Loc.T("Connection.Disconnected")
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Loc.LanguageChanged -= OnLanguageChanged;
        _conversationTimer.Stop();
        _toastTimer.Stop();
        var chat = AppServices.Chat;
        chat.MessageReceived -= ChatOnMessageReceived;
        chat.ReadReceipt -= ChatOnReadReceipt;
        chat.ConversationUpdated -= ChatOnConversationUpdated;
        chat.MomentChanged -= ChatOnMomentChanged;
        chat.Reconnected -= ChatOnReconnected;
        chat.StatusChanged -= ChatOnStatusChanged;
    }
}
