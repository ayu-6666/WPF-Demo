using System.Collections.Generic;
using Elastic.BMS.Core;
using Elastic.BMS.Models;

namespace Elastic.BMS.ViewModels
{
    /// <summary>设置页的一格（标签 + 可选补充说明 + 数值 + 可选单位）</summary>
    public class SettingCell
    {
        public string Label { get; set; }
        public string Sub { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
    }

    /// <summary>设置页的一行（名称 + 若干格）</summary>
    public class SettingRow
    {
        public string Name { get; set; }
        public List<SettingCell> Cells { get; set; } = new List<SettingCell>();
    }

    /// <summary>系统设置 - 基本设置</summary>
    public class SettingsBasicViewModel : ObservableObject
    {
        public List<SettingRow> CapacityRows { get; }
        public List<SettingRow> AlarmRows { get; }
        public List<SettingRow> VoltageRows { get; }
        public List<SettingRow> CurrentRows { get; }
        public List<SettingRow> TemperatureRows { get; }

        public SettingsBasicViewModel()
        {
            CapacityRows = new List<SettingRow>
            {
                Row("总充电容量", Cell("", "", "1000", "AH")),
                Row("总放电容量", Cell("", "", "1000", "AH")),
            };

            AlarmRows = new List<SettingRow>
            {
                Row("单体电压", Cell("上限", "(充电)", "1000", ""), Cell("下限", "(放电)", "0", "")),
                Row("总电压", Cell("上限", "(充电)", "10000", ""), Cell("下限", "(放电)", "0", "")),
                Row("电芯温度", Cell("上限", "(充电)", "200", ""), Cell("下限", "(放电)", "0", "")),
            };

            VoltageRows = new List<SettingRow>
            {
                Row("单体过充", Cell("保护(V)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(V)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
                Row("单体过放", Cell("保护(V)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(V)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
                Row("总压过充", Cell("保护(V)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(V)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
                Row("总压过放", Cell("保护(V)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(V)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
            };

            CurrentRows = new List<SettingRow>
            {
                Row("充电电流", Cell("保护(A)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(A)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
                Row("放电电流1", Cell("保护(A)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(A)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
                Row("放电电流2", Cell("保护(A)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(A)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
                Row("短路电流", Cell("保护(A)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(A)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
            };

            TemperatureRows = new List<SettingRow>
            {
                Row("充电高温", Cell("保护(℃)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(℃)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
                Row("放电高温", Cell("保护(℃)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(℃)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
                Row("充电低温", Cell("保护(℃)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(℃)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
                Row("放电低温", Cell("保护(℃)", "", "10.0", ""), Cell("延时(ms)", "", "10.0", ""), Cell("恢复(℃)", "", "10.0", ""), Cell("过流保护次数", "", "1", "")),
            };
        }

        private static SettingRow Row(string name, params SettingCell[] cells)
            => new SettingRow { Name = name, Cells = new List<SettingCell>(cells) };

        private static SettingCell Cell(string label, string sub, string value, string unit)
            => new SettingCell { Label = label, Sub = sub, Value = value, Unit = unit };
    }
}
