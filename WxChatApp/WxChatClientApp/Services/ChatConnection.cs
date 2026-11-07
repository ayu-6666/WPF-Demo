using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WxChatApp.Models;
using WxChatApp.Views;

namespace WxChatApp.Services;

/// <summary>
/// SignalR 实时连接：自动重连；服务端重启后自动补连并通知界面刷新补齐消息。
/// 所有事件回调统一切到 WPF Dispatcher 触发，订阅者无需再手动 Invoke。
/// </summary>
public sealed class ChatConnection
{
    private HubConnection? _conn;
    private CancellationTokenSource? _retryCts;
    private bool _disposed;

    public event Action<MessageDto>? MessageReceived;
    public event Action<string, string, string>? ReadReceipt;
    public event Action? ConversationUpdated;
    public event Action? MomentChanged;
    public event Action? Reconnected;
    public event Action<string>? StatusChanged;
    /// <summary>收到呼叫：callId, 发起人 Id, 发起人昵称, 类型（voice/video）。</summary>
    public event Action<string, string, string, string>? IncomingCall;
    /// <summary>呼叫结果：callId, 是否接听, 对方昵称。</summary>
    public event Action<string, bool, string>? CallResponse;
    /// <summary>对方挂断：callId, 对方 Id。</summary>
    public event Action<string, string>? CallEnded;

    public bool IsConnected => _conn?.State == HubConnectionState.Connected;

    // --------- Dispatcher 工具 ---------
    private static void Dispatch(Action action)
    {
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action);
            }
        }
        catch
        {
            // Dispatcher 已关闭或不可用，丢弃事件避免二次崩溃
        }
    }

    // --------- Connect ---------

    /// <summary>
    /// 建立 SignalR 连接。默认 15 秒超时；传入外部 CancellationToken 可提前取消。
    /// </summary>
    public async Task ConnectAsync(string token, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();
        _disposed = false;

        using var totalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        totalCts.CancelAfter(TimeSpan.FromSeconds(15));
        var totalToken = totalCts.Token;

        _conn = new HubConnectionBuilder()
            .WithUrl(AppConfig.HubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();

        // 所有 Hub 回调统一切 Dispatcher
        _conn.On<MessageDto>("message", m => Dispatch(() => MessageReceived?.Invoke(m)));
        _conn.On<string, string, string>("readReceipt",
            (convType, convId, readerId) => Dispatch(() => ReadReceipt?.Invoke(convType, convId, readerId)));
        _conn.On("conversationUpdated", () => Dispatch(() => ConversationUpdated?.Invoke()));
        _conn.On("momentChanged", () => Dispatch(() => MomentChanged?.Invoke()));
        _conn.On<string, bool>("presence", (_, _) => { });
        _conn.On<string, string, string, string>("incomingCall",
            (callId, fromId, fromName, kind) => Dispatch(() => IncomingCall?.Invoke(callId, fromId, fromName, kind)));
        _conn.On<string, bool, string>("callResponse",
            (callId, accepted, byName) => Dispatch(() => CallResponse?.Invoke(callId, accepted, byName)));
        _conn.On<string, string>("callEnded",
            (callId, userId) => Dispatch(() => CallEnded?.Invoke(callId, userId)));

        _conn.Reconnecting += _ =>
        {
            Dispatch(() => StatusChanged?.Invoke("reconnecting"));
            return Task.CompletedTask;
        };
        _conn.Reconnected += _ =>
        {
            CancelRetry();
            Dispatch(() =>
            {
                StatusChanged?.Invoke("connected");
                Reconnected?.Invoke();
            });
            return Task.CompletedTask;
        };
        _conn.Closed += _ =>
        {
            Dispatch(() => StatusChanged?.Invoke("disconnected"));
            StartRetryLoop();
            return Task.CompletedTask;
        };

        try
        {
            await _conn.StartAsync(totalToken);
        }
        catch (OperationCanceledException) when (totalCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // 是我们自己的 15s 超时
            throw new TimeoutException(Loc.T("Connection.TimeoutDetail"));
        }

        Dispatch(() => StatusChanged?.Invoke("connected"));
    }

    private void CancelRetry()
    {
        _retryCts?.Cancel();
        _retryCts = null;
    }

    private void StartRetryLoop()
    {
        if (_disposed || _retryCts != null)
        {
            return;
        }
        _retryCts = new CancellationTokenSource();
        var token = _retryCts.Token;
        _ = Task.Run(async () =>
        {
            while (!_disposed && !token.IsCancellationRequested && _conn != null)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                if (_conn.State == HubConnectionState.Connected)
                {
                    return;
                }
                try
                {
                    await _conn.StartAsync(token);
                    Dispatch(() =>
                    {
                        StatusChanged?.Invoke("connected");
                        Reconnected?.Invoke();
                    });
                    return;
                }
                catch
                {
                    // 服务端尚未就绪，继续等待
                }
            }
        });
    }

    // --------- Invoke ---------

    public async Task SendMessageAsync(string convType, string targetId, string type, string content,
        string? imageUrl, IReadOnlyCollection<string>? atUserIds = null)
    {
        if (_conn == null)
        {
            throw new ApiException(Loc.T("Connection.NotConnected"));
        }
        await _conn.InvokeAsync("SendMessage", convType, targetId, type, content, imageUrl,
            atUserIds?.ToArray() ?? Array.Empty<string>());
    }

    public async Task<string> StartCallAsync(string convType, string targetId, string kind)
    {
        if (_conn == null || _conn.State != HubConnectionState.Connected)
        {
            throw new ApiException(Loc.T("Connection.NotConnected"));
        }
        return await _conn.InvokeAsync<string>("StartCall", convType, targetId, kind);
    }

    public async Task RespondCallAsync(string callId, bool accepted)
    {
        if (_conn == null || _conn.State != HubConnectionState.Connected)
        {
            return;
        }
        try
        {
            await _conn.InvokeAsync("RespondCall", callId, accepted);
        }
        catch
        {
            // 呼叫已失效
        }
    }

    public async Task EndCallAsync(string callId)
    {
        if (_conn == null || _conn.State != HubConnectionState.Connected)
        {
            return;
        }
        try
        {
            await _conn.InvokeAsync("EndCall", callId);
        }
        catch
        {
            // 呼叫已失效
        }
    }

    public async Task MarkReadAsync(string convType, string convId)
    {
        if (_conn == null || _conn.State != HubConnectionState.Connected)
        {
            return;
        }
        try
        {
            await _conn.InvokeAsync("MarkRead", convType, convId);
        }
        catch
        {
            // 已读回执失败不阻断阅读
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        CancelRetry();
        if (_conn != null)
        {
            try
            {
                await _conn.StopAsync(cancellationToken);
            }
            catch
            {
                // 连接已经失效
            }
            try
            {
                await _conn.DisposeAsync();
            }
            catch
            {
                // 忽略释放异常
            }
            _conn = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await DisconnectAsync();
    }
}

public static class AppServices
{
    public static ApiClient Api { get; } = new();
    public static ChatConnection Chat { get; } = new();
    public static VoiceService Voice { get; } = new();
    public static UserDto? CurrentUser { get; set; }
    public static string? Token { get; set; }
    public static MainWindow? MainWindow { get; set; }

    public static async Task SignOutAsync()
    {
        try
        {
            await Api.LogoutAsync();
        }
        catch
        {
            // 离线退出也要清空本地会话
        }
        Voice.CancelRecording();
        Voice.StopPlayback();
        await Chat.DisconnectAsync();
        CurrentUser = null;
        Token = null;
    }
}
