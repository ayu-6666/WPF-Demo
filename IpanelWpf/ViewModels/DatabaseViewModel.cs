using System.Collections.ObjectModel;
using IpanelWpf.Core;
using IpanelWpf.Models;

namespace IpanelWpf.ViewModels
{
    public class DatabaseViewModel : ObservableObject
    {
        public string PageName => "数据库";

        public ObservableCollection<string> Tabs { get; }
        public int TabIndex { get; set; }

        public ObservableCollection<string> SubTabs { get; }
        public int SubTabIndex { get; set; }

        public string SearchText { get; set; }

        public ObservableCollection<DbRow> Rows { get; }

        public DatabaseViewModel()
        {
            Tabs = new ObservableCollection<string> { "MySQL", "PostgreSQL", "Redis", "MongoDB" };
            SubTabs = new ObservableCollection<string>
            {
                "创建", "连接信息", "用户", "从服务器同步", "远程服务器", "终端", "管理"
            };
            SubTabIndex = 0;

            Rows = new ObservableCollection<DbRow>
            {
                new DbRow { Name = "测试", Users = "1 个用户", Desc = "hy", Time = "2026-10-16 09:33:17" },
            };
        }
    }
}
