using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using IpanelWpf.Core;
using IpanelWpf.Models;

namespace IpanelWpf.ViewModels
{
    public class DashboardViewModel : ObservableObject
    {
        public string PageName => "概览";

        // 概览统计
        public ObservableCollection<StatItem> Stats { get; }

        // 状态仪表
        public double LoadValue => 0.0;
        public double CpuValue => 4.7;
        public double MemValue => 59.69;
        public double DiskValue => 16.49;

        // 监控曲线（600x160 视口）
        public PointCollection UpSeries { get; }
        public PointCollection DownSeries { get; }

        // 系统信息
        public ObservableCollection<SysInfoRow> SysInfo { get; }

        // 应用列表
        public ObservableCollection<AppSummaryItem> Apps { get; }

        public DashboardViewModel()
        {
            Stats = new ObservableCollection<StatItem>
            {
                new StatItem { Label = "智能体", Value = "0" },
                new StatItem { Label = "网站", Value = "0" },
                new StatItem { Label = "数据库 · 所有", Value = "1" },
                new StatItem { Label = "已安装应用", Value = "6" },
            };

            UpSeries = BuildSeries(0.06, 0.92, 42);
            DownSeries = BuildSeries(0.05, 0.95, 42);

            SysInfo = new ObservableCollection<SysInfoRow>
            {
                new SysInfoRow { Name = "主机名称", Value = "twrm-dp38ep7hz2" },
                new SysInfoRow { Name = "发行版本", Value = "CentOS Linux 8" },
                new SysInfoRow { Name = "内核版本", Value = "4.18.0-193.el8.x86_64" },
                new SysInfoRow { Name = "系统类型", Value = "x86_64" },
                new SysInfoRow { Name = "主机地址", Value = "172.18.0.1" },
                new SysInfoRow { Name = "启动时间", Value = "2026-09-23 15:43:08" },
                new SysInfoRow { Name = "运行时间", Value = "23天 22小时 32分钟 08秒" },
            };

            Apps = new ObservableCollection<AppSummaryItem>
            {
                new AppSummaryItem { Name = "mysql", Desc = "版本: 8.4.11", IconKind = MaterialDesignThemes.Wpf.PackIconKind.Database, ColorHex = "#1D6FB8" },
                new AppSummaryItem { Name = "redis", Desc = "版本: 8.10.2", IconKind = MaterialDesignThemes.Wpf.PackIconKind.Layers, ColorHex = "#A62423" },
                new AppSummaryItem { Name = "1Panel AI 网关", Desc = "全新 AI 终端——嵌入式智能平台", IconKind = MaterialDesignThemes.Wpf.PackIconKind.Robot, ColorHex = "#F59E0B" },
                new AppSummaryItem { Name = "MaxKB", Desc = "基于大语言模型的企业级知识库平台", IconKind = MaterialDesignThemes.Wpf.PackIconKind.Chat, ColorHex = "#3B82F6" },
                new AppSummaryItem { Name = "OpenResty", Desc = "基于 NGINX & LuaJIT 的 Web 平台", IconKind = MaterialDesignThemes.Wpf.PackIconKind.Web, ColorHex = "#16A34A" },
                new AppSummaryItem { Name = "DeepSeek Harmony", Desc = "DeepSeek 开源智能眼镜开发环境", IconKind = MaterialDesignThemes.Wpf.PackIconKind.Fish, ColorHex = "#4B5563", ShowFlame = true },
            };
        }

        private static PointCollection BuildSeries(double amplitude, double baseLine, int count)
        {
            var rnd = new Random(baseLine.GetHashCode() + count);
            var points = new PointCollection();
            for (int i = 0; i <= count; i++)
            {
                var x = 600.0 * i / count;
                var y = 160.0 * (baseLine - rnd.NextDouble() * amplitude);
                points.Add(new Point(x, y));
            }
            return points;
        }
    }

    public class StatItem
    {
        public string Label { get; set; }
        public string Value { get; set; }
    }
}
