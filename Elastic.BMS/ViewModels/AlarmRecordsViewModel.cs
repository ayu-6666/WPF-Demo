using System.Collections.Generic;
using Elastic.BMS.Core;
using Elastic.BMS.Models;

namespace Elastic.BMS.ViewModels
{
    /// <summary>报警记录</summary>
    public class AlarmRecordsViewModel : ObservableObject
    {
        public string StartDate { get; set; } = "2023-06-30";
        public string EndDate { get; set; } = "2023-06-30";

        public List<string> TypeOptions { get; } = new List<string> { "全部", "电压报警", "电流报警", "温度报警", "通信报警" };

        private string _selectedType = "全部";
        public string SelectedType
        {
            get => _selectedType;
            set => SetProperty(ref _selectedType, value);
        }

        public List<AlarmRecord> Records { get; }

        public string PageText { get; } = "2/5";

        public AlarmRecordsViewModel()
        {
            Records = new List<AlarmRecord>();
            for (var i = 1; i <= 20; i++)
                Records.Add(new AlarmRecord { Index = i });
        }
    }
}
