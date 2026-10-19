using System;
using System.Collections.Generic;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using RebootHMI.Core;
using RebootHMI.Models;

namespace RebootHMI.ViewModels
{
    public enum DeviceState
    {
        Idle,
        Running,
        Paused,
        EStop
    }

    public class MainViewModel : ObservableObject
    {
        private readonly Dictionary<string, object> _pageCache = new Dictionary<string, object>();
        private readonly DispatcherTimer _clock;
        private object _currentViewModel;
        private string _currentPageKey = "login";
        private bool _isLoggedIn;
        private DeviceState _runState = DeviceState.Idle;
        private bool _isPosMenuOpen;
        private bool _isIoMenuOpen;

        public string DateText { get; private set; }
        public string TimeText { get; private set; }

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            private set => SetProperty(ref _isLoggedIn, value);
        }

        public string CurrentPageKey
        {
            get => _currentPageKey;
            private set => SetProperty(ref _currentPageKey, value);
        }

        public object CurrentViewModel
        {
            get => _currentViewModel;
            private set => SetProperty(ref _currentViewModel, value);
        }

        public DeviceState RunState
        {
            get => _runState;
            private set
            {
                if (SetProperty(ref _runState, value))
                    RefreshOpButtons();
            }
        }

        public bool IsPosMenuOpen
        {
            get => _isPosMenuOpen;
            set => SetProperty(ref _isPosMenuOpen, value);
        }

        public bool IsIoMenuOpen
        {
            get => _isIoMenuOpen;
            set => SetProperty(ref _isIoMenuOpen, value);
        }

        public List<NavItem> BottomNavs { get; }
        public List<NavItem> PositionChildren { get; }
        public List<NavItem> IoChildren { get; }
        public List<OpButtonModel> OpButtons { get; }

        public RelayCommand NavigateCommand { get; }
        public RelayCommand OpenPosMenuCommand { get; }
        public RelayCommand OpenIoMenuCommand { get; }
        public RelayCommand StartCommand { get; }
        public RelayCommand PauseCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand EStopCommand { get; }
        public RelayCommand ResetCommand { get; }

        public MainViewModel()
        {
            PositionChildren = new List<NavItem>
            {
                new NavItem { Key = "pos_station", Label = "位置参数-工位" },
                new NavItem { Key = "pos_buffer", Label = "位置参数-缓存" },
                new NavItem { Key = "pos_feed", Label = "位置参数-上料道" },
                new NavItem { Key = "pos_discharge", Label = "位置参数-下料道" },
            };

            IoChildren = new List<NavItem>
            {
                new NavItem { Key = "io_gripper", Label = "IO信号-手抓-机器人" },
                new NavItem { Key = "io_feedtray", Label = "IO信号-进料道-拆机盘" },
                new NavItem { Key = "io_discharge", Label = "IO信号-下料道信号" },
                new NavItem { Key = "io_ng", Label = "IO信号-NG料道-人检区" },
            };

            BottomNavs = new List<NavItem>
            {
                new NavItem { Key = "manual", Label = "手动控制-扫码拍照", Icon = PackIconKind.GestureTap },
                new NavItem { Key = "auto", Label = "自动模式", Icon = PackIconKind.CogSync },
                new NavItem { Key = "pos_station", Label = "位置参数", Icon = PackIconKind.CheckboxMarkedOutline, HasSubMenu = true, Children = PositionChildren },
                new NavItem { Key = "roller", Label = "辊道-物料记忆", Icon = PackIconKind.ViewList },
                new NavItem { Key = "alarm", Label = "报警", Icon = PackIconKind.AlarmLight },
                new NavItem { Key = "agv", Label = "组件", Icon = PackIconKind.Forklift },
                new NavItem { Key = "io_gripper", Label = "IO信号", Icon = PackIconKind.Cog, HasSubMenu = true, Children = IoChildren },
            };

            OpButtons = new List<OpButtonModel>
            {
                new OpButtonModel { Name = "开始", Icon = PackIconKind.Play, State = "Running" },
                new OpButtonModel { Name = "暂停", Icon = PackIconKind.Pause, State = "Paused" },
                new OpButtonModel { Name = "停止", Icon = PackIconKind.Stop, State = "Idle" },
                new OpButtonModel { Name = "急停", Icon = PackIconKind.BlockHelper, State = "EStop" },
                new OpButtonModel { Name = "复位", Icon = PackIconKind.MapMarker, State = "Reset" },
            };

            NavigateCommand = new RelayCommand(o =>
            {
                if (o is NavItem n) NavigateTo(n.Key);
                else if (o is string k) NavigateTo(k);
            });
            OpenPosMenuCommand = new RelayCommand(() => { IsIoMenuOpen = false; IsPosMenuOpen = !IsPosMenuOpen; });
            OpenIoMenuCommand = new RelayCommand(() => { IsPosMenuOpen = false; IsIoMenuOpen = !IsIoMenuOpen; });
            StartCommand = new RelayCommand(() => RunState = DeviceState.Running);
            PauseCommand = new RelayCommand(() => RunState = DeviceState.Paused);
            StopCommand = new RelayCommand(() => RunState = DeviceState.Idle);
            EStopCommand = new RelayCommand(() => RunState = DeviceState.EStop);
            ResetCommand = new RelayCommand(() => RunState = DeviceState.Idle);

            CurrentViewModel = GetPage("login");
            CurrentPageKey = "login";

            UpdateClock();
            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (s, e) => UpdateClock();
            _clock.Start();
        }

