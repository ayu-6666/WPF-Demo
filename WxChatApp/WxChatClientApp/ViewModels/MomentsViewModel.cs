using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WxChatApp.Helpers;
using WxChatApp.Models;
using WxChatApp.Services;

namespace WxChatApp.ViewModels;

public partial class MomentsViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private List<MomentItemVm> _all = new();

    public MomentsViewModel(ShellViewModel shell)
    {
        _shell = shell;
    }

    public ObservableCollection<MomentItemVm> Items { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _onlyMine;
    [ObservableProperty] private string _errorText = "";

    public bool IsEmpty => !IsLoading && Items.Count == 0;
    public string OwnerName => AppServices.CurrentUser?.NickName ?? "";
    public string OwnerWxId => AppServices.CurrentUser?.WxId ?? "";
    public Brush OwnerAvatar => AvatarHelper.BrushFor(AppServices.CurrentUser?.Id ?? "me");
    public string OwnerAvatarText => AvatarHelper.Initial(OwnerName);
    public ImageSource? OwnerAvatarImage => AvatarHelper.FromUrl(AppServices.CurrentUser?.Avatar);
    public bool OwnerHasAvatarImage => OwnerAvatarImage != null;
    public string OnlyMineText => OnlyMine ? Loc.T("Moments.ViewAll") : Loc.T("Moments.OnlyMine");
    public IReadOnlyList<MomentItemVm> AllItems => _all;

    /// <summary>在朋友圈界面内显示一条轻提示。</summary>
    public void Notify(string message) => _shell.ShowToast(message);

    /// <summary>本人资料（含头像）变更后刷新封面顶部信息。</summary>
    public void NotifyOwnerChanged()
    {
        OnPropertyChanged(nameof(OwnerName));
        OnPropertyChanged(nameof(OwnerWxId));
        OnPropertyChanged(nameof(OwnerAvatarText));
        OnPropertyChanged(nameof(OwnerAvatarImage));
        OnPropertyChanged(nameof(OwnerHasAvatarImage));
    }

    /// <summary>语言切换后刷新朋友圈派生文案。</summary>
    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(OnlyMineText));
        foreach (var item in _all)
        {
            item.NotifyLanguageChanged();
        }
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnOnlyMineChanged(bool value)
    {
        OnPropertyChanged(nameof(OnlyMineText));
        Rebuild();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorText = "";
        try
        {
            var dtos = await AppServices.Api.GetMomentsAsync();
            _all = dtos.Select(d => new MomentItemVm(d)).ToList();
            Rebuild();
            _shell.RefreshContactMoments();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            NotifyOwnerChanged();
        }
    }

    private void Rebuild()
    {
        var me = AppServices.CurrentUser?.Id ?? "";
        Items.Clear();
        foreach (var item in OnlyMine ? _all.Where(x => x.UserId == me) : _all)
        {
            Items.Add(item);
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task OnExternalChangeAsync()
    {
        await LoadAsync();
        if (!_shell.IsDiscoverTab)
        {
            _shell.HasNewMoments = true;
        }
    }

    [RelayCommand]
    private async Task ToggleLike(MomentItemVm? moment)
    {
        if (moment == null)
        {
            return;
        }
        var me = AppServices.CurrentUser;
        if (me == null)
        {
            return;
        }
        var before = moment.LikedByMe;
        if (before)
        {
            var mine = moment.Likes.FirstOrDefault(l => l.UserId == me.Id);
            if (mine != null)
            {
                moment.Likes.Remove(mine);
            }
        }
        else if (!moment.Likes.Any(l => l.UserId == me.Id))
        {
            moment.Likes.Add(new MomentLikeDto { UserId = me.Id, UserName = me.NickName });
        }
        moment.LikedByMe = !before;
        moment.UpdateLikeText();
        try
        {
            await AppServices.Api.ToggleLikeAsync(moment.Id);
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Moments.LikeFail", ex.Message);
            await LoadAsync();
        }
    }

    [RelayCommand]
    private void OpenComment(MomentItemVm? moment)
    {
        if (moment == null)
        {
            return;
        }
        moment.IsCommentBoxOpen = !moment.IsCommentBoxOpen;
        if (moment.IsCommentBoxOpen)
        {
            foreach (var other in Items)
            {
                if (other != moment)
                {
                    other.IsCommentBoxOpen = false;
                }
            }
        }
    }

    [RelayCommand]
    private async Task SubmitComment(MomentItemVm? moment)
    {
        if (moment == null)
        {
            return;
        }
        var text = moment.CommentDraft.Trim();
        if (text.Length == 0)
        {
            return;
        }
        try
        {
            var comment = await AppServices.Api.CommentMomentAsync(moment.Id, text, null);
            moment.Comments.Add(new MomentCommentVm(comment));
            moment.CommentDraft = "";
            moment.IsCommentBoxOpen = false;
            moment.NotifyCommentsChanged();
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Moments.CommentFail", ex.Message);
        }
    }

    [RelayCommand]
    private void ToggleOnlyMine()
    {
        OnlyMine = !OnlyMine;
    }

    public async Task PublishAsync(string content, IReadOnlyList<string> localFiles)
    {
        var urls = new List<string>();
        foreach (var file in localFiles)
        {
            urls.Add(await AppServices.Api.UploadImageAsync(file));
        }
        await AppServices.Api.PublishMomentAsync(content, urls);
        await LoadAsync();
    }

    /// <summary>删除本人动态并刷新列表，返回是否删除成功。</summary>
    public async Task<bool> DeleteAsync(MomentItemVm moment)
    {
        try
        {
            await AppServices.Api.DeleteMomentAsync(moment.Id);
            await LoadAsync();
            return true;
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Moments.DeleteFail", ex.Message);
            return false;
        }
    }
}
