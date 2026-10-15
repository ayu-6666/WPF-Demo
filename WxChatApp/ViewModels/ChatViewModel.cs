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
    public partial class ChatViewModel : ObservableObject
    {
        private readonly IChatService _chatService;
        private readonly ITrayService _trayService;
        private readonly User _currentUser = new() { UserId = Guid.Parse("00000000-0000-0000-0000-000000000001") };

        [ObservableProperty] private User _currentChatPartner; // 当前聊天对象（好友/群）
        [ObservableProperty] private ObservableCollection<Message> _messages = new();
        [ObservableProperty] private string _inputText = string.Empty;
        [ObservableProperty] private bool _isGroupChat;
        [ObservableProperty] private ObservableCollection<User> _groupMembers = new();
        [ObservableProperty] private bool _isAtPopupOpen;

        // 发送消息命令
        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;

            var message = new Message
            {
                SenderId = _currentUser.UserId,
                ReceiverId = IsGroupChat ? CurrentChatPartner.GroupId : CurrentChatPartner.UserId,
                Type = MessageType.Text,
                Content = InputText,
                Status = MessageStatus.Sending
            };

            Messages.Add(message);
            InputText = string.Empty;

            // 调用服务发送消息
            await _chatService.SendMessageAsync(message);
            message.Status = MessageStatus.Sent;

            // 模拟对方回复（实际由WebSocket接收服务端消息触发）
            if (!IsGroupChat)
            {
                await Task.Delay(1000);
                var reply = new Message
                {
                    SenderId = CurrentChatPartner.UserId,
                    ReceiverId = _currentUser.UserId,
                    Type = MessageType.Text,
                    Content = "收到你的消息啦！",
                    IsRead = true
                };
                Messages.Add(reply);
                _trayService.ShowNotification($"新消息来自{CurrentChatPartner.NickName}", reply.Content);
                _trayService.StartFlash();
            }
        }

        // 消息撤回命令
        [RelayCommand]
        private async Task RetractMessageAsync(Message message)
        {
            // 限制2分钟内可撤回
            if ((DateTime.Now - message.SendTime).TotalMinutes > 2)
            {
                MessageBox.Show("超过2分钟无法撤回");
                return;
            }

            await _chatService.RetractMessageAsync(message.MessageId);
            message.IsRecalled = true;
            message.Content = $"{_currentUser.NickName}撤回了一条消息";
        }

        // 标记消息已读
        public void MarkMessagesAsRead()
        {
            var unreadMessages = Messages.Where(m => m.SenderId != _currentUser.UserId && !m.IsRead).ToList();
            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                _chatService.SendReadReceiptAsync(msg.MessageId); // 发送已读回执
            }
        }

        // 模拟群@功能：输入框文本变化时检测@
        partial void OnInputTextChanged(string value)
        {
            if (IsGroupChat && value.Contains('@'))
            {
                IsAtPopupOpen = true;
            }
            else
            {
                IsAtPopupOpen = false;
            }
        }

        // 选中@的成员
        [RelayCommand]
        private void SelectAtUser(User user)
        {
            InputText = InputText.Replace("@", $"@{user.NickName} ");
            IsAtPopupOpen = false;
        }
    }

}
