using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using WxChatServer.Data;
using WxChatServer.Models;
using WxChatServer.Services;

namespace WxChatServer.Hubs;

/// <summary>
/// 聊天实时通道：连接认证、单聊投递、群聊广播、已读回执、在线状态。
/// </summary>
public sealed class ChatHub : Hub
{
    private readonly Db _db;
    private readonly IHubContext<ChatHub> _hub;

    public ChatHub(Db db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private UserRecord CurrentUser()
    {
        var user = Context.Items["user"] as UserRecord;
        if (user == null)
        {
            throw new HubException("未认证的连接");
        }
        return user;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var token = http?.Request.Query["access_token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            var header = http?.Request.Headers.Authorization.ToString() ?? "";
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = header["Bearer ".Length..].Trim();
            }
        }

        var user = _db.GetUserByToken(token);
        if (user == null)
        {
            Context.Abort();
            return;
        }

        Context.Items["user"] = user;
        var first = Presence.Add(user.Id, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "user:" + user.Id);
        if (first)
        {
            await Clients.All.SendAsync("presence", user.Id, true);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var user = Context.Items["user"] as UserRecord;
        if (user != null && Presence.Remove(user.Id, Context.ConnectionId))
        {
            await Clients.All.SendAsync("presence", user.Id, false);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>发送文本/图片消息；convType=group 时 targetId 为群 Id。</summary>
    public async Task SendMessage(string convType, string targetId, string type, string content,
        string? imageUrl, string[]? atUserIds)
    {
        var me = CurrentUser();
        convType = convType == "group" ? "group" : "single";
        type = type is "image" or "voice" or "file" or "merge" ? type : "text";
        content = (content ?? "").Trim();
        if ((type == "text" || type == "merge") && content.Length == 0)
        {
            return;
        }
        if (type is "voice" or "file" && string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new HubException("消息缺少媒体附件");
        }
        // 合并转发的内容是 JSON（聊天记录条目），放宽长度上限
        if (content.Length > (type == "merge" ? 8000 : 2000))
        {
            throw new HubException("消息过长");
        }

        string convId;
        List<string> participants;
        if (convType == "single")
        {
            if (!_db.AreFriends(me.Id, targetId))
            {
                throw new HubException("对方不是你的好友");
            }
            convId = Db.DirectConversationId(me.Id, targetId);
            participants = _db.GetConversationMembers(convType, convId);
        }
        else
        {
            if (!_db.IsGroupMember(targetId, me.Id))
            {
                throw new HubException("你不在该群聊中");
            }
            convId = targetId;
            participants = _db.GetConversationMembers("group", convId);
        }

        var msg = _db.InsertMessage(convType, convId, me.Id, type, content, imageUrl, atUserIds);
        foreach (var uid in participants)
        {
            var dto = DtoFactory.Message(msg, uid, me.NickName, me.Avatar ?? "", false);
            await Clients.Group("user:" + uid).SendAsync("message", dto);
            await Clients.Group("user:" + uid).SendAsync("conversationUpdated");
        }
    }

    /// <summary>把当前会话里对方发来的消息标记为已读，并向发送方推送已读回执。</summary>
    public async Task MarkRead(string convType, string convId)
    {
        var me = CurrentUser();
        convType = convType == "group" ? "group" : "single";
        var members = _db.GetConversationMembers(convType, convId);
        if (!members.Contains(me.Id))
        {
            return;
        }

        var notify = _db.MarkRead(convType, convId, me.Id);
        foreach (var uid in notify)
        {
            await Clients.Group("user:" + uid).SendAsync("readReceipt", convType, convId, me.Id);
        }
        await Clients.Caller.SendAsync("conversationUpdated");
    }

    public async Task Typing(string convType, string convId, string title)
    {
        var me = CurrentUser();
        var members = _db.GetConversationMembers(convType, convId).Where(x => x != me.Id);
        foreach (var uid in members)
        {
            await Clients.Group("user:" + uid).SendAsync("typing", convType, convId, me.NickName);
        }
    }

    // ---------------- 音视频通话信令 ----------------
    // 说明：WPF 端未内置 WebRTC，无法收发真实音视频流。这里实现完整的呼叫信令
    // （呼叫/接听/拒接/挂断/通话计时），UI 与链路可用，媒体流为后续接入点。

    private sealed class CallSession
    {
        public string CallerId { get; init; } = "";
        public string CalleeId { get; init; } = "";
        public string Kind { get; init; } = "voice";
    }

    private static readonly ConcurrentDictionary<string, CallSession> ActiveCalls = new();

    /// <summary>发起通话，返回 callId；服务端向被叫推送 incomingCall。</summary>
    public async Task<string> StartCall(string convType, string targetId, string kind)
    {
        var me = CurrentUser();
        if (convType == "group")
        {
            throw new HubException("当前仅支持单聊通话");
        }
        kind = kind == "video" ? "video" : "voice";
        if (!_db.AreFriends(me.Id, targetId))
        {
            throw new HubException("对方不是你的好友");
        }
        var callId = Guid.NewGuid().ToString("N");
        ActiveCalls[callId] = new CallSession { CallerId = me.Id, CalleeId = targetId, Kind = kind };
        await Clients.Group("user:" + targetId)
            .SendAsync("incomingCall", callId, me.Id, me.NickName, kind);
        return callId;
    }

    /// <summary>被叫接听或拒接，结果推给主叫。</summary>
    public async Task RespondCall(string callId, bool accepted)
    {
        var me = CurrentUser();
        if (!ActiveCalls.TryGetValue(callId, out var session) || session.CalleeId != me.Id)
        {
            return;
        }
        if (!accepted)
        {
            ActiveCalls.TryRemove(callId, out _);
        }
        await Clients.Group("user:" + session.CallerId)
            .SendAsync("callResponse", callId, accepted, me.NickName);
    }

    /// <summary>任意一方挂断，通知另一方。</summary>
    public async Task EndCall(string callId)
    {
        var me = CurrentUser();
        if (!ActiveCalls.TryRemove(callId, out var session))
        {
            return;
        }
        var peer = session.CallerId == me.Id ? session.CalleeId : session.CallerId;
        if (peer != me.Id)
        {
            await Clients.Group("user:" + peer).SendAsync("callEnded", callId, me.Id);
        }
    }
}
