using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WxChatApp.Models;
using WxChatApp.Services;

namespace WxChatApp.ViewModels
{
    public partial class MomentsViewModel : ObservableObject
    {
        private readonly IMomentsService _momentsService;
        private readonly User _currentUser = new() { UserId = Guid.Parse("00000000-0000-0000-0000-000000000001") };

        [ObservableProperty] private ObservableCollection<MomentsPost> _posts = new();
        [ObservableProperty] private string _publishContent = string.Empty;
        [ObservableProperty] private List<string> _selectedImages = new();
        [ObservableProperty] private VisibilitySetting _selectedVisibility = VisibilitySetting.Public;

        public MomentsViewModel()
        {
            _momentsService = new SimulatedMomentsService();
            // 初始化模拟数据
            LoadMockData();
        }

        // 发布动态命令
        [RelayCommand]
        private async Task PublishPostAsync()
        {
            if (string.IsNullOrWhiteSpace(PublishContent) && !SelectedImages.Any()) return;

            var post = new MomentsPost
            {
                Publisher = _currentUser,
                Content = PublishContent,
                ImageUrls = SelectedImages,
                Visibility = SelectedVisibility,
                PublishTime = DateTime.Now
            };

            Posts.Insert(0, post);
            await _momentsService.PublishPostAsync(post);

            PublishContent = string.Empty;
            SelectedImages.Clear();
            MessageBox.Show("发布成功");
        }

        // 点赞命令
        [RelayCommand]
        private async Task LikePostAsync(MomentsPost post)
        {
            var like = new Like { User = _currentUser, LikeTime = DateTime.Now };
            post.Likes.Add(like);
            await _momentsService.LikePostAsync(post.PostId, _currentUser.UserId);
        }

        // 评论命令（参数多于一个，无法由 RelayCommand 源生成器生成命令属性，改为方法调用）
        private async Task CommentPostAsync(MomentsPost post, string commentContent, Comment? replyToComment = null)
        {
            var comment = new Comment
            {
                Commenter = _currentUser,
                Content = commentContent,
                CommentTime = DateTime.Now,
                ReplyToComment = replyToComment
            };
            post.Comments.Add(comment);
            await _momentsService.CommentPostAsync(post.PostId, comment);
        }

        // 加载模拟数据
        private void LoadMockData()
        {
            var mockUser1 = new User { UserId = Guid.NewGuid(), NickName = "张三", Avatar = "https://via.placeholder.com/50" };
            var mockUser2 = new User { UserId = Guid.NewGuid(), NickName = "李四", Avatar = "https://via.placeholder.com/50" };

            Posts.Add(new MomentsPost
            {
                Publisher = mockUser1,
                Content = "今天天气真好！",
                ImageUrls = new List<string> { "https://via.placeholder.com/300", "https://via.placeholder.com/300" },
                Likes = new ObservableCollection<Like> { new Like { User = mockUser2, LikeTime = DateTime.Now.AddHours(-1) } },
                Comments = new ObservableCollection<Comment>
            {
                new Comment { Commenter = mockUser2, Content = "确实不错", CommentTime = DateTime.Now.AddHours(-2) }
            }
            });
        }
    }

}
