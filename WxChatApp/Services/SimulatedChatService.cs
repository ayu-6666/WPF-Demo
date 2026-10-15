using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WxChatApp.Models;

namespace WxChatApp.Services
{
    public interface IChatService
    {
        Task SendMessageAsync(Message message);
        Task RetractMessageAsync(Guid messageId);
        Task SendReadReceiptAsync(Guid messageId);
    }

    public class SimulatedChatService : IChatService
    {
        public Task SendMessageAsync(Message message)
        {
            // 实际项目中通过WebSocket发送消息到服务端
            return Task.CompletedTask;
        }

        public Task RetractMessageAsync(Guid messageId)
        {
            // 实际项目发送撤回指令到服务端
            return Task.CompletedTask;
        }

        public Task SendReadReceiptAsync(Guid messageId)
        {
            // 实际项目发送已读回执到服务端
            return Task.CompletedTask;
        }
    }

}
