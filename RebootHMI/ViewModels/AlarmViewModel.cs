using System.Collections.ObjectModel;
using RebootHMI.Core;
using RebootHMI.Models;

namespace RebootHMI.ViewModels
{
    public class AlarmViewModel : ObservableObject
    {
        private string _searchText = "请输入内容";
        private string _selectedCategory = "望道";
        private string _selectedDate = "2025年7月1日";

        public ObservableCollection<string> Keywords { get; } = new ObservableCollection<string> { "料道", "望道" };

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        public string SelectedDate
        {
            get => _selectedDate;
            set => SetProperty(ref _selectedDate, value);
        }

        public ObservableCollection<string> Categories { get; } =
            new ObservableCollection<string> { "料道", "望道", "AGV", "相机" };

        public ObservableCollection<string> Dates { get; } =
            new ObservableCollection<string> { "2025年7月1日", "2025年6月30日", "2025年6月29日" };

        public ObservableCollection<LogEntry> Logs { get; } = new ObservableCollection<LogEntry>
        {
            new LogEntry { Time = "2025.07.20 15:26:31", Text = "机械正常", State = "Normal" },
            new LogEntry { Time = "2025.07.20 17:33:39", Text = "机械暂停", State = "Pause" },
            new LogEntry { Time = "2025.07.22 09:39:46", Text = "机械正常", State = "Normal" },
            new LogEntry { Time = "2025.07.22 10:23:11", Text = "机械暂停", State = "Pause" },
            new LogEntry { Time = "2025.07.22 18:28:14", Text = "机械正常", State = "Normal" },
            new LogEntry { Time = "2025.07.23 10:45:14", Text = "机械运行", State = "Run" },
            new LogEntry { Time = "2025.07.23 10:46:35", Text = "机械正常", State = "Normal" },
            new LogEntry { Time = "2025.07.30 10:35:05", Text = "机械运行", State = "Run" },
        };

        public ObservableCollection<AlarmItem> Alarms { get; } = new ObservableCollection<AlarmItem>
        {
            new AlarmItem { Time = "10:27", Text = "这是一条设备故障信息" },
            new AlarmItem { Time = "10:27", Text = "这是一条设备故障信息" },
            new AlarmItem { Time = "10:27", Text = "这是一条设备故障信息" },
            new AlarmItem { Time = "10:27", Text = "这是一条设备故障信息" },
        };
    }
}
