namespace WxChatServer.Models;

/// <summary>数据库中的用户记录（含密码哈希，仅服务端可见）。</summary>
public sealed class UserRecord
{
    public string Id { get; set; } = "";
    public string Account { get; set; } = "";
    public string NickName { get; set; } = "";
    public string Signature { get; set; } = "";
    public string WxId { get; set; } = "";
    public string Region { get; set; } = "";
    public string Avatar { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

/// <summary>对外返回的用户信息（不含密码）。</summary>
public sealed class UserDto
{
    public string Id { get; set; } = "";
    public string Account { get; set; } = "";
    public string NickName { get; set; } = "";
    public string Signature { get; set; } = "";
    public string WxId { get; set; } = "";
    public string Region { get; set; } = "";
    public string Avatar { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool IsFriend { get; set; }
    public string Remark { get; set; } = "";
}

public sealed class AuthResult
{
    public string Token { get; set; } = "";
    public UserDto User { get; set; } = new();
}

public sealed class ContactDto
{
    public string Id { get; set; } = "";
    public string Account { get; set; } = "";
    public string NickName { get; set; } = "";
    public string Remark { get; set; } = "";
    public string Signature { get; set; } = "";
    public string WxId { get; set; } = "";
    public string Region { get; set; } = "";
    public string Avatar { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Group { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

public sealed class ConversationDto
{
    public string Type { get; set; } = "single";
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Avatar { get; set; } = "";
    public string LastMessage { get; set; } = "";
    public string LastMessageTime { get; set; } = "";
    public string LastSenderName { get; set; } = "";
    public int Unread { get; set; }
    public bool IsGroup { get; set; }
    public int MemberCount { get; set; }
}

public sealed class MessageDto
{
    public string Id { get; set; } = "";
    public string ConvType { get; set; } = "single";
    public string ConvId { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string SenderAvatar { get; set; } = "";
    public string Type { get; set; } = "text";
    public string Content { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string CreatedAt { get; set; } = "";
    public bool IsMine { get; set; }
    public bool IsRead { get; set; }
    public List<string> AtUserIds { get; set; } = new();
}

public sealed class GroupDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public List<UserDto> Members { get; set; } = new();
    public int MemberCount { get; set; }
}

public sealed class MomentLikeDto
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
}

public sealed class MomentCommentDto
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ReplyToName { get; set; }
    public string CreatedAt { get; set; } = "";
}

public sealed class MomentDto
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string UserAvatar { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string> Images { get; set; } = new();
    public string CreatedAt { get; set; } = "";
    public List<MomentLikeDto> Likes { get; set; } = new();
    public List<MomentCommentDto> Comments { get; set; } = new();
    public bool LikedByMe { get; set; }
    public bool IsMine { get; set; }
}

public sealed class MessageRecord
{
    public string Id { get; set; } = "";
    public string ConvType { get; set; } = "";
    public string ConvId { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string CreatedAt { get; set; } = "";
}
