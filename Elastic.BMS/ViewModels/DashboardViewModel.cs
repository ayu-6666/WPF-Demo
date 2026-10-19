using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Elastic.BMS.Core;
using Elastic.BMS.Models;

namespace Elastic.BMS.ViewModels
{
    /// <summary>监控主页</summary>
    public class DashboardViewModel : ObservableObject
    {
        private const double ChartWidth = 1000;
        private const double ChartHeight = 210;
        private const int PointCount = 24;

        // ---- 总体状态：三个指标 ----
        public List<KeyValue> Metrics { get; }
        public List<MetricBox> MetricBoxes { get; }

        // ---- 当前电芯状态 ----
        public int GaugePercent { get; }
        public string CapacityText { get; } = "1000";
        public string RemainText { get; } = "60%";
        public List<KeyValue> RunStats { get; }

        // ---- 实时数据统计图表 ----
        public List<LegendItem> Legends { get; }
        public PointCollection SocPoints { get; }
        public PointCollection SohPoints { get; }
        public List<double> YLabels { get; }
        public List<int> XLabels { get; }

        // ---- 异常报警 ----
        public List<AlarmEntry> Alarms { get; }

        public DashboardViewModel()
        {
            Metrics = new List<KeyValue>
            {
                new KeyValue { Value = "70°C", Label = "温度", IsGreen = false },
                new KeyValue { Value = "55%", Label = "剩余电量", IsGreen = true },
                new KeyValue { Value = "1000kW", Label = "总电量", IsGreen = true },
            };

            MetricBoxes = new List<MetricBox>
            {
                new MetricBox { Label = "总电芯量", Value = "50000", Icon = MaterialDesignThemes.Wpf.PackIconKind.BatteryPlusVariant },
                new MetricBox { Label = "正在报警", Value = "1", Icon = MaterialDesignThemes.Wpf.PackIconKind.BellRingOutline },
                new MetricBox { Label = "已停机", Value = "20", Icon = MaterialDesignThemes.Wpf.PackIconKind.PowerPlugOffOutline },
                new MetricBox { Label = "总设备数", Value = "100", Icon = MaterialDesignThemes.Wpf.PackIconKind.Devices },
            };

            RunStats = new List<KeyValue>
            {
                new KeyValue { Label = "运行时长", Value = "120.11", Unit = "h", IsGreen = true },
                new KeyValue { Label = "充放电次数", Value = "245", Unit = "", IsGreen = true },
                new KeyValue { Label = "SOC", Value = "40", Unit = "%", IsGreen = true },
                new KeyValue { Label = "SOH", Value = "45", Unit = "%", IsGreen = true },
            };

            GaugePercent = 60;

            Legends = new List<LegendItem>
            {
                new LegendItem { Name = "SOC (%)", ColorHex = "#3FA9FF" },
                new LegendItem { Name = "SOH (%)", ColorHex = "#8FB6FF" },
            };

            SocPoints = BuildSeries(92.0, 2.95);
            SohPoints = BuildSeries(90.0, 2.35);

            YLabels = new List<double> { 100, 80, 60, 40, 20, 0 };

            XLabels = new List<int>();
            for (var i = 1; i <= PointCount; i++) XLabels.Add(i);

            Alarms = new List<AlarmEntry>
            {
                new AlarmEntry { Time = "2024-02-27 14:50", Text = "电芯1超压异常" },
                new AlarmEntry { Time = "2024-07-19 19:32", Text = "电芯1超压异常" },
                new AlarmEntry { Time = "2024-02-13 16:15", Text = "电芯1超压异常" },
                new AlarmEntry { Time = "2024-08-23 23:18", Text = "电芯1超压异常" },
            };
        }

        /// <summary>按 y = 起始值 - 步长 * i 生成折线点（x 取每格中点，与坐标轴刻度对齐）。</summary>
        private static PointCollection BuildSeries(double start, double step)
        {
            var pts = new PointCollection();
            for (var i = 0; i < PointCount; i++)
            {
                var v = Math.Max(0, start - step * i);
                var x = (i + 0.5) * (ChartWidth / PointCount);
                var y = ChartHeight - v / 100.0 * ChartHeight;
                pts.Add(new Point(x, y));
            }
            return pts;
        }
    }
}
