using System.Collections.ObjectModel;
using IpanelWpf.Core;
using IpanelWpf.Models;

namespace IpanelWpf.ViewModels
{
    public class FirewallViewModel : ObservableObject
    {
        public string PageName => "防火墙";

        public ObservableCollection<string> Tabs { get; }
        public int TabIndex { get; set; }

        public string SearchText { get; set; }

        public ObservableCollection<FirewallRow> Rows { get; }

        public FirewallViewModel()
        {
            Tabs = new ObservableCollection<string> { "主机防火墙", "容器端口防护", "端口转发", "设置" };

            Rows = new ObservableCollection<FirewallRow>
            {
                new FirewallRow
                {
                    Name = "1Panel-bililive-go-dOCL", Compose = "bililive-go",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:8981/tcp" },
                        new PortChipModel { Text = "1:8981/tcp" },
                    }
                },
                new FirewallRow
                {
                    Name = "Web-server", Compose = "nginx-proxy",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:8695/tcp" },
                        new PortChipModel { Text = "0.0.0.0:8585/tcp" },
                        new PortChipModel { Text = "0.0.0.0:20081/tcp" },
                        new PortChipModel { Text = "1:8686/tcp" },
                        new PortChipModel { Text = "1:8888/tcp" },
                        new PortChipModel { Text = "+1" },
                    }
                },
                new FirewallRow
                {
                    Name = "mysql", Compose = "mysql",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:3306/tcp" },
                        new PortChipModel { Text = "1:3306/tcp" },
                    }
                },
                new FirewallRow
                {
                    Name = "openvpn", Compose = "openvpn",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:9443/tcp", IsGreen = true },
                        new PortChipModel { Text = "0.0.0.0:943/tcp", IsGreen = true },
                        new PortChipModel { Text = "0.0.0.0:1194/udp" },
                        new PortChipModel { Text = "1:443/tcp" },
                        new PortChipModel { Text = "1:940/tcp" },
                        new PortChipModel { Text = "+1" },
                    }
                },
                new FirewallRow
                {
                    Name = "redis", Compose = "redis",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:6379/tcp" },
                        new PortChipModel { Text = "1:6379/tcp" },
                    }
                },
                new FirewallRow
                {
                    Name = "tomcat", Compose = "tomcat",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:8087/tcp", IsGreen = true },
                        new PortChipModel { Text = "1:8087/tcp" },
                    }
                },
            };
        }
    }
}
