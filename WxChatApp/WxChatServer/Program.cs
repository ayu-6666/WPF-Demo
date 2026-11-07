using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using WxChatServer.Auth;
using WxChatServer.Data;
using WxChatServer.Hubs;
using WxChatServer.Models;
using WxChatServer.Services;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("WXCHAT_PORT") ?? "5210";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.Services.AddSingleton<Db>();
builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 20L * 1024 * 1024);

var app = builder.Build();
var db = app.Services.GetRequiredService<Db>();
db.Init();
var hub = app.Services.GetRequiredService<IHubContext<ChatHub>>();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(db.UploadDir),
    RequestPath = "/uploads"
});

// 统一会话令牌认证（/api/auth/* 与 /uploads 公开）
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (path.StartsWith("/api") && !path.StartsWith("/api/auth") && !path.StartsWith("/api/health"))
    {
        var token = ctx.Request.Headers.Authorization.ToString();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Bearer ".Length..].Trim();
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            token = ctx.Request.Query["access_token"].ToString();
        }
        var user = db.GetUserByToken(token);
        if (user == null)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "登录状态已失效，请重新登录" });
            return;
        }
        ctx.Items["user"] = user;
        ctx.Items["token"] = token;
    }
    await next();
});

app.MapGet("/", () => Results.Text("WxChatServer is running. SignalR endpoint: /hub", "text/plain"));
app.MapGet("/api/health", () => Results.Json(new { ok = true, time = DateTime.UtcNow.ToString("o") }));

// ---------------- 认证 ----------------

app.MapPost("/api/auth/register", async (RegisterBody body) =>
{
    var account = (body.Account ?? "").Trim();
    var password = body.Password ?? "";
    var nickname = (body.NickName ?? "").Trim();

    if (!Regex.IsMatch(account, "^[A-Za-z0-9_]{6,20}$"))
    {
        return Results.Json(new { error = "账号需为 6-20 位字母、数字或下划线" }, statusCode: 400);
    }
    if (password.Length < 6 || password.Length > 32)
    {
        return Results.Json(new { error = "密码长度需为 6-32 位" }, statusCode: 400);
    }
    if (nickname.Length < 1 || nickname.Length > 20)
    {
        return Results.Json(new { error = "昵称长度需为 1-20 个字符" }, statusCode: 400);
    }
    if (db.GetUserByAccount(account) != null)
    {
        return Results.Json(new { error = "该账号已被注册" }, statusCode: 409);
    }

    var user = db.CreateUser(account, nickname, AuthService.HashPassword(password));
    var token = AuthService.NewToken();
    db.CreateSession(token, user.Id);
    return Results.Json(new AuthResult { Token = token, User = DtoFactory.User(user) });
});

app.MapPost("/api/auth/login", (LoginBody body) =>
{
    var account = (body.Account ?? "").Trim();
    var user = db.GetUserByAccount(account);
    if (user == null || !AuthService.VerifyPassword(body.Password ?? "", user.PasswordHash))
    {
        return Results.Json(new { error = "账号或密码错误" }, statusCode: 401);
    }
    var token = AuthService.NewToken();
    db.CreateSession(token, user.Id);
    return Results.Json(new AuthResult { Token = token, User = DtoFactory.User(user) });
});

app.MapPost("/api/auth/logout", (HttpContext ctx) =>
{
    var token = ctx.Items["token"] as string;
    if (token != null)
    {
        db.DeleteSession(token);
    }
    return Results.Json(new { ok = true });
});

// ---------------- 用户 ----------------

app.MapGet("/api/me", (HttpContext ctx) => Results.Json(DtoFactory.User(Me(ctx))));

