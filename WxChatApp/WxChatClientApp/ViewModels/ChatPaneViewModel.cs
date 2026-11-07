using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WxChatApp.Helpers;
using WxChatApp.Models;
using WxChatApp.Services;

namespace WxChatApp.ViewModels;

public partial class ChatPaneViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly List<string> _atIds = new();
    private DateTime? _lastTime;

    public ChatPaneViewModel(ShellViewModel shell)
    {
        _shell = shell;
        Messages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasMessages));
            OnPropertyChanged(nameof(ShowEmpty));
        };
        foreach (var emoji in EmojiSet)
        {
            Emojis.Add(emoji);
        }
        AppServices.Voice.PlayStateChanged += OnVoicePlayStateChanged;
    }

    public ObservableCollection<ChatMessageVm> Messages { get; } = new();
    public ObservableCollection<UserDto> Members { get; } = new();
    public ObservableCollection<string> Emojis { get; } = new();
    public ObservableCollection<StickerItem> Stickers { get; } = new();

    public ShellViewModel Shell => _shell;

    private static readonly string[] EmojiSet =
    {
        "😀", "😃", "😄", "😁", "😆", "😅", "😂", "🙂", "🙃", "😉",
        "😊", "😍", "🥰", "😘", "😋", "😜", "🤗", "🤔", "😐", "😴",
        "😷", "🤯", "🥳", "😎", "🥺", "😭", "😡", "👍", "👏", "🙏",
        "💪", "🎉", "❤️", "🌹", "☕", "🍀"
    };

    public event Action? BackRequested;
    public event Action? MoreRequested;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isGroup;
    [ObservableProperty] private int _memberCount;
    [ObservableProperty] private string _convType = "single";
    [ObservableProperty] private string _convId = "";
    [ObservableProperty] private string _peerId = "";
    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private bool _emojiOpen;
    [ObservableProperty] private bool _atPanelOpen;
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private string _atHint = "";
    [ObservableProperty] private int _emojiTab;
    [ObservableProperty] private bool _voiceMode;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _multiSelectMode;

    public int SelectedCount => Messages.Count(m => m.IsSelected);
    public string SelectedCountText => Loc.T("Chat.SelectedCount", SelectedCount);

    /// <summary>语言切换后刷新代码侧派生文案。</summary>
    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(VoiceHoldText));
        OnPropertyChanged(nameof(SelectedCountText));
        LoadStickers();
    }

    partial void OnMultiSelectModeChanged(bool value)
    {
        if (!value)
        {
            foreach (var m in Messages) m.IsSelected = false;
        }
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
    }

    public bool HasMembers => IsGroup && Members.Count > 0;
    public bool HasMessages => Messages.Count > 0;
    public bool ShowEmpty => IsOpen && Messages.Count == 0;
    public bool CanSend => InputText.Trim().Length > 0 && !IsSending;
    public string MemberSummary => IsGroup ? $"({MemberCount})" : "";
    public bool IsSingle => !IsGroup;
    /// <summary>单聊对方的头像（群聊为空，显示群昵称色块）。</summary>
    [ObservableProperty] private ImageSource? _peerAvatarImage;
    public bool HasPeerAvatar => PeerAvatarImage != null;
    partial void OnPeerAvatarImageChanged(ImageSource? value) => OnPropertyChanged(nameof(HasPeerAvatar));
    public bool IsStickerTab => EmojiTab == 1;
    public double EmojiPanelHeight => EmojiTab == 1 ? 210d : 130d;
    public string VoiceHoldText => IsRecording ? Loc.T("Chat.ReleaseToSend") : Loc.T("Chat.VoiceHold");

    partial void OnEmojiTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsStickerTab));
        OnPropertyChanged(nameof(EmojiPanelHeight));
    }

    partial void OnIsRecordingChanged(bool value) => OnPropertyChanged(nameof(VoiceHoldText));

    partial void OnInputTextChanged(string value) => OnPropertyChanged(nameof(CanSend));

    partial void OnIsSendingChanged(bool value) => OnPropertyChanged(nameof(CanSend));

    partial void OnIsOpenChanged(bool value) => OnPropertyChanged(nameof(ShowEmpty));

    partial void OnIsGroupChanged(bool value)
    {
        OnPropertyChanged(nameof(HasMembers));
        OnPropertyChanged(nameof(MemberSummary));
        OnPropertyChanged(nameof(IsSingle));
    }

    partial void OnMemberCountChanged(int value) => OnPropertyChanged(nameof(MemberSummary));

    public bool Matches(string convType, string convId) =>
        IsOpen && ConvType == convType && ConvId == convId;

    public async Task OpenAsync(string convType, string convId, string title, bool isGroup, int memberCount,
        string? peerId = null, string? peerAvatar = null)
    {
        ConvType = convType;
        ConvId = convId;
        Title = title;
        IsGroup = isGroup;
        MemberCount = memberCount;
        PeerAvatarImage = isGroup ? null : AvatarHelper.FromUrl(peerAvatar ?? "");
        PeerId = peerId ?? DerivePeerId(convId);
        ErrorText = "";
        AtHint = "";
        EmojiOpen = false;
        AtPanelOpen = false;
        VoiceMode = false;
        EmojiTab = 0;
        InputText = "";
        Members.Clear();
        Messages.Clear();
        _lastTime = null;
        IsOpen = true;
        OnPropertyChanged(nameof(HasMembers));
        OnPropertyChanged(nameof(ShowEmpty));
        _atIds.Clear();
        LoadStickers();

        if (isGroup)
        {
            try
            {
                var group = await AppServices.Api.GetGroupAsync(convId);
                Title = group.Name;
                MemberCount = group.MemberCount;
                foreach (var member in group.Members)
                {
                    Members.Add(member);
                }
                OnPropertyChanged(nameof(HasMembers));
            }
            catch (Exception ex)
            {
                ErrorText = ex.Message;
            }
        }

        await LoadInitialMessagesAsync();
        await AppServices.Chat.MarkReadAsync(ConvType, ConvId);
    }

    private string DerivePeerId(string convId)
    {
        var me = AppServices.CurrentUser?.Id ?? "";
        var parts = convId.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return parts.FirstOrDefault(p => p != me) ?? "";
    }

    private async Task LoadInitialMessagesAsync()
    {
        try
        {
            var clearBefore = LocalStore.GetClearedBefore(ConvId)?.ToLocalTime();
            var messages = await AppServices.Api.GetMessagesAsync(ConvType, ConvId, 100);
            foreach (var dto in messages)
            {
                if (clearBefore != null && TimeFormatter.Parse(dto.CreatedAt) <= clearBefore)
                {
                    continue;
                }
                AppendMessage(dto);
            }
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }

    /// <summary>断线重连后按最后一条消息时间补齐遗漏消息。</summary>
    public async Task ResyncAsync()
    {
        if (!IsOpen)
        {
            return;
        }
        var last = Messages.LastOrDefault();
        if (last == null)
        {
            await LoadInitialMessagesAsync();
            return;
        }
        try
        {
            var newer = await AppServices.Api.GetMessagesAsync(ConvType, ConvId, 200, last.CreatedAt);
            foreach (var dto in newer)
            {
                AppendMessage(dto);
            }
            await AppServices.Chat.MarkReadAsync(ConvType, ConvId);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }

    public void AppendMessage(MessageDto dto)
    {
        if (Messages.Any(m => m.Id == dto.Id))
        {
            return;
        }
        var time = TimeFormatter.Parse(dto.CreatedAt);
        var showTime = _lastTime == null || (time - _lastTime.Value).TotalMinutes >= 5;
        var vm = new ChatMessageVm(dto, IsGroup);
        vm.ShowTime = showTime;
        Messages.Add(vm);
        _lastTime = time;
    }

    public void HandleReadReceipt(string convType, string convId)
    {
        if (!Matches(convType, convId))
        {
            return;
        }
        foreach (var message in Messages.Where(m => m.IsMine))
        {
            message.IsRead = true;
        }
    }

    public void OnMomentOrGroupChanged()
    {
        // 群资料变化时刷新成员条
    }

    public async Task ReloadAsync()
    {
        Messages.Clear();
        _lastTime = null;
        await LoadInitialMessagesAsync();
    }

    public async Task ReloadMembersAsync()
    {
        if (!IsGroup)
        {
            return;
        }
        try
        {
            var group = await AppServices.Api.GetGroupAsync(ConvId);
            Title = group.Name;
            MemberCount = group.MemberCount;
            Members.Clear();
            foreach (var member in group.Members)
            {
                Members.Add(member);
            }
            OnPropertyChanged(nameof(HasMembers));
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }

    [RelayCommand]
    private void Back()
    {
        BackRequested?.Invoke();
    }

    [RelayCommand]
    private void More()
    {
        MoreRequested?.Invoke();
    }

    [RelayCommand]
    private void ToggleEmoji()
    {
        EmojiOpen = !EmojiOpen;
        if (EmojiOpen)
        {
            AtPanelOpen = false;
        }
    }

    [RelayCommand]
    private void ToggleAt()
    {
        AtPanelOpen = !AtPanelOpen;
        if (AtPanelOpen)
        {
            EmojiOpen = false;
        }
    }

    [RelayCommand]
    private void InsertEmoji(string emoji)
    {
        InputText += emoji;
    }

    [RelayCommand]
    private void InsertMention(string? nickname)
    {
        if (nickname == null)
        {
            return;
        }
        InputText += "@" + nickname + " ";
        var member = Members.FirstOrDefault(m => m.NickName == nickname);
        if (member != null && !_atIds.Contains(member.Id))
        {
            _atIds.Add(member.Id);
        }
        AtPanelOpen = false;
        AtHint = Loc.T("Chat.Mentioned", nickname);
    }

    [RelayCommand]
    private async Task Send()
    {
        var text = InputText.Trim();
        if (text.Length == 0 || IsSending)
        {
            return;
        }
        IsSending = true;
        ErrorText = "";
        InputText = "";
        EmojiOpen = false;
        AtPanelOpen = false;
        try
        {
            var target = IsGroup ? ConvId : PeerId;
            await AppServices.Chat.SendMessageAsync(ConvType, target, "text", text, null, _atIds.ToArray());
            _atIds.Clear();
            AtHint = "";
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Chat.SendFail", ex.Message);
            InputText = text;
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private async Task AttachImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("Common.ChooseImage"),
            Filter = Loc.T("Chat.ImageFilter")
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        try
        {
            IsSending = true;
            // 先在设置的“图片/文件存放路径”下落一份，再上传
            var localCopy = LocalStore.StageOutgoingFile(dialog.FileName);
            var url = await AppServices.Api.UploadImageAsync(localCopy);
            var target = IsGroup ? ConvId : PeerId;
            await AppServices.Chat.SendMessageAsync(ConvType, target, "image", "", url,
                Array.Empty<string>());
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Chat.ImageSendFail", ex.Message);
        }
        finally
        {
            IsSending = false;
        }
    }

    // ==================== 文件发送 ====================

    [RelayCommand]
    private async Task SendFile()
    {
        var dialog = new OpenFileDialog { Title = Loc.T("Chat.SendFileTitle") };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        var path = dialog.FileName;
        try
        {
            IsSending = true;
            var localCopy = LocalStore.StageOutgoingFile(path);
            var url = await AppServices.Api.UploadFileAsync(localCopy);
            var size = new FileInfo(path).Length;
            var target = IsGroup ? ConvId : PeerId;
            await AppServices.Chat.SendMessageAsync(ConvType, target, "file",
                Path.GetFileName(path) + "|" + size, url, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Chat.FileSendFail", ex.Message);
        }
        finally
        {
            IsSending = false;
        }
    }

    // ==================== 表情包 ====================

    public void LoadStickers()
    {
        Stickers.Clear();
        foreach (var item in StickerStore.LoadAll())
        {
            Stickers.Add(item);
        }
    }

    [RelayCommand]
    private void SelectEmojiTab(string tab) => EmojiTab = tab == "sticker" ? 1 : 0;

    /// <summary>点击表情包直接作为图片消息发送。</summary>
    [RelayCommand]
    private async Task SendSticker(string? path)
    {
        if (path == null || IsSending)
        {
            return;
        }
        try
        {
            IsSending = true;
            EmojiOpen = false;
            var url = await AppServices.Api.UploadFileAsync(path);
            var target = IsGroup ? ConvId : PeerId;
            await AppServices.Chat.SendMessageAsync(ConvType, target, "image", "", url,
                Array.Empty<string>());
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Chat.StickerSendFail", ex.Message);
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private void AddSticker()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("Chat.AddStickerTitle"),
            Filter = Loc.T("Chat.StickerFilter")
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        try
        {
            StickerStore.AddFromFile(dialog.FileName);
            LoadStickers();
            _shell.ShowToast(Loc.T("Chat.StickerAdded"));
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Chat.StickerAddFail", ex.Message);
        }
    }

    [RelayCommand]
    private void RemoveSticker(string? path)
    {
        if (path == null)
        {
            return;
        }
        var item = Stickers.FirstOrDefault(s => s.Path == path);
        if (item == null)
        {
            return;
        }
        if (item.IsBuiltin)
        {
            _shell.ShowToast(Loc.T("Chat.StickerBuiltinCannotDelete"));
            return;
        }
        StickerStore.Delete(path);
        LoadStickers();
        _shell.ShowToast(Loc.T("Chat.StickerDeleted"));
    }

    // ==================== 语音 ====================

    [RelayCommand]
    private void ToggleVoiceMode()
    {
        VoiceMode = !VoiceMode;
        if (VoiceMode)
        {
            EmojiOpen = false;
            AtPanelOpen = false;
        }
    }

    /// <summary>开始录音（由"按住说话"按钮的按下事件调用）。</summary>
    public void StartVoiceRecording()
    {
        if (IsSending || IsRecording)
        {
            return;
        }
        try
        {
            AppServices.Voice.StartRecording();
            IsRecording = true;
        }
        catch (Exception ex)
        {
            IsRecording = false;
            ErrorText = Loc.T("Chat.RecordingFail", ex.Message);
        }
    }

    /// <summary>松开按钮：停止录音并作为语音消息发送。</summary>
    public async Task FinishVoiceRecordingAsync()
    {
        if (!IsRecording)
        {
            return;
        }
        IsRecording = false;
        var result = AppServices.Voice.StopRecording();
        if (result == null)
        {
            return;
        }
        if (result.Value.ElapsedSeconds < 1)
        {
            _shell.ShowToast(Loc.T("Chat.VoiceTooShort"));
            return;
        }
        try
        {
            IsSending = true;
            var url = await AppServices.Api.UploadFileAsync(result.Value.File);
            var target = IsGroup ? ConvId : PeerId;
            await AppServices.Chat.SendMessageAsync(ConvType, target, "voice",
                result.Value.Seconds.ToString(), url, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Chat.VoiceSendFail", ex.Message);
        }
        finally
        {
            IsSending = false;
        }
    }

    /// <summary>移出按钮取消录音。</summary>
    public void CancelVoiceRecording()
    {
        if (!IsRecording)
        {
            return;
        }
        IsRecording = false;
        AppServices.Voice.CancelRecording();
    }

    /// <summary>点击语音气泡：下载缓存后播放，再点停止。</summary>
    [RelayCommand]
    private async Task PlayVoice(ChatMessageVm? vm)
    {
        if (vm?.ImageUrl == null)
        {
            return;
        }
        if (AppServices.Voice.PlayingId == vm.Id)
        {
            AppServices.Voice.StopPlayback();
            return;
        }
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "WxChatVoice");
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(new Uri(vm.ImageUrl).AbsolutePath);
            if (string.IsNullOrEmpty(ext))
            {
                ext = ".wav";
            }
            var cache = Path.Combine(dir, "msg_" + vm.Id + ext);
            if (!File.Exists(cache))
            {
                await AppServices.Api.DownloadFileAsync(vm.ImageUrl, cache);
            }
            AppServices.Voice.Play(vm.Id, cache);
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Chat.VoicePlayFail", ex.Message);
        }
    }

    private void OnVoicePlayStateChanged(string? playingId)
    {
        // 播放回调来自后台线程，切回 UI 线程更新气泡状态
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            foreach (var message in Messages)
            {
                message.IsPlaying = playingId != null && message.Id == playingId;
            }
        });
    }

    /// <summary>收藏单条消息。</summary>
    [RelayCommand]
    private async Task FavoriteMessageAsync(ChatMessageVm? vm)
    {
        if (vm == null) return;
        var fav = await AppServices.Api.AddFavoriteAsync(vm.Id);
        _shell.ShowToast(fav != null ? Loc.T("Chat.Favorited") : Loc.T("Chat.AlreadyFavorited"));
    }

    /// <summary>删除自己发出的消息（服务端校验 sender_id == me）。</summary>
    [RelayCommand]
    private async Task DeleteMessageAsync(ChatMessageVm? vm)
    {
        if (vm == null) return;
        if (!vm.IsMine)
        {
            _shell.ShowToast(Loc.T("Chat.DeleteOnlyMine"));
            return;
        }
        var ok = await AppServices.Api.DeleteMessageAsync(vm.Id);
        if (ok)
        {
            Messages.Remove(vm);
            _shell.ShowToast(Loc.T("Chat.Deleted"));
        }
        else
        {
            _shell.ShowToast(Loc.T("Chat.DeleteFail"));
        }
    }

    [RelayCommand]
    private void ToggleMultiSelect() => MultiSelectMode = !MultiSelectMode;

    [RelayCommand]
    private void CancelMultiSelect() => MultiSelectMode = false;

    /// <summary>把某条消息原样转发到目标会话（复用原 type/content/imageUrl；Hub 自动算 convId）。</summary>
    public async Task SendForwardAsync(ChatMessageVm msg, string targetId)
    {
        try
        {
            IsSending = true;
            await AppServices.Chat.SendMessageAsync("single", targetId, msg.MsgType, msg.Content, msg.ImageUrl);
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Chat.ForwardFail", ex.Message);
        }
        finally
        {
            IsSending = false;
        }
    }

    /// <summary>把多选的消息合并成一条「聊天记录」消息转发给目标好友。</summary>
    public async Task<bool> MergeForwardSelectedAsync(string targetId)
    {
        var selected = Messages.Where(m => m.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _shell.ShowToast(Loc.T("Chat.SelectFirstToMerge"));
            return false;
        }
        var entries = selected.Select(m => new MergeEntry
        {
            SenderName = m.IsMine ? (AppServices.CurrentUser?.NickName ?? Loc.T("Common.Me")) : m.SenderName,
            Type = m.MsgType,
            Content = m.MsgType switch
            {
                "image" => Loc.T("Chat.ImageTag"),
                "voice" => Loc.T("Chat.VoiceTag", m.VoiceDurationText),
                "file" => Loc.T("Chat.FileTag", m.FileName),
                "merge" => Loc.T("Chat.MergeTag"),
                _ => m.Content
            }
        }).ToList();
        var json = System.Text.Json.JsonSerializer.Serialize(entries);
        try
        {
            IsSending = true;
            await AppServices.Chat.SendMessageAsync("single", targetId, "merge", json, null);
            MultiSelectMode = false;
            _shell.ShowToast(Loc.T("Chat.MergedCount", entries.Count));
            return true;
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("Chat.MergeForwardFail", ex.Message);
            return false;
        }
        finally
        {
            IsSending = false;
        }
    }

    /// <summary>批量删除多选的消息（仅自己发出的消息允许删）。</summary>
    [RelayCommand]
    private async Task BatchDeleteSelectedAsync()
    {
        var selected = Messages.Where(m => m.IsSelected && m.IsMine).ToList();
        if (selected.Count == 0)
        {
            _shell.ShowToast(Loc.T("Chat.BatchSelectMine"));
            return;
        }
        var remaining = Messages.Count(m => m.IsSelected && !m.IsMine);
        if (remaining > 0)
        {
            _shell.ShowToast(Loc.T("Chat.BatchSkipOthers", remaining));
        }
        var okCount = 0;
        foreach (var m in selected)
        {
            var ok = await AppServices.Api.DeleteMessageAsync(m.Id);
            if (ok)
            {
                Messages.Remove(m);
                okCount++;
            }
        }
        MultiSelectMode = false;
        _shell.ShowToast(Loc.T("Chat.BatchDeleted", okCount));
    }
}
