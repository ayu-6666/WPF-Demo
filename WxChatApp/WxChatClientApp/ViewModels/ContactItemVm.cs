using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WxChatApp.Helpers;
using WxChatApp.Models;
using WxChatApp.Services;

namespace WxChatApp.ViewModels;

public partial class ContactItemVm : ObservableObject
{
    public ContactItemVm(ContactDto dto)
    {
        Id = dto.Id;
        Account = dto.Account;
        NickName = dto.NickName;
        Remark = dto.Remark;
        Tag = dto.Tag ?? "";
        Group = dto.Group ?? "";
        Signature = dto.Signature;
        WxId = dto.WxId;
        Region = dto.Region;
        AvatarUrl = dto.Avatar ?? "";
        CreatedAt = dto.CreatedAt;
        DisplayName = string.IsNullOrWhiteSpace(dto.Remark) ? dto.NickName : dto.Remark;
        Initial = PinyinHelper.Initial(DisplayName);
        AvatarBrush = AvatarHelper.BrushFor(dto.Id);
        AvatarText = AvatarHelper.Initial(DisplayName);
    }

    public string Id { get; }
    public string Account { get; }
    public string NickName { get; }
    public string Remark { get; }

    private string _tag = "";
    public string Tag
    {
        get => _tag;
        set
        {
            if (SetProperty(ref _tag, value)) OnPropertyChanged(nameof(HasTag));
        }
    }
    public bool HasTag => !string.IsNullOrWhiteSpace(Tag);

    public string Group { get; }
    public bool HasGroup => !string.IsNullOrWhiteSpace(Group);
    public string GroupText => Fallback(Group);

    public string Signature { get; }
    public string WxId { get; }
    public string Region { get; }
    public string AvatarUrl { get; }
    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
    public string CreatedAt { get; }

    /// <summary>资料页“微信号”栏：优先显示用户自己设置的微信号，未设置时回退到账户号。</summary>
    public string WxIdText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(WxId))
            {
                return WxId;
            }
            return string.IsNullOrWhiteSpace(Account) ? Loc.T("Common.NotSet") : Account;
        }
    }
    /// <summary>账号与微信号共用同一属性：资料页统一展示 WxId（空则回退账户号）。</summary>
    public string WxIdDisplay
    {
        get
        {
            var id = WxIdText;
            return id == Loc.T("Common.NotSet") ? id : Loc.T("Me.WxIdPrefix", id);
        }
    }
    public string RegionText => Fallback(Region);
    public string RemarkText => Fallback(Remark);
    public string NickNameText => Fallback(NickName);
    public string TagText => Fallback(Tag);
    public string SignatureText => Fallback(Signature);
    public string CreatedAtText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CreatedAt)) return Loc.T("Common.NotSet");
            if (DateTime.TryParse(CreatedAt, out var dt))
            {
                return dt.ToLocalTime().ToString(Loc.IsEnglish ? "MMM d, yyyy HH:mm" : "yyyy-MM-dd HH:mm", Loc.Culture);
            }
            return CreatedAt;
        }
    }

    private static string Fallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Loc.T("Common.NotSet") : value;

    /// <summary>语言切换后刷新派生文案。</summary>
    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(WxIdText));
        OnPropertyChanged(nameof(WxIdDisplay));
        OnPropertyChanged(nameof(RegionText));
        OnPropertyChanged(nameof(RemarkText));
        OnPropertyChanged(nameof(NickNameText));
        OnPropertyChanged(nameof(TagText));
        OnPropertyChanged(nameof(GroupText));
        OnPropertyChanged(nameof(SignatureText));
        OnPropertyChanged(nameof(CreatedAtText));
    }

    public string DisplayName { get; }
    public string Initial { get; }
    public Brush AvatarBrush { get; }
    public string AvatarText { get; }

    [ObservableProperty] private bool _isSelected;
}
