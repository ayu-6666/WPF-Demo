using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using Icon = System.Drawing.Icon;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ToolTipIcon = System.Windows.Forms.ToolTipIcon;

namespace WxChatApp.Services
{
    public interface ITrayService
    {
        void Initialize();
        void ShowNotification(string title, string content);
        void StartFlash();
        void StopFlash();
    }

    public class TrayService : ITrayService
    {
        private NotifyIcon _notifyIcon;
        private Timer _flashTimer;
        private bool _isFlash;

        public void Initialize()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = new Icon("Resources/wechat_icon.ico"), // 替换为你的微信图标
                Visible = true,
                Text = "微信Demo"
            };

            // 右键菜单
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("打开主窗口", null, (s, e) =>
            {
                Application.Current.MainWindow.Show();
                Application.Current.MainWindow.Activate();
            });
            _notifyIcon.ContextMenuStrip.Items.Add("退出", null, (s, e) => Application.Current.Shutdown());

            // 双击打开主窗口
            _notifyIcon.MouseDoubleClick += (s, e) =>
            {
                Application.Current.MainWindow.Show();
                Application.Current.MainWindow.Activate();
                StopFlash();
            };
        }

        public void ShowNotification(string title, string content)
        {
            _notifyIcon.ShowBalloonTip(3000, title, content, ToolTipIcon.Info);
        }

        public void StartFlash()
        {
            _flashTimer = new Timer(_ =>
            {
                _notifyIcon.Icon = _isFlash ? new Icon("Resources/wechat_icon.ico") : new Icon("Resources/wechat_icon_highlight.ico");
                _isFlash = !_isFlash;
            }, null, 0, 500);
        }

        public void StopFlash()
        {
            _flashTimer?.Dispose();
            _notifyIcon.Icon = new Icon("Resources/wechat_icon.ico");
        }
    }

}
