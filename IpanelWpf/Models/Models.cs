using System.Collections.ObjectModel;
using MaterialDesignThemes.Wpf;

namespace IpanelWpf.Models
{
    /// <summary>侧边栏导航项。</summary>
    public class NavItem : Core.ObservableObject
    {
        private bool _isExpanded;
        private bool _isSelected;

        public string Name { get; set; }
        public PackIconKind Icon { get; set; }
        public string PageKey { get; set; }
        public NavItem Parent { get; set; }
        public ObservableCollection<NavItem> Children { get; set; }

        /// <summary>是否显示展开箭头（与是否实际拥有子项无关，保持与原界面一致）。</summary>
        public bool ShowChevron { get; set; }

        public bool HasChildren => Children != null && Children.Count > 0;

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    /// <summary>系统信息键值行。</summary>
    public class SysInfoRow
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    /// <summary>概览页-应用列表项。</summary>
    public class AppSummaryItem
    {
        public string Name { get; set; }
        public string Desc { get; set; }
        public PackIconKind IconKind { get; set; }
        public string ColorHex { get; set; }
        public bool ShowFlame { get; set; }
    }

    /// <summary>端口标签。</summary>
    public class PortChipModel
    {
        public string Text { get; set; }
        public bool IsGreen { get; set; }
    }

    /// <summary>操作按钮（可置灰）。</summary>
    public class OpModel
    {
        public string Text { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Selected { get; set; }
    }

    /// <summary>应用商店卡片。</summary>
    public class AppStoreItem
    {
        public string Name { get; set; }
        public string LogoKind { get; set; }   // mysql / redis / nginx / openvpn / tomcat / bilibili
        public string Version { get; set; }
        public string InstallText { get; set; }
        public bool HasJump { get; set; }
        public ObservableCollection<PortChipModel> Ports { get; set; }
        public ObservableCollection<OpModel> Ops { get; set; }
    }

    /// <summary>数据库列表行。</summary>
    public class DbRow
    {
        public string Name { get; set; }
        public string Users { get; set; }
        public string Desc { get; set; }
        public string Time { get; set; }
    }

    /// <summary>容器列表行。</summary>
    public class ContainerRow
    {
        public string Name { get; set; }
        public string Image { get; set; }
        public double CpuPct { get; set; }
        public double MemPct { get; set; }
        public string Ip { get; set; }
        public string Related { get; set; }
        public ObservableCollection<PortChipModel> Ports { get; set; }
        public string Desc { get; set; }
        public ObservableCollection<OpModel> Ops { get; set; }
    }

    /// <summary>防火墙规则行。</summary>
    public class FirewallRow
    {
        public string Name { get; set; }
        public string Compose { get; set; }
        public ObservableCollection<PortChipModel> Ports { get; set; }
    }

    /// <summary>文件列表行。</summary>
    public class FileRow
    {
        public string Name { get; set; }
        public bool IsFolder { get; set; } = true;
        public string Perm { get; set; }
        public string UserGroup { get; set; }
        public string Size { get; set; }
        public string Time { get; set; }
        public string Remark { get; set; } = "-";
        public bool ShowStar { get; set; }
    }
}
