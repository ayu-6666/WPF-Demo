using System.Collections.Generic;
using Elastic.BMS.Core;
using Elastic.BMS.Models;

namespace Elastic.BMS.ViewModels
{
    /// <summary>实时监控</summary>
    public class RealtimeViewModel : ObservableObject
    {
        // ---- 顶部数据条 ----
        public List<StatGroup> StatGroups { get; }

        // ---- 电池（电芯电压） ----
        public List<CellRow> Cells { get; } = new List<CellRow>();

        // ---- 电芯温度 ----
        public List<CellRow> Temps { get; } = new List<CellRow>();

        // ---- 控制 ----
        public List<string> PackOptions { get; } = new List<string> { "1" };
        public string SelectedPack { get; set; } = "1";

        public List<StateCell> StateCells { get; }

        public RealtimeViewModel()
        {
            StatGroups = new List<StatGroup>
            {
                new StatGroup { Cells = new List<StatCell>
                {
                    new StatCell { Label = "总电压", Value = "0.00", Unit = "mV" },
                    new StatCell { Label = "总电流", Value = "0.00", Unit = "A" },
                }},
                new StatGroup { Cells = new List<StatCell>
                {
                    new StatCell { Label = "SOC", Value = "0.00", Unit = "%" },
                    new StatCell { Label = "SOH", Value = "0.00", Unit = "%" },
                }},
                new StatGroup { Cells = new List<StatCell>
                {
                    new StatCell { Label = "剩余容量", Value = "0.00", Unit = "AH" },
                    new StatCell { Label = "充满容量", Value = "0.00", Unit = "AH" },
                }},
                new StatGroup { Cells = new List<StatCell>
                {
                    new StatCell { Label = "最高电压", Value = "0.00", Unit = "mV", Extra = "序号", ExtraValue = "1" },
                    new StatCell { Label = "最低电压", Value = "0.00", Unit = "mV", Extra = "序号", ExtraValue = "1" },
                }},
                new StatGroup { Cells = new List<StatCell>
                {
                    new StatCell { Label = "均衡温度", Value = "0.00", Unit = "℃" },
                    new StatCell { Label = "环境温度", Value = "0.00", Unit = "℃" },
                    new StatCell { Label = "MOS温度", Value = "0.00", Unit = "℃" },
                }},
            };

            for (var i = 1; i <= 20; i++) Cells.Add(new CellRow("电芯" + i, "mV", ""));
            for (var i = 1; i <= 20; i++) Temps.Add(new CellRow("温度" + i, "℃", ""));

            StateCells = new List<StateCell>
            {
                new StateCell("充电MOS"),
                new StateCell("放电MOS"),
                new StateCell("有效充电"),
                new StateCell("有效放电"),
                new StateCell("限流模块"),
                new StateCell("加热膜"),
                new StateCell("充电器"),
                new StateCell("充满"),
            };
        }
    }
}
