using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WxChatApp.Models
{
    // 消息类型枚举
    public enum MessageType { Text, Image, Voice, Video, Emoji, Recall, System }
    // 消息状态枚举
    public enum MessageStatus { Sending, Sent, Failed, Read }
    // 朋友圈可见性枚举
    public enum VisibilitySetting { Public, Private, OnlyMe, PartiallyVisible, NotVisibleToSome }

    // 消息实体
    public class Message : ObservableObject
    {
        public Guid MessageId { get; set; } = Guid.NewGuid();
        public Guid SenderId { get; set; }
        public Guid ReceiverId { get; set; } // 单聊=对方ID，群聊=群ID
        public MessageType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SendTime { get; set; } = DateTime.Now;
        public MessageStatus Status { get; set; }
        public bool IsRead { get; set; }
        public bool IsRecalled { get; set; }
        public List<Guid> AtUserIds { get; set; } = new(); // 群@用户ID列表
    }

    // 用户实体
    public class User : ObservableObject
    {
        public Guid UserId { get; set; }
        public string NickName { get; set; } = "我";
        public string Avatar { get; set; } = "pack://application:,,,/Resources/default_avatar.png";
        public string Signature { get; set; } = "这个人很懒，什么都没写";
        public Guid? GroupId { get; set; } // 群聊时为所属群ID
    }

    // 群组实体
    public class Group : ObservableObject
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = "技术交流群";
        public string Avatar { get; set; } = "pack://application:,,,/Resources/default_group_avatar.png";
        public ObservableCollection<User> Members { get; set; } = new();
        public User Owner { get; set; }
    }

    // 朋友圈动态实体
    public class MomentsPost : ObservableObject
    {
        public Guid PostId { get; set; }
        public User Publisher { get; set; } = new();
        public string Content { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();
        public VisibilitySetting Visibility { get; set; } = VisibilitySetting.Public;
        public DateTime PublishTime { get; set; } = DateTime.Now;
        public ObservableCollection<Like> Likes { get; set; } = new();
        public ObservableCollection<Comment> Comments { get; set; } = new();
    }

    // 点赞实体
    public class Like
    {
        public User User { get; set; } = new();
        public DateTime LikeTime { get; set; }
    }

    // 评论实体
    public class Comment
    {
        public Guid CommentId { get; set; }
        public User Commenter { get; set; } = new();
        public string Content { get; set; } = string.Empty;
        public DateTime CommentTime { get; set; }
        public Guid? ReplyToCommentId { get; set; } // 回复的评论ID，为空则为评论动态
        public Comment? ReplyToComment { get; set; } // 回复的评论实体
    }

}
