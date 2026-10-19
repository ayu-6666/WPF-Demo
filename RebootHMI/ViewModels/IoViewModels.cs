using System.Collections.Generic;
using RebootHMI.Models;

namespace RebootHMI.ViewModels
{
    /// <summary>IO信号-手抓-机器人</summary>
    public class IoGripperViewModel
    {
        public List<IoColumn> Columns { get; } = new List<IoColumn>();

        public IoGripperViewModel()
        {
            var gripper = new IoColumn { Title = "手抓信号", TitleIcon = MaterialDesignThemes.Wpf.PackIconKind.HandOkay };
            gripper.Rows.AddRange(new[]
            {
                new IoRow { Left = new IoItem("1#左角缸", "松开磁开"), Right = new IoItem("1#左角缸", "夹紧磁开") },
                new IoRow { Left = new IoItem("1#右角缸", "松开磁开"), Right = new IoItem("1#右角缸", "夹紧磁开") },
                new IoRow { Left = new IoItem("2#左角缸", "松开磁开", true), Right = new IoItem("2#左角缸", "夹紧磁开") },
                new IoRow { Left = new IoItem("2#右角缸", "松开磁开"), Right = new IoItem("2#右角缸", "夹紧磁开") },
                new IoRow { Left = new IoItem("左-防坠落", "松开磁开"), Right = new IoItem("右-防坠落", "夹紧磁开", true) },
                new IoRow { Left = new IoItem("左-插销缸", "松开磁开"), Right = new IoItem("右-插销缸", "夹紧磁开") },
                new IoRow { Left = new IoItem("1#前档停检", "松开磁开"), Right = new IoItem("1#挡停上升", "夹紧磁开") },
                new IoRow { Left = new IoItem("1#前档停检", "有料"), Right = new IoItem("1#挡停上升", "到位") },
                new IoRow { Left = new IoItem("1#前档停检", "有料"), Right = new IoItem("1#挡停上升", "到位") },
            });

            Columns.Add(gripper);
            Columns.Add(BuildRobotColumn("拍照机器人"));
            Columns.Add(BuildRobotColumn("抓放机器人"));
        }

        private static IoColumn BuildRobotColumn(string title)
        {
            var col = new IoColumn { Title = title, TitleIcon = MaterialDesignThemes.Wpf.PackIconKind.RobotIndustrial };
            col.Rows.AddRange(new[]
            {
                new IoRow { Left = new IoItem("控制就绪", "就绪") },
                new IoRow { Left = new IoItem("机器人急停", "急停") },
                new IoRow { Left = new IoItem("程序启动", "完成", true) },
                new IoRow { Left = new IoItem("接口反馈", "激活") },
                new IoRow { Left = new IoItem("程序激活", "请求") },
                new IoRow { Left = new IoItem("程序编号", "有效") },
                new IoRow { Left = new IoItem("起始位置", "就位") },
                new IoRow { Left = new IoItem("T1模式", "打开") },
                new IoRow { Left = new IoItem("内部控制", "自动") },
            });
            return col;
        }
    }

    /// <summary>IO信号-进料道-拆机盘</summary>
    public class IoFeedTrayViewModel
    {
        public List<IoColumn> Columns { get; } = new List<IoColumn>();

        public IoFeedTrayViewModel()
        {
            var feed = new IoColumn { Title = "进料道信号", TitleIcon = MaterialDesignThemes.Wpf.PackIconKind.ArrowDecisionOutline };
            feed.Rows.AddRange(new[]
            {
                new IoRow { Left = new IoItem("1#前档停检", "有料"), Right = new IoItem("1#挡停上升", "到位") },
                new IoRow { Left = new IoItem("1#料检", "有料", true), Right = new IoItem("1#挡停下降", "到位") },
                new IoRow { Left = new IoItem("2#前档停检", "有料"), Right = new IoItem("2#挡停上升", "到位") },
                new IoRow { Left = new IoItem("2#料检", "有料"), Right = new IoItem("2#挡停下降", "到位", true) },
                new IoRow { Left = new IoItem("2#工位上升", "有料"), Right = new IoItem("3#挡停上升", "到位") },
                new IoRow { Left = new IoItem("3#前档停检", "有料"), Right = new IoItem("3#挡停下降", "到位") },
                new IoRow { Left = new IoItem("3#料检", "有料"), Right = new IoItem("扫码不合格", "到位") },
                new IoRow { Left = new IoItem("扫码合格", "有料"), Right = new IoItem("拍照机器人", "到位") },
                new IoRow { Left = new IoItem("抓放机器人", "有料"), Right = new IoItem("1#料道电机", "到位") },
                new IoRow { Left = new IoItem("1#前档停检", "有料"), Right = new IoItem("2#挡停上升", "到位") },
            });

            var tray = new IoColumn { Title = "拆机盘信号", TitleIcon = MaterialDesignThemes.Wpf.PackIconKind.ArrowDecisionOutline };
            tray.Rows.Add(new IoRow
            {
                Left = new IoItem("一号叉车", "有料"),
                Right = new IoItem("1#挡停上升", "到位")
            });
            for (var i = 0; i < 7; i++)
            {
                tray.Rows.Add(new IoRow
                {
                    Left = new IoItem("1#前档停检", "有料"),
                    Right = new IoItem("1#挡停上升", "到位")
                });
            }

            Columns.Add(feed);
            Columns.Add(tray);
        }
    }

