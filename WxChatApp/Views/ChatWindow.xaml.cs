using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WxChatApp.Models;
using WxChatApp.ViewModels;

namespace WxChatApp.Views
{
    /// <summary>
    /// ChatWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ChatWindow : Window
    {
        public ChatWindow()
        {
            InitializeComponent();
        }

        // 点击群成员，将 @昵称 插入输入框并关闭 Popup
        private void SelectAtUser_OnClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb
                && tb.DataContext is User user
                && DataContext is ChatViewModel vm)
            {
                vm.InputText += $"@{user.NickName} ";
                vm.IsAtPopupOpen = false;
            }
        }
    }
}
