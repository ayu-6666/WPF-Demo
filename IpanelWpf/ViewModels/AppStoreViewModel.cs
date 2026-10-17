using System.Collections.ObjectModel;
using IpanelWpf.Core;
using IpanelWpf.Models;

namespace IpanelWpf.ViewModels
{
    public class AppStoreViewModel : ObservableObject
    {
        public string PageName => "应用商店";

        public ObservableCollection<string> Tabs { get; }
        public int TabIndex { get; set; } = 1;

        public ObservableCollection<string> Categories { get; }
        public int CategoryIndex { get; set; }

        public string SearchText { get; set; }

        public ObservableCollection<AppStoreItem> Apps { get; }

        public AppStoreViewModel()
        {
            Tabs = new ObservableCollection<string> { "全部", "已安装", "可升级", "设置" };
            Categories = new ObservableCollection<string>
            {
                "全部", "AI", "网站", "数据库", "Web服务器", "实用工具", "云存储", "BI", "CRM",
                "安全", "开发工具", "DevOps", "中间件", "多媒体", "硬件服务", "休闲游戏", "本地"
            };

            Apps = new ObservableCollection<AppStoreItem>
            {
                new AppStoreItem
                {
                    Name = "mysql", LogoKind = "mysql", Version = "8.4.11", InstallText = "已安装: 1 天 4 小时",
                    Ports = new ObservableCollection<PortChipModel> { new PortChipModel { Text = "3306" } },
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "重建" }, new OpModel { Text = "重启" },
                        new OpModel { Text = "禁用", Enabled = false }, new OpModel { Text = "停止", Enabled = false },
                        new OpModel { Text = "删除" }, new OpModel { Text = "参数" },
                    }
                },
                new AppStoreItem
                {
                    Name = "redis", LogoKind = "redis", Version = "8.10.2", InstallText = "已安装: 1 天 4 小时",
                    Ports = new ObservableCollection<PortChipModel> { new PortChipModel { Text = "6379" } },
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "重建" }, new OpModel { Text = "重启" },
                        new OpModel { Text = "禁用", Enabled = false }, new OpModel { Text = "停止", Enabled = false },
                        new OpModel { Text = "删除" }, new OpModel { Text = "参数" },
                    }
                },
                new AppStoreItem
                {
                    Name = "nginx-proxy", LogoKind = "nginx", Version = "2.16.9", InstallText = "已安装: 1 天 4 小时", HasJump = true,
                    Ports = new ObservableCollection<PortChipModel> { new PortChipModel { Text = "30081" } },
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "重建" }, new OpModel { Text = "重启" },
                        new OpModel { Text = "禁用", Enabled = false }, new OpModel { Text = "停止", Enabled = false },
                        new OpModel { Text = "删除" }, new OpModel { Text = "参数" },
                    }
                },
                new AppStoreItem
                {
                    Name = "openvpn", LogoKind = "openvpn", Version = "latest", InstallText = "已安装: 1 天 4 小时", HasJump = true,
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "943" }, new PortChipModel { Text = "443" },
                    },
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "重建" }, new OpModel { Text = "重启" },
                        new OpModel { Text = "禁用", Enabled = false }, new OpModel { Text = "停止", Enabled = false },
                        new OpModel { Text = "删除" }, new OpModel { Text = "参数" },
                    }
                },
                new AppStoreItem
                {
                    Name = "tomcat", LogoKind = "tomcat", Version = "10.1.43", InstallText = "已安装: 1 天 4 小时", HasJump = true,
                    Ports = new ObservableCollection<PortChipModel> { new PortChipModel { Text = "8087" } },
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "重建" }, new OpModel { Text = "重启" },
                        new OpModel { Text = "禁用", Enabled = false }, new OpModel { Text = "停止", Enabled = false },
                        new OpModel { Text = "删除" }, new OpModel { Text = "参数" },
                    }
                },
                new AppStoreItem
                {
                    Name = "bililive-go", LogoKind = "bilibili", Version = "0.8.3", InstallText = "已安装: 1 天", HasJump = true,
                    Ports = new ObservableCollection<PortChipModel> { new PortChipModel { Text = "8089" } },
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "重建" }, new OpModel { Text = "重启" },
                        new OpModel { Text = "禁用", Enabled = false }, new OpModel { Text = "停止", Enabled = false },
                        new OpModel { Text = "删除" }, new OpModel { Text = "参数" },
                    }
                },
            };
        }
    }
}
