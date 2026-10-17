using System.Collections.ObjectModel;
using IpanelWpf.Core;
using IpanelWpf.Models;

namespace IpanelWpf.ViewModels
{
    public class FilesViewModel : ObservableObject
    {
        public string PageName => "文件";

        public string PathText { get; set; } = "/home";

        public string SearchText { get; set; }

        public ObservableCollection<FileRow> Items { get; }

        public FilesViewModel()
        {
            Items = new ObservableCollection<FileRow>
            {
                new FileRow { Name = ".panel_clesh", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-09-23 15:24:19" },
                new FileRow { Name = "boot", Perm = "0555", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-09-23 15:03:34" },
                new FileRow { Name = "dev", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-09-23 15:43:19" },
                new FileRow { Name = "etc", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-10-16 09:31:24" },
                new FileRow { Name = "home", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-09-28 10:47:51", ShowStar = true },
                new FileRow { Name = "media", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2019-05-11 08:33:06" },
                new FileRow { Name = "mnt", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2021-01-20 18:33:06" },
                new FileRow { Name = "none", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2021-01-20 18:08:46" },
                new FileRow { Name = "opt", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-09-28 10:26:22" },
                new FileRow { Name = "proc", Perm = "0555", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-09-28 10:42:11" },
                new FileRow { Name = "root", Perm = "0550", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-10-16 09:04:21" },
                new FileRow { Name = "run", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-10-16 09:27:05" },
                new FileRow { Name = "srv", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2019-05-11 08:33:06" },
                new FileRow { Name = "sys", Perm = "0555", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-09-23 15:43:11" },
                new FileRow { Name = "tmp", Perm = "0777", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2026-10-11 14:16:12" },
                new FileRow { Name = "usr", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2021-01-20 18:09:05" },
                new FileRow { Name = "var", Perm = "0755", UserGroup = "root (0) / root (0)", Size = "计算", Time = "2021-01-20 18:14:16" },
                new FileRow { Name = "autoupdatedel", IsFolder = false, Perm = "0644", UserGroup = "root (0) / root (0)", Size = "0 B", Time = "2021-01-20 18:14:16" },
            };
        }
    }
}
