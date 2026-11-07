using WxChatServer.Models;

namespace WxChatServer.Services;

/// <summary>内存中的在线状态登记（连接级），用于通讯录在线点与调试。</summary>
public static class Presence
{
    private static readonly Dictionary<string, HashSet<string>> Map = new();
    private static readonly object Gate = new();

    public static bool Add(string userId, string connectionId)
    {
        lock (Gate)
        {
            if (!Map.TryGetValue(userId, out var set))
            {
                set = new HashSet<string>();
                Map[userId] = set;
            }
            set.Add(connectionId);
            return set.Count == 1;
        }
    }

    public static bool Remove(string userId, string connectionId)
    {
        lock (Gate)
        {
            if (!Map.TryGetValue(userId, out var set))
            {
                return false;
            }
            set.Remove(connectionId);
            if (set.Count == 0)
            {
                Map.Remove(userId);
                return true;
            }
            return false;
        }
    }

    public static bool IsOnline(string userId)
    {
        lock (Gate)
        {
            return Map.ContainsKey(userId);
        }
    }
}

public static class DtoFactory
{
    public static UserDto User(UserRecord u, bool isFriend = false) => new()
    {
        Id = u.Id,
        Account = u.Account,
        NickName = u.NickName,
        Signature = u.Signature,
        WxId = u.WxId,
        Region = u.Region,
        Avatar = u.Avatar ?? "",
        Phone = u.Phone ?? "",
        IsFriend = isFriend
    };

    public static MessageDto Message(MessageRecord m, string viewerId, string senderName, string senderAvatar, bool isRead) => new()
    {
        Id = m.Id,
        ConvType = m.ConvType,
        ConvId = m.ConvId,
        SenderId = m.SenderId,
        SenderName = senderName,
        SenderAvatar = senderAvatar,
        Type = m.Type,
        Content = m.Content,
        ImageUrl = m.ImageUrl,
        CreatedAt = m.CreatedAt,
        IsMine = m.SenderId == viewerId,
        IsRead = isRead
    };

    public static GroupDto Group(string id, string name, string ownerId, IEnumerable<UserRecord> members)
    {
        var list = members.Select(x => User(x)).ToList();
        return new GroupDto { Id = id, Name = name, OwnerId = ownerId, Members = list, MemberCount = list.Count };
    }
}