app.MapPut("/api/me", (HttpContext ctx, ProfileBody body) =>
{
    var me = Me(ctx);
    var nickname = (body.NickName ?? "").Trim();
    var signature = (body.Signature ?? "").Trim();
    var wxid = (body.WxId ?? "").Trim();
    var phone = (body.Phone ?? "").Trim();
    if (nickname.Length < 1 || nickname.Length > 20)
    {
        return Results.Json(new { error = "昵称长度需为 1-20 个字符", code = "nickname_invalid" }, statusCode: 400);
    }
    if (signature.Length > 60)
    {
        return Results.Json(new { error = "个性签名最多 60 个字符", code = "signature_invalid" }, statusCode: 400);
    }
    // 微信号：6-20 位，字母开头，仅允许字母/数字/下划线/短横线
    if (wxid.Length < 6 || wxid.Length > 20 || !Regex.IsMatch(wxid, "^[A-Za-z][-A-Za-z0-9_]{5,19}$"))
    {
        return Results.Json(new { error = "微信号需为 6-20 位，字母开头，只能包含字母/数字/下划线/短横线", code = "wxid_invalid" }, statusCode: 400);
    }
    if (db.IsWxIdTaken(wxid, me.Id))
    {
        return Results.Json(new { error = "该微信号已被其它账号使用", code = "wxid_taken" }, statusCode: 409);
    }
    // 手机号：可留空；填写时须为 11 位中国大陆手机号
    if (phone.Length > 0 && !Regex.IsMatch(phone, "^1[3-9]\\d{9}$"))
    {
        return Results.Json(new { error = "手机号格式不正确", code = "phone_invalid" }, statusCode: 400);
    }
    string? avatar = null;
    if (body.AvatarUrl != null)
    {
        var a = body.AvatarUrl.Trim();
        if (a.Length > 0 && a.Length <= 400)
        {
            // 同路径覆盖上传时追加版本参数，客户端据此绕过旧图片缓存
            if (!a.Contains("?v=", StringComparison.Ordinal))
            {
                a += (a.Contains('?') ? "&" : "?") + "v=" + DateTime.UtcNow.Ticks;
            }
            avatar = a;
        }
    }
    db.UpdateProfile(me.Id, nickname, signature, wxid, phone, avatar);
    var updated = db.GetUserById(me.Id)!;
    return Results.Json(DtoFactory.User(updated));
});

app.MapGet("/api/users/{id}", (HttpContext ctx, string id) =>
{
    var me = Me(ctx);
    var target = db.GetUserById(id);
    if (target == null) return Results.NotFound(new { error = "用户不存在" });
    var isFriend = db.AreFriends(me.Id, target.Id);
    var dto = DtoFactory.User(target, isFriend);
    if (isFriend)
    {
        var contact = db.GetContacts(me.Id).FirstOrDefault(c => c.Id == target.Id);
        dto.Remark = contact?.Remark ?? "";
    }
    return Results.Json(dto);
});

app.MapPut("/api/friends/{friendId}/tag", (HttpContext ctx, string friendId, FriendTagBody body) =>
{
    var me = Me(ctx);
    if (!db.AreFriends(me.Id, friendId))
    {
        return Results.Json(new { error = "对方不是你的好友" }, statusCode: 400);
    }
    db.UpdateFriendTag(me.Id, friendId, (body.Tag ?? "").Trim());
    return Results.Json(new { ok = true });
});

app.MapPut("/api/friends/{friendId}/profile", (HttpContext ctx, string friendId, FriendProfileBody body) =>
{
    var me = Me(ctx);
    if (!db.AreFriends(me.Id, friendId))
    {
        return Results.Json(new { error = "对方不是你的好友" }, statusCode: 400);
    }
    var remark = (body.Remark ?? "").Trim();
    var tag = (body.Tag ?? "").Trim();
    var group = (body.Group ?? "").Trim();
    if (remark.Length > 30)
    {
        return Results.Json(new { error = "备注最多 30 个字符", code = "remark_invalid" }, statusCode: 400);
    }
    if (tag.Length > 60)
    {
        return Results.Json(new { error = "标签合计最多 60 个字符", code = "tag_invalid" }, statusCode: 400);
    }
    if (group.Length > 20)
    {
        return Results.Json(new { error = "分组名称最多 20 个字符", code = "group_invalid" }, statusCode: 400);
    }
    db.UpdateFriendProfile(me.Id, friendId, remark, tag, group);
    var updated = db.GetContacts(me.Id).FirstOrDefault(c => c.Id == friendId);
    if (updated == null) return Results.Json(new { error = "好友不存在" }, statusCode: 404);
    return Results.Json(updated);
});

app.MapGet("/api/users/search", (HttpContext ctx, string? q) =>
{
    var me = Me(ctx);
    var keyword = (q ?? "").Trim();
    if (keyword.Length == 0)
    {
        return Results.Json(Array.Empty<UserDto>());
    }
    var users = db.SearchUsers(keyword, me.Id)
        .Select(x => DtoFactory.User(x.User, x.IsFriend))
        .ToList();
    return Results.Json(users);
});

// ---------------- 好友 / 通讯录 ----------------

app.MapGet("/api/contacts", (HttpContext ctx) =>
{
    var me = Me(ctx);
    var contacts = db.GetContacts(me.Id);
    return Results.Json(contacts);
});

