using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WxChatApp.Helpers;
using WxChatApp.Models;
using WxChatApp.Services;

namespace WxChatApp.ViewModels;

public partial class ChatMessageVm : ObservableObject
{
    public ChatMessageVm(MessageDto dto, bool showSender)
    {
        Id = dto.Id;
        ConvType = dto.ConvType;
        ConvId = dto.ConvId;
        IsMine = dto.IsMine;
        MsgType = dto.Type;
        IsSystem = dto.Type == "system";
        SenderName = dto.SenderName;
        Content = dto.Content;
        ImageUrl = dto.ImageUrl;
        CreatedAt = dto.CreatedAt;
        ShowSender = showSender && !IsMine && !IsSystem;
        IsRead = dto.IsRead;
        AvatarText = AvatarHelper.Initial(IsMine ? AppServices.CurrentUser?.NickName : dto.SenderName);
        AvatarBrush = AvatarHelper.BrushFor(IsMine ? AppServices.CurrentUser?.Id ?? "me" : dto.SenderId);
        AvatarUrl = IsMine ? AppServices.CurrentUser?.Avatar ?? "" : dto.SenderAvatar;
        AvatarImage = AvatarHelper.FromUrl(AvatarUrl);
    }

    public string Id { get; }
    public string ConvType { get; }
    public string ConvId { get; }
    public bool IsMine { get; }
    public string MsgType { get; }
    public bool IsSystem { get; }
    public bool ShowSender { get; }
    public string SenderName { get; }
    public string Content { get; }
    public string? ImageUrl { get; }
    public string CreatedAt { get; }
    public string TimeText => TimeFormatter.MessageTime(CreatedAt);
    public string AvatarText { get; }
    public Brush AvatarBrush { get; }
    public string AvatarUrl { get; }
    public ImageSource? AvatarImage { get; }
    public bool HasAvatarImage => AvatarImage != null;

    public bool IsImage => MsgType == "image" && !string.IsNullOrWhiteSpace(ImageUrl);
    public bool IsVoice => MsgType == "voice";
    public bool IsFile => MsgType == "file";
    public bool IsMerge => MsgType == "merge";
    public bool HasText => MsgType == "text" && Content.Length > 0;
    public bool ShowReadState => IsMine && !IsSystem;
    public bool ShowOther => !IsMine && !IsSystem;
    public bool ShowMine => IsMine && !IsSystem;

    private List<MergeEntry>? _mergeEntries;

    /// <summary>合并转发的聊天记录条目（Content 为 JSON 数组）。</summary>
    public List<MergeEntry> MergeEntries
    {
        get
        {
            if (_mergeEntries != null) return _mergeEntries;
            try
            {
                _mergeEntries = System.Text.Json.JsonSerializer.Deserialize<List<MergeEntry>>(Content) ?? new();
            }
            catch
            {
                _mergeEntries = new List<MergeEntry>();
            }
            return _mergeEntries;
        }
    }

    public string MergeTitle => Loc.T("Chat.MergeTitle", MergeEntries.Count);
    public string MergePreviewText =>
        string.Join("\n", MergeEntries.Take(5).Select(e => Loc.T("Chat.MergeEntryFormat", e.SenderName, e.Content)));
    public bool MergeHasMore => MergeEntries.Count > 5;

    /// <summary>语言切换后刷新派生文案。</summary>
    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(MergeTitle));
        OnPropertyChanged(nameof(MergePreviewText));
        OnPropertyChanged(nameof(ReadStateText));
    }

    /// <summary>语音时长（秒）。Content 为秒数字符串。</summary>
    public int VoiceSeconds => int.TryParse(Content, out var s) && s > 0 ? s : 1;
    public string VoiceDurationText => VoiceSeconds + "″";
    /// <summary>气泡最小宽度随时长增长，模拟语音条。</summary>
    public double VoiceMinWidth => 90 + Math.Min(VoiceSeconds, 60) * 4;

    /// <summary>文件名与大小（Content 格式为 "文件名|字节数"）。</summary>
    public string FileName => IsFile ? Content.Split('|')[0] : "";
    public string FileSizeText => IsFile
        ? long.TryParse(Content.Split('|').ElementAtOrDefault(1), out var size)
            ? FormatSize(size)
            : ""
        : "";

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => (bytes / 1024.0 / 1024.0).ToString("0.#") + " MB",
        >= 1024 => (bytes / 1024.0).ToString("0.#") + " KB",
        _ => bytes + " B"
    };

    [ObservableProperty] private bool _showTime;
    [ObservableProperty] private bool _isRead;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isSelected;

    public string ReadStateText => IsRead ? Loc.T("Chat.Read") : Loc.T("Chat.Unread");

    partial void OnIsReadChanged(bool value)
    {
        OnPropertyChanged(nameof(ReadStateText));
    }
}
