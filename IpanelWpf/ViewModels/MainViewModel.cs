using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using IpanelWpf.Core;
using IpanelWpf.Models;
using MaterialDesignThemes.Wpf;

namespace IpanelWpf.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private object _currentViewModel;
        private readonly Dictionary<string, object> _pageMap;
        private readonly Dictionary<string, object> _placeholderMap;

        public ObservableCollection<NavItem> Menu { get; }

        public ICommand NavigateCommand { get; }
        public RelayCommand ShowMenuCommand { get; set; }


        public object CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public MainViewModel()
        {
            _pageMap = new Dictionary<string, object>
            {
                ["dashboard"] = new DashboardViewModel(),
                ["appstore"] = new AppStoreViewModel(),
                ["database"] = new DatabaseViewModel(),
                ["container"] = new ContainerViewModel(),
                ["firewall"] = new FirewallViewModel(),
                ["settings"] = new PanelSettingsViewModel(),
                ["files"] = new FilesViewModel(),
            };

            _placeholderMap = new Dictionary<string, object>();

            Menu = BuildMenu();
            NavigateCommand = new RelayCommand<NavItem>(Navigate);
            ShowMenuCommand = new RelayCommand(o => switchMenu());
            // 默认进入概览页
            var home = Menu.First(m => m.PageKey == "dashboard");
            home.IsSelected = true;
            CurrentViewModel = _pageMap["dashboard"];
        }

        private void switchMenu()
        {
            ShowMenu=!ShowMenu;
            foreach (var item in Menu)
            {
                item.ShowChevron = !ShowMenu;
            }
        }

        private ObservableCollection<NavItem> BuildMenu()
        {
            var system = new NavItem
            {
                Name = "系统",
                Icon = PackIconKind.Monitor,
                ShowChevron = true,
                Children = new ObservableCollection<NavItem>
                {
                    new NavItem { Name = "文件", Icon = PackIconKind.Folder, PageKey = "files" },
                    new NavItem { Name = "监控", Icon = PackIconKind.Speedometer, PageKey = "ph:monitor" },
                    new NavItem { Name = "防火墙", Icon = PackIconKind.Shield, PageKey = "firewall" },
                    new NavItem { Name = "进程管理", Icon = PackIconKind.FormatListBulleted, PageKey = "ph:process" },
                    new NavItem { Name = "SSH 管理", Icon = PackIconKind.Key, PageKey = "ph:ssh" },
                    new NavItem { Name = "磁盘管理", Icon = PackIconKind.Harddisk, PageKey = "ph:disk" },
                }
            };
            foreach (var child in system.Children)
                child.Parent = system;

            return new ObservableCollection<NavItem>
            {
                new NavItem { Name = "概览", Icon = PackIconKind.Home, PageKey = "dashboard" },
                new NavItem { Name = "应用商店", Icon = PackIconKind.Apps, PageKey = "appstore" },
                new NavItem { Name = "AI", Icon = PackIconKind.Robot, PageKey = "ph:ai", ShowChevron = false, Children = new ObservableCollection<NavItem>() },
                new NavItem { Name = "网站", Icon = PackIconKind.Web, PageKey = "ph:website", ShowChevron = false, Children = new ObservableCollection<NavItem>() },
                new NavItem { Name = "数据库", Icon = PackIconKind.Database, PageKey = "database", ShowChevron = false, Children = new ObservableCollection<NavItem>() },
                new NavItem { Name = "容器", Icon = PackIconKind.Cube, PageKey = "container", ShowChevron = false, Children = new ObservableCollection<NavItem>() },
                system,
                new NavItem { Name = "终端", Icon = PackIconKind.Console, PageKey = "ph:terminal" },
                new NavItem { Name = "计划任务", Icon = PackIconKind.Clock, PageKey = "ph:cron" },
                new NavItem { Name = "工具箱", Icon = PackIconKind.Toolbox, PageKey = "ph:toolbox" },
                new NavItem { Name = "高级功能", Icon = PackIconKind.Diamond, PageKey = "ph:advanced", ShowChevron = true, Children = new ObservableCollection<NavItem>() },
                new NavItem { Name = "日志审计", Icon = PackIconKind.FileDocument, PageKey = "ph:audit", ShowChevron = true, Children = new ObservableCollection<NavItem>() },
                new NavItem { Name = "面板设置", Icon = PackIconKind.Cog, PageKey = "settings" },
            };
        }

        private void Navigate(NavItem item)
        {
            if (item == null)
                return;

            if (string.IsNullOrEmpty(item.PageKey) || item.PageKey.StartsWith("ph:"))
            {
                if (item.HasChildren)
                {
                    item.IsExpanded = !item.IsExpanded;
                    return;
                }

                // 占位页
                var key = item.PageKey ?? item.Name;
                if (!_placeholderMap.TryGetValue(key, out var vm))
                {
                    vm = new PlaceholderViewModel(item.Name);
                    _placeholderMap[key] = vm;
                }

                ClearSelection(Menu);
                item.IsSelected = true;
                if (item.Parent != null)
                    item.Parent.IsExpanded = true;
                CurrentViewModel = vm;
                return;
            }

            if (item.HasChildren)
                item.IsExpanded = true;

            ClearSelection(Menu);
            item.IsSelected = true;
            if (item.Parent != null)
                item.Parent.IsExpanded = true;

            CurrentViewModel = _pageMap[item.PageKey];
        }

        private static void ClearSelection(IEnumerable<NavItem> items)
        {
            foreach (var it in items)
            {
                it.IsSelected = false;
                if (it.Children != null)
                    ClearSelection(it.Children);
            }
        }

        public bool ShowMenu 
        {
            get{ return _showMenu;   } 
            set{ SetProperty(ref _showMenu, value);}
        }
        private bool _showMenu = true;
    }
}