    /// <summary>IO信号-下料道信号</summary>
    public class IoDischargeViewModel
    {
        public List<IoColumn> ControlColumns { get; } = new List<IoColumn>();
        public List<IoColumn> RealtimeColumns { get; } = new List<IoColumn>();

        public string OutCode { get; set; } = string.Empty;
        public string PhotoState { get; set; } = "待拍照";
        public List<string> PhotoOptions { get; } = new List<string> { "待拍照", "拍照中", "拍照完成" };

        public IoDischargeViewModel()
        {
            var control = new IoColumn { Title = "下料道控制" };
            control.Rows.AddRange(new[]
            {
                new IoRow { Left = new IoItem("1#下料道", "一号"), Right = new IoItem("2#下料道", "一号") },
                new IoRow { Left = new IoItem("1#下料道", "二号"), Right = new IoItem("2#下料道", "二号") },
                new IoRow { Left = new IoItem("1#下料道", "三号"), Right = new IoItem("2#下料道", "三号") },
            });
            for (var i = 0; i < 7; i++)
            {
                control.Rows.Add(new IoRow
                {
                    Left = new IoItem("1#前档停检", "有料", i == 1),
                    Right = new IoItem("1#挡停上升", "到位")
                });
            }
            ControlColumns.Add(control);

            var realtime = new IoColumn { Title = "实时-下料道" };
            realtime.Rows.AddRange(new[]
            {
                new IoRow { Left = new IoItem("1#-1有料", "有料", true), Right = new IoItem("2#-1有料", "到位") },
                new IoRow { Left = new IoItem("1#-2有料", "有料"), Right = new IoItem("2#-2有料", "到位") },
                new IoRow { Left = new IoItem("3#-1有料", "有料"), Right = new IoItem("1#挡停上升", "到位") },
                new IoRow { Left = new IoItem("3#-2有料", "有料"), Right = new IoItem("1#挡停上升", "到位") },
                new IoRow { Left = new IoItem("4#-1有料", "有料"), Right = new IoItem("1#挡停上升", "到位") },
            });
            RealtimeColumns.Add(realtime);
        }
    }

    /// <summary>IO信号-NG料道-人检区</summary>
    public class IoNgInspectViewModel
    {
        public List<IoColumn> TopColumns { get; } = new List<IoColumn>();
        public IoColumn CameraColumn { get; }

        public IoNgInspectViewModel()
        {
            var ng = new IoColumn { Title = "NG料道信号" };
            ng.Rows.AddRange(new[]
            {
                new IoRow { Left = new IoItem("一号检测", "有料"), Right = new IoItem("NG机器人放料", "到位") },
                new IoRow { Left = new IoItem("二号检测", "有料"), Right = new IoItem("AGV取托盘", "到位") },
                new IoRow { Left = new IoItem("挡停上升", "到位", true), Right = new IoItem("AGV送空托盘", "到位") },
                new IoRow { Left = new IoItem("挡停下降", "到位"), Right = new IoItem("呼叫AGV取托盘", "到位", true) },
                new IoRow { Left = new IoItem("托盘检测", "到位"), Right = new IoItem("呼叫AGV送托盘", "到位") },
                new IoRow { Left = new IoItem("托盘感应", "到位") },
            });

            var inspect = new IoColumn { Title = "人检区信号" };
            inspect.Rows.AddRange(new[]
            {
                new IoRow { Left = new IoItem("1#人检感应", "有料"), Right = new IoItem("2#人检感应", "到位") },
                new IoRow { Left = new IoItem("1#人检到位", "到位"), Right = new IoItem("2#人检到位", "到位") },
            });
            for (var i = 0; i < 5; i++)
            {
                inspect.Rows.Add(new IoRow
                {
                    Left = new IoItem("1#前档停检", "有料"),
                    Right = new IoItem("1#挡停上升", "到位")
                });
            }

            TopColumns.Add(ng);
            TopColumns.Add(inspect);

            CameraColumn = new IoColumn { Title = "相机信号", TitleIcon = MaterialDesignThemes.Wpf.PackIconKind.Camera };
            for (var i = 0; i < 4; i++)
            {
                CameraColumn.Rows.Add(new IoRow
                {
                    Left = new IoItem(i == 0 ? "相机在线" : string.Empty, "常亮"),
                    Middle = new IoItem(i == 0 ? "相机在线" : string.Empty, "常亮"),
                    Right = new IoItem(i == 0 ? "相机拍照" : string.Empty, "拍照")
                });
            }
        }
    }
}
