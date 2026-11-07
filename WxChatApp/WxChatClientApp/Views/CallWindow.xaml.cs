using System.Windows;
using System.Windows.Threading;
using WxChatApp.Helpers;
using WxChatApp.Services;

namespace WxChatApp.Views;

/// <summary>
/// 音视频通话窗口。当前版本实现完整的呼叫信令（呼叫/接听/拒接/挂断/计时），
/// 但受限于 WPF 未内置 WebRTC，暂不传输真实音视频流。
/// </summary>
public partial class CallWindow : Window
{
    private static CallWindow? _current;

    private readonly bool _isIncoming;
    private readonly DispatcherTimer _timer;
    private string _callId;
    private int _seconds;
    private bool _inCall;

    private CallWindow(string kind, string peerName, bool isIncoming, string callId)
    {
        InitializeComponent();
        _isIncoming = isIncoming;
        _callId = callId;

        PeerNameText.Text = peerName;
        AvatarText.Text = string.IsNullOrEmpty(peerName) ? "?" : peerName[..1];
        KindText.Text = kind == "video" ? Loc.T("Call.Video") : Loc.T("Call.Voice");

        if (isIncoming)
        {
            StateText.Text = Loc.T("Call.Invite", KindText.Text);
            AcceptButton.Visibility = Visibility.Visible;
            HangUpButton.Content = Loc.T("Call.Reject");
        }
        else
        {
            StateText.Text = Loc.T("Call.Calling");
            HangUpButton.Content = Loc.T("Call.Cancel");
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _seconds++;
            TimeText.Text = $"{_seconds / 60:D2}:{_seconds % 60:D2}";
        };

        AppServices.Chat.CallResponse += OnCallResponse;
        AppServices.Chat.CallEnded += OnCallEnded;
        Closed += OnClosed;
    }

    /// <summary>被叫方：收到呼叫时弹窗。</summary>
    public static void ShowIncoming(string callId, string fromId, string fromName, string kind)
    {
        if (_current != null)
        {
            // 已有通话窗口，直接自动拒接新呼叫
            _ = AppServices.Chat.RespondCallAsync(callId, false);
            return;
        }
        var win = new CallWindow(kind, fromName, true, callId);
        win.Owner = null;
        _current = win;
        win.Show();
        win.Activate();
    }

    /// <summary>主叫方：发起呼叫并弹窗。</summary>
    public static async Task StartOutgoingAsync(string kind, string peerId, string peerName)
    {
        if (_current != null)
        {
            Dialogs.Info(null, Loc.T("Call.Busy"), Loc.T("Dialog.InfoTitle"));
            return;
        }
        var win = new CallWindow(kind, peerName, false, "");
        _current = win;
        win.Show();
        try
        {
            win._callId = await AppServices.Chat.StartCallAsync("single", peerId, kind);
        }
        catch (Exception ex)
        {
            win.StateText.Text = ex.Message;
            win.HangUpButton.Content = Loc.T("Call.Close");
            win._callId = "";
        }
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_callId)) return;
        _ = AppServices.Chat.RespondCallAsync(_callId, true);
        EnterCall();
    }

    private async void HangUp_Click(object sender, RoutedEventArgs e)
    {
        if (!_inCall && _isIncoming && !string.IsNullOrEmpty(_callId))
        {
            // 拒接
            await AppServices.Chat.RespondCallAsync(_callId, false);
        }
        else if (!string.IsNullOrEmpty(_callId))
        {
            await AppServices.Chat.EndCallAsync(_callId);
            _callId = "";
        }
        Close();
    }

    private void OnCallResponse(string callId, bool accepted, string byName)
    {
        if (_inCall || callId != _callId) return;
        Dispatcher.Invoke(() =>
        {
            if (accepted)
            {
                EnterCall();
            }
            else
            {
                StateText.Text = Loc.T("Call.Declined", byName);
                HangUpButton.Content = Loc.T("Call.Close");
                _callId = "";
                CloseAfterDelay();
            }
        });
    }

    private void OnCallEnded(string callId, string userId)
    {
        if (callId != _callId) return;
        Dispatcher.Invoke(() =>
        {
            StateText.Text = _inCall ? Loc.T("Call.HungUp") : Loc.T("Call.Ended");
            TimeText.Visibility = Visibility.Collapsed;
            _timer.Stop();
            _callId = "";
            HangUpButton.Content = Loc.T("Call.Close");
            CloseAfterDelay();
        });
    }

    private void EnterCall()
    {
        Dispatcher.Invoke(() =>
        {
            _inCall = true;
            AcceptButton.Visibility = Visibility.Collapsed;
            HangUpButton.Content = Loc.T("Call.HangUp");
            StateText.Text = Loc.T("Call.InCall");
            TimeText.Text = "00:00";
            TimeText.Visibility = Visibility.Visible;
            _timer.Start();
        });
    }

    private void CloseAfterDelay()
    {
        var delay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        delay.Tick += (_, _) =>
        {
            delay.Stop();
            Close();
        };
        delay.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        AppServices.Chat.CallResponse -= OnCallResponse;
        AppServices.Chat.CallEnded -= OnCallEnded;
        if (_current == this) _current = null;
    }
}
