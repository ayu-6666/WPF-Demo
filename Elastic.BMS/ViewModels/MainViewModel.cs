using System;
using System.Collections.Generic;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Elastic.BMS.Core;
using Elastic.BMS.Models;

namespace Elastic.BMS.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly Dictionary<string, object> _pageCache = new Dictionary<string, object>();
        private readonly DispatcherTimer _clock;
        private object _currentViewModel;
        private string _currentPageKey = "dashboard";

        public string DateText { get; private set; }
        public string TimeText { get; private set; }

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

        public List<NavItem> BottomNavs { get; }

        public RelayCommand NavigateCommand { get; }
        public RelayCommand ExitCommand { get; }

        public MainViewModel()
        {
            BottomNavs = new List<NavItem>
            {
                new NavItem { Key = "dashboard", Label = "监控主页", Icon = PackIconKind.HomeVariant },
                new NavItem { Key = "realtime", Label = "实时监控", Icon = PackIconKind.ChartTimelineVariant },
                new NavItem { Key = "alarm", Label = "报警记录", Icon = PackIconKind.ClipboardTextOutline },
                new NavItem { Key = "settings", Label = "系统设置", Icon = PackIconKind.CogOutline },
            };

            NavigateCommand = new RelayCommand(o =>
            {
                if (o is NavItem n) NavigateTo(n.Key);
                else if (o is string k) NavigateTo(k);
            });
            ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());

            CurrentViewModel = GetPage("dashboard");

            UpdateClock();
            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (s, e) => UpdateClock();
            _clock.Start();
        }

        private void NavigateTo(string key)
        {
            CurrentPageKey = key;
            CurrentViewModel = GetPage(key);
        }

        private object GetPage(string key)
        {
            if (_pageCache.TryGetValue(key, out var page))
                return page;

            switch (key)
            {
                case "dashboard":
                    page = new DashboardViewModel();
                    break;
                case "realtime":
                    page = new RealtimeViewModel();
                    break;
                case "alarm":
                    page = new AlarmRecordsViewModel();
                    break;
                case "settings":
                    page = new SettingsViewModel();
                    break;
                default:
                    page = new DashboardViewModel();
                    break;
            }

            _pageCache[key] = page;
            return page;
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            DateText = now.ToString("yyyy-MM-dd HH:mm");
            TimeText = now.ToString("HH:mm:ss");
            OnPropertyChanged(nameof(DateText));
            OnPropertyChanged(nameof(TimeText));
        }
    }
}
