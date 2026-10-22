using System;
using System.Collections.Generic;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Rxbit.Calibration.Core;
using Rxbit.Calibration.Models;

namespace Rxbit.Calibration.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();
        private readonly DispatcherTimer _clock;
        private string _currentPageKey = "dashboard";
        private object _currentVM;

        public List<NavItem> SideNavs { get; }
        public string LogoTitle { get; } = "XXXX";
        public string Company { get; } = "阿羽科技有限公司";
        public string DateLabel { get; private set; }
        public string TimeLabel { get; private set; }

        public string CurrentPageKey
        {
            get => _currentPageKey;
            set => SetProperty(ref _currentPageKey, value);
        }

        public object CurrentViewModel
        {
            get => _currentVM;
            private set => SetProperty(ref _currentVM, value);
        }

        public StatusBarInfo Status { get; } = new StatusBarInfo
        {
            X = "0.000 mm", Y = "0.000 mm", Z = "0.000 mm",
            A = "0.000 mm", B = "0.000 mm", C = "0.000 mm", Spindle = "0"
        };

        private bool _showAuthorizeDialog;
        public bool ShowAuthorizeDialog
        {
            get => _showAuthorizeDialog;
            set => SetProperty(ref _showAuthorizeDialog, value);
        }

        private bool _showUserInfo;
        public bool ShowUserInfo
        {
            get => _showUserInfo;
            set => SetProperty(ref _showUserInfo, value);
        }

        public string UserName { get; } = "admin";
        public string UserRole { get; } = "系统管理员";
        public string UserLevel { get; } = "Level 5";
        public string UserLoginTime { get; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        private string _requestCodeSlot0 = "0";
        public string RequestCodeSlot0 { get => _requestCodeSlot0; set => SetProperty(ref _requestCodeSlot0, value); }
        private string _requestCodeSlot1 = "1";
        public string RequestCodeSlot1 { get => _requestCodeSlot1; set => SetProperty(ref _requestCodeSlot1, value); }
        private string _requestCodeSlot2 = "";
        public string RequestCodeSlot2 { get => _requestCodeSlot2; set => SetProperty(ref _requestCodeSlot2, value); }
        private string _requestCodeSlot3 = "";
        public string RequestCodeSlot3 { get => _requestCodeSlot3; set => SetProperty(ref _requestCodeSlot3, value); }

        private string _authCodeSlot0 = "0";
        public string AuthCodeSlot0 { get => _authCodeSlot0; set => SetProperty(ref _authCodeSlot0, value); }
        private string _authCodeSlot1 = "0";
        public string AuthCodeSlot1 { get => _authCodeSlot1; set => SetProperty(ref _authCodeSlot1, value); }
        private string _authCodeSlot2 = "0";
        public string AuthCodeSlot2 { get => _authCodeSlot2; set => SetProperty(ref _authCodeSlot2, value); }
        private string _authCodeSlot3 = "0";
        public string AuthCodeSlot3 { get => _authCodeSlot3; set => SetProperty(ref _authCodeSlot3, value); }
        private string _authCodeSlot4 = "0";
        public string AuthCodeSlot4 { get => _authCodeSlot4; set => SetProperty(ref _authCodeSlot4, value); }

        public RelayCommand NavigateCommand { get; }
        public RelayCommand ShowManualParamsCommand { get; }
        public RelayCommand ShowAutoParamsCommand { get; }
        public RelayCommand CloseAuthorizeCommand { get; }
        public RelayCommand ShowUserInfoCommand { get; }
        public RelayCommand CloseUserInfoCommand { get; }
        public RelayCommand MinimizeCommand { get; }
        public RelayCommand ToggleMaximizeCommand { get; }
        public RelayCommand ExitCommand { get; }

        private bool _isMaximized;
        public bool IsMaximized
        {
            get => _isMaximized;
            set => SetProperty(ref _isMaximized, value);
        }

        public MainViewModel()
        {
            // 12 项侧边导航（从参考图可见）
            SideNavs = new List<NavItem>
            {
                new NavItem { Key = "dashboard", Label = "主页",     Icon = PackIconKind.Home },
                new NavItem { Key = "settings",  Label = "设置",     Icon = PackIconKind.Cog },
                new NavItem { Key = "user",      Label = "用户",     Icon = PackIconKind.Account },
                new NavItem { Key = "auth",      Label = "权限",     Icon = PackIconKind.Lock },
                new NavItem { Key = "comm",      Label = "通信",     Icon = PackIconKind.Antenna },
                new NavItem { Key = "output",    Label = "输出界面", Icon = PackIconKind.PowerPlug },
                new NavItem { Key = "io",        Label = "IO",       Icon = PackIconKind.Cube },
                new NavItem { Key = "input",     Label = "输入界面", Icon = PackIconKind.Pencil },
                new NavItem { Key = "manual",    Label = "手动参数", Icon = PackIconKind.Tune },
                new NavItem { Key = "auto",      Label = "自动参数", Icon = PackIconKind.Cog },
                new NavItem { Key = "alarm",     Label = "报警",     Icon = PackIconKind.Bell },
                new NavItem { Key = "log",       Label = "日志",     Icon = PackIconKind.Clock },
            };

            NavigateCommand = new RelayCommand(o =>
            {
                if (o is NavItem n) NavigateTo(n.Key);
                else if (o is string k) NavigateTo(k);
            });

            ShowManualParamsCommand = new RelayCommand(() => NavigateTo("manual"));
            ShowAutoParamsCommand = new RelayCommand(() => NavigateTo("auto"));
            CloseAuthorizeCommand = new RelayCommand(() => ShowAuthorizeDialog = false);
            ShowUserInfoCommand = new RelayCommand(() => ShowUserInfo = true);
            CloseUserInfoCommand = new RelayCommand(() => ShowUserInfo = false);
            MinimizeCommand = new RelayCommand(() =>
            {
                if (System.Windows.Application.Current.MainWindow != null)
                    System.Windows.Application.Current.MainWindow.WindowState = System.Windows.WindowState.Minimized;
            });
            ToggleMaximizeCommand = new RelayCommand(() =>
            {
                if (System.Windows.Application.Current.MainWindow is var w)
                {
                    w.WindowState = w.WindowState == System.Windows.WindowState.Maximized
                        ? System.Windows.WindowState.Normal
                        : System.Windows.WindowState.Maximized;
                    IsMaximized = w.WindowState == System.Windows.WindowState.Maximized;
                }
            });
            ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());

            CurrentViewModel = GetPage("dashboard");

            UpdateClock();
            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (s, e) => UpdateClock();
            _clock.Start();

            // 默认弹授权窗（模拟参考图 2）
            ShowAuthorizeDialog = true;
        }

        private void NavigateTo(string key)
        {
            CurrentPageKey = key;
            ShowAuthorizeDialog = false;
            CurrentViewModel = GetPage(key);
        }

        private object GetPage(string key)
        {
            if (_cache.TryGetValue(key, out var p)) return p;
            object v = key switch
            {
                "dashboard" => new DashboardViewModel(this),
                "output"    => new OutputViewModel(),
                "manual"    => new ManualParamsViewModel(),
                "auto"      => new AutoParamsViewModel(),
                _           => new DashboardViewModel(this),
            };
            _cache[key] = v;
            return v;
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            TimeLabel = now.ToString("h:mm tt");
            DateLabel = now.ToString("yyyy-MM-dd");
            OnPropertyChanged(nameof(TimeLabel));
            OnPropertyChanged(nameof(DateLabel));
        }
    }
}
