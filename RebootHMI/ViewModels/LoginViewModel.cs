using System.Windows;
using RebootHMI.Core;

namespace RebootHMI.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private readonly MainViewModel _main;
        private string _phone = string.Empty;
        private string _code = string.Empty;
        private bool _autoLogin;
        private bool _isPhoneLogin = true;
        private int _countdown;

        public string Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string Code
        {
            get => _code;
            set => SetProperty(ref _code, value);
        }

        public bool AutoLogin
        {
            get => _autoLogin;
            set => SetProperty(ref _autoLogin, value);
        }

        public bool IsPhoneLogin
        {
            get => _isPhoneLogin;
            set => SetProperty(ref _isPhoneLogin, value);
        }

        public RelayCommand LoginCommand { get; }
        public RelayCommand SendCodeCommand { get; }
        public RelayCommand ForgotCommand { get; }

        public LoginViewModel(MainViewModel main)
        {
            _main = main;
            LoginCommand = new RelayCommand(Login);
            SendCodeCommand = new RelayCommand(SendCode, () => _countdown == 0);
            ForgotCommand = new RelayCommand(() =>
                MessageBox.Show("请联系管理员重置密码", "忘记密码", MessageBoxButton.OK, MessageBoxImage.Information));
        }

        private void Login()
        {
            if (string.IsNullOrWhiteSpace(Phone))
            {
                MessageBox.Show("请输入手机号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (IsPhoneLogin && string.IsNullOrWhiteSpace(Code))
            {
                MessageBox.Show("请输入验证码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _main.Login();
        }

        private void SendCode()
        {
            if (string.IsNullOrWhiteSpace(Phone))
            {
                MessageBox.Show("请输入手机号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _countdown = 60;
            MessageBox.Show("验证码已发送", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