app.MapPost("/api/friends", async (HttpContext ctx, AddFriendBody body) =>
{
    var me = Me(ctx);
    var account = (body.Account ?? "").Trim();
    var target = db.GetUserByAccount(account);
    if (target == null)
    {
        return Results.Json(new { error = "用户不存在" }, statusCode: 404);
    }
    if (target.Id == me.Id)
    {
        return Results.Json(new { error = "不能添加自己为好友" }, statusCode: 400);
    }
    if (!db.AddFriendPair(me.Id, target.Id))
    {
        return Results.Json(new { error = "你们已经是好友了" }, statusCode: 409);
    }

    var convId = Db.DirectConversationId(me.Id, target.Id);
    if (db.GetLastMessage("single", convId) == null)
    {
        db.InsertMessage("single", convId, me.Id, "system",
            "我通过了你的朋友验证请求，现在我们可以开始聊天了", null, null);
    }
    await NotifyConversation(hub, me.Id, target.Id);
    return Results.Json(DtoFactory.User(target, true));
});

// ---------------- 会话与消息 ----------------

app.MapGet("/api/conversations", (HttpContext ctx) =>
{
    var me = Me(ctx);
    return Results.Json(db.GetConversations(me.Id));
});

app.MapGet("/api/messages", (HttpContext ctx, string? type, string? id, int? limit, string? after) =>
{
    var me = Me(ctx);
    var convType = type == "group" ? "group" : "single";
    var convId = id ?? "";
    if (convId.Length == 0 || !db.GetConversationMembers(convType, convId).Contains(me.Id))
    {
        return Results.Json(new { error = "无权访问该会话" }, statusCode: 403);
    }
    var msgs = db.GetMessages(convType, convId, Math.Clamp(limit ?? 100, 1, 300), after);
    var readIds = db.GetReadMessageIds(msgs.Where(m => m.SenderId == me.Id).Select(m => m.Id));
    var dto = msgs.Select(m =>
        {
            var sender = db.GetUserById(m.SenderId);
            return DtoFactory.Message(m, me.Id, sender?.NickName ?? "", sender?.Avatar ?? "", readIds.Contains(m.Id));
        })
        .ToList();
    return Results.Json(dto);
});

// ---------------- 群组 ----------------