        public void Login()
        {
            IsLoggedIn = true;
            NavigateTo("alarm");
        }

        private void NavigateTo(string key)
        {
            IsPosMenuOpen = false;
            IsIoMenuOpen = false;
            CurrentPageKey = key;
            CurrentViewModel = GetPage(key);
        }

        private object GetPage(string key)
        {
            if (_pageCache.TryGetValue(key, out var page))
                return page;

            switch (key)
            {
                case "login":
                    page = new LoginViewModel(this);
                    break;
                case "alarm":
                    page = new AlarmViewModel();
                    break;
                case "auto":
                    page = new AutoModeViewModel();
                    break;
                case "pos_station":
                    page = new PositionViewModel("工位一", "工位二", "工位三");
                    break;
                case "pos_buffer":
                    page = new PositionViewModel("OP50缓存台", "OP60缓存台", "OP70缓存台");
                    break;
                case "pos_feed":
                    page = new PositionViewModel("上料道一", "上料道二", "上料道三");
                    break;
                case "pos_discharge":
                    page = new PositionViewModel("下料道一", "下料道二", "下料道三");
                    break;
                case "io_gripper":
                    page = new IoGripperViewModel();
                    break;
                case "io_feedtray":
                    page = new IoFeedTrayViewModel();
                    break;
                case "io_discharge":
                    page = new IoDischargeViewModel();
                    break;
                case "io_ng":
                    page = new IoNgInspectViewModel();
                    break;
                case "manual":
                    page = new PlaceholderViewModel("手动控制-扫码拍照");
                    break;
                case "roller":
                    page = new PlaceholderViewModel("辊道-物料记忆");
                    break;
                case "agv":
                    page = new PlaceholderViewModel("组件");
                    break;
                default:
                    page = new PlaceholderViewModel(key);
                    break;
            }

            _pageCache[key] = page;
            return page;
        }

        private void RefreshOpButtons()
        {
            foreach (var op in OpButtons)
            {
                switch (op.State)
                {
                    case "Running":
                        op.IsActive = RunState == DeviceState.Running;
                        break;
                    case "Paused":
                        op.IsActive = RunState == DeviceState.Paused;
                        break;
                    case "EStop":
                        op.IsActive = RunState == DeviceState.EStop;
                        break;
                    default:
                        op.IsActive = false;
                        break;
                }
            }
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            DateText = now.ToString("yyyy/M/d");
            TimeText = now.ToString("HH:mm:ss");
            OnPropertyChanged(nameof(DateText));
            OnPropertyChanged(nameof(TimeText));
        }
    }
}
