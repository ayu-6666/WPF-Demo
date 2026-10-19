using System.Collections.Generic;
using MaterialDesignThemes.Wpf;

namespace Elastic.BMS.Models
{
    /// <summary>底部导航项</summary>
    public class NavItem
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public PackIconKind Icon { get; set; }
    }

    /// <summary>顶部数据条中的一格（名称 + 数值 + 单位，可选附加"序号"格）</summary>
    public class StatCell
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        /// <summary>数值是否用绿色显示</summary>
        public bool IsGreen { get; set; }
        /// <summary>附加标签（如"序号"），为空时不显示</summary>
        public string Extra { get; set; }
        /// <summary>附加数值（如"1"）</summary>
        public string ExtraValue { get; set; }
    }

    /// <summary>顶部数据条（一组，可含 1~2 行）</summary>
    public class StatGroup
    {
        public List<StatCell> Cells { get; set; } = new List<StatCell>();
    }

    /// <summary>总体状态中的 2x2 图标统计框</summary>
    public class MetricBox
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public PackIconKind Icon { get; set; }
    }

    /// <summary>电芯一行（名称 + 数值 + 单位），用于电池/电芯温度面板</summary>
    public class CellRow
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public string Value { get; set; }

        public CellRow(string name, string unit = "", string value = "")
        {
            Name = name;
            Unit = unit;
            Value = value;
        }
    }

    /// <summary>控制面板下方状态格</summary>
    public class StateCell
    {
        public string Label { get; set; }

        public StateCell(string label) => Label = label;
    }

    /// <summary>运行数据键值（运行时长 / 充放电次数 / SOC / SOH）</summary>
    public class KeyValue
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public bool IsGreen { get; set; }
    }

    /// <summary>图表图例</summary>
    public class LegendItem
    {
        public string Name { get; set; }
        public string ColorHex { get; set; }
    }

    /// <summary>异常报警条目</summary>
    public class AlarmEntry
    {
        public string Time { get; set; }
        public string Text { get; set; }
        public string ButtonText { get; set; } = "关闭";
    }

    /// <summary>报警记录表格行</summary>
    public class AlarmRecord
    {
        public int Index { get; set; }
        public string Title { get; set; }
        public string Info { get; set; }
        public string Time { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public string Handle { get; set; } = "关闭";
    }
}