app.MapPost("/api/groups", async (HttpContext ctx, CreateGroupBody body) =>
{
    var me = Me(ctx);
    var name = (body.Name ?? "").Trim();
    var ids = (body.MemberIds ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
    if (name.Length < 1 || name.Length > 30)
    {
        return Results.Json(new { error = "群名称长度需为 1-30 个字符" }, statusCode: 400);
    }
    if (ids.Count < 1)
    {
        return Results.Json(new { error = "请至少选择一位群成员" }, statusCode: 400);
    }
    foreach (var id in ids)
    {
        if (db.GetUserById(id) == null || !db.AreFriends(me.Id, id))
        {
            return Results.Json(new { error = "只能邀请好友加入群聊" }, statusCode: 400);
        }
    }

    var groupId = db.CreateGroup(name, me.Id, ids);
    db.InsertMessage("group", groupId, me.Id, "system", "我创建了群聊", null, null);
    foreach (var uid in ids.Append(me.Id))
    {
        await hub.Clients.Group("user:" + uid).SendAsync("conversationUpdated");
    }
    var members = db.GetGroupMembers(groupId);
    return Results.Json(DtoFactory.Group(groupId, name, me.Id, members));
});

app.MapGet("/api/groups/{id}", (HttpContext ctx, string id) =>
{
    var me = Me(ctx);
    if (!db.IsGroupMember(id, me.Id))
    {
        return Results.Json(new { error = "你不在该群聊中" }, statusCode: 403);
    }
    var name = db.GetGroupName(id);
    if (name == null)
    {
        return Results.Json(new { error = "群聊不存在" }, statusCode: 404);
    }
    return Results.Json(DtoFactory.Group(id, name, db.GetGroupOwner(id) ?? "", db.GetGroupMembers(id)));
});

app.MapPost("/api/groups/{id}/members", async (HttpContext ctx, string id, AddFriendBody body) =>
{
    var me = Me(ctx);
    if (!db.IsGroupMember(id, me.Id))
    {
        return Results.Json(new { error = "你不在该群聊中" }, statusCode: 403);
    }
    var target = db.GetUserByAccount((body.Account ?? "").Trim());
    if (target == null)
    {
        return Results.Json(new { error = "用户不存在" }, statusCode: 404);
    }
    if (!db.AddGroupMember(id, target.Id))
    {
        return Results.Json(new { error = "该用户已在群聊中" }, statusCode: 409);
    }
    db.InsertMessage("group", id, me.Id, "system", $"{target.NickName}加入了群聊", null, null);
    await BroadcastGroup(db, hub, id);
    return Results.Json(new { ok = true });
});

app.MapDelete("/api/groups/{id}/members/{userId}", async (HttpContext ctx, string id, string userId) =>
{
    var me = Me(ctx);
    if (db.GetGroupOwner(id) != me.Id)
    {
        return Results.Json(new { error = "只有群主可以移除成员" }, statusCode: 403);
    }
    if (userId != me.Id && db.RemoveGroupMember(id, userId))
    {
        var target = db.GetUserById(userId);
        db.InsertMessage("group", id, me.Id, "system", $"{target?.NickName ?? "成员"}被移出了群聊", null, null);
        await BroadcastGroup(db, hub, id);
        await hub.Clients.Group("user:" + userId).SendAsync("conversationUpdated");
    }
    return Results.Json(new { ok = true });
});

// ---------------- 朋友圈 ----------------

app.MapGet("/api/moments", (HttpContext ctx) =>
{
    var me = Me(ctx);
    return Results.Json(db.GetMoments(me.Id));
});

app.MapPost("/api/moments", async (HttpContext ctx, PublishMomentBody body) =>
{
    var me = Me(ctx);
    var content = (body.Content ?? "").Trim();
    var images = body.Images ?? new List<string>();
    if (content.Length == 0 && images.Count == 0)
    {
        return Results.Json(new { error = "请输入内容或选择图片" }, statusCode: 400);
    }
    if (content.Length > 500)
    {
        return Results.Json(new { error = "朋友圈文字最多 500 字" }, statusCode: 400);
    }
    if (images.Count > 9)
    {
        return Results.Json(new { error = "最多上传 9 张图片" }, statusCode: 400);
    }
    var postId = Guid.NewGuid().ToString("D");
    db.PublishMoment(postId, me.Id, content, images);
    await BroadcastMoment(hub, db, postId);
    var dto = db.GetMoments(me.Id).First(p => p.Id == postId);
    return Results.Json(dto);
});

app.MapPost("/api/moments/{id}/like", async (HttpContext ctx, string id) =>
{
    var me = Me(ctx);
    if (db.GetMomentAuthor(id) == null)
    {
        return Results.Json(new { error = "动态不存在" }, statusCode: 404);
    }
    var liked = db.ToggleLike(id, me.Id);
    await BroadcastMoment(hub, db, id);
    return Results.Json(new { liked });
});

app.MapPost("/api/moments/{id}/comments", async (HttpContext ctx, string id, CommentBody body) =>
{
    var me = Me(ctx);
    if (db.GetMomentAuthor(id) == null)
    {
        return Results.Json(new { error = "动态不存在" }, statusCode: 404);
    }
    var content = (body.Content ?? "").Trim();
    if (content.Length == 0 || content.Length > 200)
    {
        return Results.Json(new { error = "评论内容需为 1-200 字" }, statusCode: 400);
    }
    var comment = db.AddComment(id, me.Id, content, body.ReplyToId);
    await BroadcastMoment(hub, db, id);
    return Results.Json(comment);
});

app.MapDelete("/api/moments/{id}", async (HttpContext ctx, string id) =>
{
    var me = Me(ctx);
    var author = db.GetMomentAuthor(id);
    if (author == null)
    {
        return Results.Json(new { error = "动态不存在" }, statusCode: 404);
    }
    if (author != me.Id)
    {
        return Results.Json(new { error = "只能删除自己发布的朋友圈" }, statusCode: 403);
    }
    var audience = db.GetMomentAudience(id);
    db.DeleteMoment(id, me.Id);
    foreach (var uid in audience)
    {
        await hub.Clients.Group("user:" + uid).SendAsync("momentChanged");
    }
    return Results.Json(new { ok = true });
});

app.MapPut("/api/moments/{id}", async (HttpContext ctx, string id, PublishMomentBody body) =>
{
    var me = Me(ctx);
    var author = db.GetMomentAuthor(id);
    if (author == null)
    {
        return Results.Json(new { error = "动态不存在" }, statusCode: 404);
    }
    if (author != me.Id)
    {
        return Results.Json(new { error = "只能修改自己发布的朋友圈" }, statusCode: 403);
    }
    var content = (body.Content ?? "").Trim();
    var images = body.Images ?? new List<string>();
    if (content.Length == 0 && images.Count == 0)
    {
        return Results.Json(new { error = "请输入内容或选择图片" }, statusCode: 400);
    }
    if (content.Length > 500)
    {
        return Results.Json(new { error = "朋友圈文字最多 500 字" }, statusCode: 400);
    }
    if (images.Count > 9)
    {
        return Results.Json(new { error = "最多上传 9 张图片" }, statusCode: 400);
    }
    db.UpdateMoment(id, me.Id, content, images);
    await BroadcastMoment(hub, db, id);
    var dto = db.GetMoments(me.Id).FirstOrDefault(p => p.Id == id);
    return dto == null ? Results.Json(new { ok = true }) : Results.Json(dto);
});

// ---------------- 媒体/文件上传（图片、语音、普通文件共用） ----------------

app.MapPost("/api/upload", async (HttpContext ctx) =>
{
    var me = Me(ctx);
    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file == null || file.Length == 0)
    {
        return Results.Json(new { error = "未收到文件" }, statusCode: 400);
    }
    if (file.Length > 20 * 1024 * 1024)
    {
        return Results.Json(new { error = "文件不能超过 20MB" }, statusCode: 400);
    }
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (new[] { ".exe", ".bat", ".cmd", ".com", ".scr", ".msi", ".js", ".vbs", ".ps1", ".sh" }.Contains(ext))
    {
        return Results.Json(new { error = "不支持发送可执行脚本类文件" }, statusCode: 400);
    }
    var name = Guid.NewGuid().ToString("N") + ext;
    var full = Path.Combine(db.UploadDir, name);
    await using (var stream = File.Create(full))
    {
        await file.CopyToAsync(stream);
    }
    var url = $"{ctx.Request.Scheme}://{ctx.Request.Host}/uploads/{name}";
    return Results.Json(new { url });
});

