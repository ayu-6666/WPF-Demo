using System.Collections.ObjectModel;
using IpanelWpf.Core;
using IpanelWpf.Models;

namespace IpanelWpf.ViewModels
{
    public class ContainerViewModel : ObservableObject
    {
        public string PageName => "容器";

        public ObservableCollection<string> Tabs { get; }
        public int TabIndex { get; set; } = 1;

        public string SearchText { get; set; }

        public ObservableCollection<ContainerRow> Rows { get; }

        public ContainerViewModel()
        {
            Tabs = new ObservableCollection<string>
            {
                "概览", "容器", "编排", "镜像", "网络", "存储卷", "仓库", "操作模板", "配置"
            };

            Rows = new ObservableCollection<ContainerRow>
            {
                new ContainerRow
                {
                    Name = "1Panel-bililive-go-dOCL", Image = "chigusa/bililive-go:v0.8.3",
                    CpuPct = 0.60, MemPct = 3.90, Ip = "172.18.0.7",
                    Related = "应用: Bililive-go [bililive-go]",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:8001->8080/tcp" },
                        new PortChipModel { Text = "8001->8001/tcp" },
                        new PortChipModel { Text = "+1" },
                    },
                    Desc = "-",
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "控制", Selected = true }, new OpModel { Text = "日志" }, new OpModel { Text = "更多" },
                    }
                },
                new ContainerRow
                {
                    Name = "Web-server", Image = "jc21/nginx-proxy-manager:l...",
                    CpuPct = 0.01, MemPct = 2.37, Ip = "172.18.0.4",
                    Related = "应用: Nginx Proxy Manager [...]",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:8086->445/tcp" },
                        new PortChipModel { Text = "0.0.0.0:8088->80/tcp" },
                        new PortChipModel { Text = "+4" },
                    },
                    Desc = "-",
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "控制" }, new OpModel { Text = "日志" }, new OpModel { Text = "更多" },
                    }
                },
                new ContainerRow
                {
                    Name = "mysql", Image = "mysql:8.4.11",
                    CpuPct = 0.24, MemPct = 11.80, Ip = "172.18.0.2",
                    Related = "应用: MySQL [mysql]",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:3296->3306/tcp" },
                        new PortChipModel { Text = "33060/tcp" },
                        new PortChipModel { Text = "+1" },
                    },
                    Desc = "-",
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "控制" }, new OpModel { Text = "日志" }, new OpModel { Text = "更多" },
                    }
                },
                new ContainerRow
                {
                    Name = "openvpn", Image = "openvpn/openvpn-as:1...",
                    CpuPct = 0.07, MemPct = 13.22, Ip = "172.18.0.5",
                    Related = "应用: OpenVPN [openvpn]",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:943->943/tcp" },
                        new PortChipModel { Text = "0.0.0.0:945->945/tcp" },
                        new PortChipModel { Text = "+4" },
                    },
                    Desc = "-",
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "控制" }, new OpModel { Text = "日志" }, new OpModel { Text = "更多" },
                    }
                },
                new ContainerRow
                {
                    Name = "redis", Image = "redis:8.10.2",
                    CpuPct = 0.16, MemPct = 0.46, Ip = "172.18.0.3",
                    Related = "应用: Redis [redis]",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:6379->6379/tcp" },
                        new PortChipModel { Text = "::6379->6379/tcp" },
                    },
                    Desc = "-",
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "控制" }, new OpModel { Text = "日志" }, new OpModel { Text = "更多" },
                    }
                },
                new ContainerRow
                {
                    Name = "tomcat", Image = "bitnami/legacy/tomcat:...",
                    CpuPct = 0.41, MemPct = 3.62, Ip = "172.18.0.6",
                    Related = "应用: Apache Tomcat [tomcat]",
                    Ports = new ObservableCollection<PortChipModel>
                    {
                        new PortChipModel { Text = "0.0.0.0:8087->8080/tcp" },
                        new PortChipModel { Text = "8000/tcp" },
                        new PortChipModel { Text = "+1" },
                    },
                    Desc = "-",
                    Ops = new ObservableCollection<OpModel>
                    {
                        new OpModel { Text = "控制" }, new OpModel { Text = "日志" }, new OpModel { Text = "更多" },
                    }
                },
            };
        }
    }
}
