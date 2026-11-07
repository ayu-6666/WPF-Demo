namespace WxChatApp.Models;

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
    public int MemberCount { get; set; }
    public List<UserDto> Members { get; set; } = new();
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

public sealed class ErrorDto
{
    public string? Error { get; set; }
    public string? Code { get; set; }
}

public sealed class FavoriteItem
{
    public string Id { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string ConvType { get; set; } = "";
    public string ConvId { get; set; } = "";
    public string MsgType { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string MessageCreatedAt { get; set; } = "";
    public string FavoritedAt { get; set; } = "";
}

/// <summary>合并转发内容的一行（序列化后作为 merge 消息的 content）。</summary>
public sealed class MergeEntry
{
    public string SenderName { get; set; } = "";
    public string Content { get; set; } = "";
    public string Type { get; set; } = "text";
}

public sealed class OfficialAccountDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "";
    public string Description { get; set; } = "";
    public int MessageCount { get; set; }
}

public sealed class OfficialMessageDto
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}