app.MapDelete("/api/messages/{id}", (HttpContext ctx, string id) =>
{
    var me = Me(ctx);
    return db.DeleteMessage(id, me.Id)
        ? Results.Json(new { ok = true })
        : Results.Json(new { error = "无权删除或消息不存在" }, statusCode: 403);
});

app.MapGet("/api/messages/search", (HttpContext ctx, string? q, int? limit) =>
{
    var me = Me(ctx);
    var kw = (q ?? "").Trim();
    if (kw.Length == 0) return Results.Json(Array.Empty<object>());
    var msgs = db.SearchMessages(me.Id, kw, Math.Clamp(limit ?? 50, 1, 200));
    var ids = db.GetReadMessageIds(msgs.Where(m => m.SenderId == me.Id).Select(m => m.Id));
    var dto = msgs.Select(m => new
    {
        Id = m.Id, ConvType = m.ConvType, ConvId = m.ConvId, SenderId = m.SenderId,
        SenderName = db.GetUserById(m.SenderId)?.NickName ?? "",
        Type = m.Type, Content = m.Content, ImageUrl = m.ImageUrl,
        CreatedAt = m.CreatedAt, IsMine = m.SenderId == me.Id, IsRead = ids.Contains(m.Id)
    }).ToList();
    return Results.Json(dto);
});

app.MapGet("/api/moments/search", (HttpContext ctx, string? q, int? limit) =>
{
    var me = Me(ctx);
    var kw = (q ?? "").Trim();
    if (kw.Length == 0) return Results.Json(Array.Empty<object>());
    var posts = db.SearchMoments(me.Id, kw, Math.Clamp(limit ?? 50, 1, 100));
    var dto = posts.Select(p => new
    {
        p.Id, p.UserId, p.UserName, p.Content, p.Images, p.CreatedAt, p.IsMine
    }).ToList();
    return Results.Json(dto);
});

// ---------------- 公众号（本地内置内容，不依赖微信开放平台） ----------------

app.MapGet("/api/official/accounts", (HttpContext ctx) =>
{
    Me(ctx);
    return Results.Json(db.GetOfficialAccounts());
});

app.MapGet("/api/official/messages", (HttpContext ctx, string? accountId, int? limit) =>
{
    Me(ctx);
    return Results.Json(db.GetOfficialMessages(accountId, Math.Clamp(limit ?? 50, 1, 200)));
});

// ---------------- 收藏 ----------------

