using System;
using System.Threading.Tasks;
using WxChatApp.Models;

namespace WxChatApp.Services
{
    /// <summary>
    /// 朋友圈业务接口（发布动态 / 点赞 / 评论）。
    /// </summary>
    public interface IMomentsService
    {
        Task PublishPostAsync(MomentsPost post);

        Task LikePostAsync(Guid postId, Guid userId);

        Task CommentPostAsync(Guid postId, Comment comment);
    }

    /// <summary>
    /// 模拟实现：仅记录到控制台，供 Demo 使用。
    /// </summary>
    public class SimulatedMomentsService : IMomentsService
    {
        public Task PublishPostAsync(MomentsPost post)
        {
            Console.WriteLine($"[Moments] 发布动态 {post.PostId}: {post.Content}");
            return Task.CompletedTask;
        }

        public Task LikePostAsync(Guid postId, Guid userId)
        {
            Console.WriteLine($"[Moments] 点赞 {postId} by {userId}");
            return Task.CompletedTask;
        }

        public Task CommentPostAsync(Guid postId, Comment comment)
        {
            Console.WriteLine($"[Moments] 评论 {postId}: {comment.Content}");
            return Task.CompletedTask;
        }
    }
}
