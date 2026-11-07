using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WxChatApp.Helpers;
using WxChatApp.Models;
using WxChatApp.Services;

namespace WxChatApp.ViewModels;

public partial class ConversationItemVm : ObservableObject
{
    public ConversationItemVm(ConversationDto dto)
    {
        Type = dto.Type;
        Id = dto.Id;
        Title = dto.Title;
        IsGroup = dto.IsGroup;
        MemberCount = dto.MemberCount;
        LastSenderName = dto.LastSenderName;
        Unread = dto.Unread;
        LastMessage = dto.IsGroup && dto.LastSenderName.Length > 0
            ? $"{dto.LastSenderName}: {dto.LastMessage}"
            : dto.LastMessage;
        TimeText = TimeFormatter.ConversationTime(dto.LastMessageTime);
        _lastIso = dto.LastMessageTime;
        AvatarBrush = AvatarHelper.BrushFor(IsGroup ? "group:" + Id : Id);
        AvatarText = AvatarHelper.Initial(Title);
        _avatarUrl = IsGroup ? "" : dto.Avatar;
        _avatarImage = AvatarHelper.FromUrl(_avatarUrl);
    }

    public string Type { get; }
    public string Id { get; }
    public string Title { get; }
    public bool IsGroup { get; }
    public int MemberCount { get; }
    public string LastSenderName { get; }
    public Brush AvatarBrush { get; }
    public string AvatarText { get; }
    public DateTime LastTime { get; set; }

    private string _avatarUrl;
    private string _lastIso = "";
    private ImageSource? _avatarImage;
    public string AvatarUrl => _avatarUrl;
    public ImageSource? AvatarImage => _avatarImage;
    public bool HasAvatarImage => _avatarImage != null;
    public bool HasAvatar => !string.IsNullOrWhiteSpace(_avatarUrl);

    [ObservableProperty] private string _lastMessage;
    [ObservableProperty] private string _timeText;
    [ObservableProperty] private int _unread;
    [ObservableProperty] private bool _isSelected;

    /// <summary>刷新时原地更新可变字段，避免整个会话列表重建造成闪烁。</summary>
    public void Update(ConversationDto dto)
    {
        LastMessage = dto.IsGroup && dto.LastSenderName.Length > 0
            ? $"{dto.LastSenderName}: {dto.LastMessage}"
            : dto.LastMessage;
        TimeText = TimeFormatter.ConversationTime(dto.LastMessageTime);
        _lastIso = dto.LastMessageTime;
        Unread = dto.Unread;
        LastTime = TimeFormatter.Parse(dto.LastMessageTime);
        var newAvatar = dto.IsGroup ? "" : dto.Avatar;
        if (newAvatar != _avatarUrl)
        {
            _avatarUrl = newAvatar;
            _avatarImage = AvatarHelper.FromUrl(newAvatar);
            OnPropertyChanged(nameof(AvatarUrl));
            OnPropertyChanged(nameof(AvatarImage));
            OnPropertyChanged(nameof(HasAvatarImage));
            OnPropertyChanged(nameof(HasAvatar));
        }
    }

    public bool HasUnread => Unread > 0;
    public string UnreadText => Unread > 99 ? "99+" : Unread.ToString(Loc.Culture);

    /// <summary>语言切换后刷新时间文案。</summary>
    public void NotifyLanguageChanged()
    {
        TimeText = TimeFormatter.ConversationTime(_lastIso);
    }

    partial void OnUnreadChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnread));
        OnPropertyChanged(nameof(UnreadText));
    }
}