app.MapGet("/api/favorites", (HttpContext ctx) =>
{
    var me = Me(ctx);
    var favs = db.GetFavorites(me.Id).Select(f => new
    {
        f.Id, f.MessageId, f.ConvType, f.ConvId, f.MsgType, f.Content,
        f.ImageUrl, f.MessageCreatedAt, f.FavoritedAt
    }).ToList();
    return Results.Json(favs);
});

app.MapPost("/api/favorites", (HttpContext ctx, FavoriteBody body) =>
{
    var me = Me(ctx);
    var msgId = (body.MessageId ?? "").Trim();
    using var conn = db.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT * FROM messages WHERE id = @id LIMIT 1";
    cmd.Parameters.AddWithValue("@id", msgId);
    using var r = cmd.ExecuteReader();
    if (!r.Read()) return Results.Json(new { error = "消息不存在" }, statusCode: 404);
    var rec = new MessageRecord
    {
        Id = (string)r["id"],
        ConvType = (string)r["conv_type"],
        ConvId = (string)r["conv_id"],
        SenderId = (string)r["sender_id"],
        Type = (string)r["type"],
        Content = (string)r["content"],
        ImageUrl = r["image_url"] is string s ? s : null,
        CreatedAt = (string)r["created_at"]
    };
    var convId = rec.ConvType == "single"
        ? Db.DirectConversationId(me.Id, rec.SenderId)
        : rec.ConvId;
    var members = db.GetConversationMembers(rec.ConvType, convId);
    if (!members.Contains(me.Id)) return Results.Json(new { error = "无权收藏该消息" }, statusCode: 403);
    var fav = db.AddFavorite(me.Id, rec);
    return fav == null
        ? Results.Json(new { error = "已收藏" }, statusCode: 409)
        : Results.Json(new { fav.Id, fav.MessageId, fav.ConvType, fav.ConvId, fav.MsgType, fav.Content, fav.ImageUrl, fav.MessageCreatedAt, fav.FavoritedAt });
});

app.MapDelete("/api/favorites/{id}", (HttpContext ctx, string id) =>
{
    var me = Me(ctx);
    return db.RemoveFavorite(me.Id, id)
        ? Results.Json(new { ok = true })
        : Results.Json(new { error = "收藏不存在" }, statusCode: 404);
});

app.MapHub<ChatHub>("/hub");

app.Logger.LogInformation("WxChatServer 数据目录: {Dir}", db.DataDir);
app.Logger.LogInformation("监听端口: {Port}", port);
app.Run();

// ---------------- 辅助 ----------------

static UserRecord Me(HttpContext ctx) => (UserRecord)ctx.Items["user"]!;

static async Task NotifyConversation(IHubContext<ChatHub> hub, string a, string b)
{
    await hub.Clients.Group("user:" + a).SendAsync("conversationUpdated");
    await hub.Clients.Group("user:" + b).SendAsync("conversationUpdated");
}

static async Task BroadcastGroup(Db db, IHubContext<ChatHub> hub, string groupId)
{
    foreach (var uid in db.GetConversationMembers("group", groupId))
    {
        await hub.Clients.Group("user:" + uid).SendAsync("conversationUpdated");
    }
}

static async Task BroadcastMoment(IHubContext<ChatHub> hub, Db db, string postId)
{
    foreach (var uid in db.GetMomentAudience(postId))
    {
        await hub.Clients.Group("user:" + uid).SendAsync("momentChanged");
    }
}

sealed class RegisterBody
{
    public string? Account { get; set; }
    public string? Password { get; set; }
    public string? NickName { get; set; }
}

sealed class LoginBody
{
    public string? Account { get; set; }
    public string? Password { get; set; }
}

sealed class ProfileBody
{
    public string? NickName { get; set; }
    public string? Signature { get; set; }
    public string? AvatarUrl { get; set; }
    public string? WxId { get; set; }
    public string? Phone { get; set; }
}

sealed class FriendTagBody
{
    public string? Tag { get; set; }
}

sealed class FriendProfileBody
{
    public string? Remark { get; set; }
    public string? Tag { get; set; }
    public string? Group { get; set; }
}

sealed class FavoriteBody
{
    public string? MessageId { get; set; }
}

sealed class AddFriendBody
{
    public string? Account { get; set; }
}

sealed class CreateGroupBody
{
    public string? Name { get; set; }
    public List<string>? MemberIds { get; set; }
}

sealed class PublishMomentBody
{
    public string? Content { get; set; }
    public List<string>? Images { get; set; }
}

sealed class CommentBody
{
    public string? Content { get; set; }
    public string? ReplyToId { get; set; }
}
