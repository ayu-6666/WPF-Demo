using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WxChatApp.Helpers;
using WxChatApp.Models;
using WxChatApp.Services;

namespace WxChatApp.ViewModels;

public partial class MomentItemVm : ObservableObject
{
    public MomentItemVm(MomentDto dto)
    {
        Id = dto.Id;
        UserId = dto.UserId;
        UserName = dto.UserName;
        Content = dto.Content;
        CreatedAt = dto.CreatedAt;
        IsMine = dto.IsMine;
        LikedByMe = dto.LikedByMe;
        AvatarBrush = AvatarHelper.BrushFor(dto.UserId);
        AvatarText = AvatarHelper.Initial(dto.UserName);
        AvatarImage = AvatarHelper.FromUrl(dto.UserAvatar);
        Images = new ObservableCollection<string>(dto.Images);
        Likes = new ObservableCollection<MomentLikeDto>(dto.Likes);
        Comments = new ObservableCollection<MomentCommentVm>(
            dto.Comments.Select(c => new MomentCommentVm(c)));
        UpdateLikeText();
    }

    public string Id { get; }
    public string UserId { get; }
    public string UserName { get; }
    public string Content { get; }
    public string CreatedAt { get; }
    public string TimeText => TimeFormatter.MomentTime(CreatedAt);
    public bool IsMine { get; }
    public Brush AvatarBrush { get; }
    public string AvatarText { get; }
    /// <summary>服务端返回的最新头像（URL 带版本参数，避免命中旧缓存）。</summary>
    public ImageSource? AvatarImage { get; }
    public bool HasAvatarImage => AvatarImage != null;
    public ObservableCollection<string> Images { get; }
    public ObservableCollection<MomentLikeDto> Likes { get; }
    public ObservableCollection<MomentCommentVm> Comments { get; }

    [ObservableProperty] private bool _likedByMe;
    [ObservableProperty] private string _likeText = "";
    [ObservableProperty] private bool _isCommentBoxOpen;
    [ObservableProperty] private string _commentDraft = "";

    public bool HasText => Content.Length > 0;
    public bool HasImages => Images.Count > 0;
    public bool HasLikes => Likes.Count > 0;
    public bool HasComments => Comments.Count > 0;
    public string LikeActionText => LikedByMe ? Loc.T("Moments.Unlike") : Loc.T("Moments.Like");

    public void UpdateLikeText()
    {
        LikeText = string.Join(Loc.IsEnglish ? ", " : "，", Likes.Select(l => l.UserName));
        OnPropertyChanged(nameof(HasLikes));
        OnPropertyChanged(nameof(LikeActionText));
    }

    public void NotifyCommentsChanged()
    {
        OnPropertyChanged(nameof(HasComments));
    }

    /// <summary>语言切换后刷新时间/点赞/评论派生文案。</summary>
    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(LikeActionText));
        UpdateLikeText();
        foreach (var c in Comments)
        {
            c.NotifyLanguageChanged();
        }
    }
}

public sealed class MomentCommentVm : ObservableObject
{
    public MomentCommentVm(MomentCommentDto dto)
    {
        UserId = dto.UserId;
        UserName = dto.UserName;
        Content = dto.Content;
        ReplyToName = dto.ReplyToName;
    }

    public string UserId { get; }
    public string UserName { get; }
    public string Content { get; }
    public string? ReplyToName { get; }

    public string DisplayText => ReplyToName == null
        ? Loc.T("Moments.CommentFormat", UserName, Content)
        : Loc.T("Moments.CommentReplyFormat", UserName, ReplyToName, Content);

    public void NotifyLanguageChanged() => OnPropertyChanged(nameof(DisplayText));
}
