using System.Collections.Generic;
using MaterialDesignThemes.Wpf;
using Rxbit.Calibration.Core;

namespace Rxbit.Calibration.Models
{
    /// <summary>左侧导航项</summary>
    public class NavItem
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public PackIconKind Icon { get; set; }
    }

    /// <summary>输出界面表格行</summary>
    public class IoRow : ObservableObject
    {
        public string Address { get; set; }
        public string Comment { get; set; }

        private bool _valid;
        public bool Valid
        {
            get => _valid;
            set => SetProperty(ref _valid, value);
        }

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public IoRow() { }
        public IoRow(string address, string comment, bool valid = false, bool enabled = false)
        {
            Address = address; Comment = comment; Valid = valid; Enabled = enabled;
        }
    }

    /// <summary>轴参数卡片（手动参数）</summary>
    public class AxisManual : ObservableObject
    {
        public string AxisName { get; set; }

        private int _homeSpeed;
        public int HomeSpeed
        {
            get => _homeSpeed;
            set => SetProperty(ref _homeSpeed, value);
        }

        private int _approachSpeed;
        public int ApproachSpeed
        {
            get => _approachSpeed;
            set => SetProperty(ref _approachSpeed, value);
        }

        private int _homeAcc;
        public int HomeAcc
        {
            get => _homeAcc;
            set => SetProperty(ref _homeAcc, value);
        }

        private int _zeroOffset;
        public int ZeroOffset
        {
            get => _zeroOffset;
            set => SetProperty(ref _zeroOffset, value);
        }
    }

    /// <summary>轴参数卡片（自动参数）</summary>
    public class AxisAuto : ObservableObject
    {
        public string AxisName { get; set; }

        private int _p1SpeedA;
        public int P1SpeedA
        {
            get => _p1SpeedA;
            set => SetProperty(ref _p1SpeedA, value);
        }

        private int _p1SpeedB;
        public int P1SpeedB
        {
            get => _p1SpeedB;
            set => SetProperty(ref _p1SpeedB, value);
        }

        private int _p1SpeedC;
        public int P1SpeedC
        {
            get => _p1SpeedC;
            set => SetProperty(ref _p1SpeedC, value);
        }

        private int _p1SpeedD;
        public int P1SpeedD
        {
            get => _p1SpeedD;
            set => SetProperty(ref _p1SpeedD, value);
        }

        private int _acc;
        public int Acc
        {
            get => _acc;
            set => SetProperty(ref _acc, value);
        }

        private int _dec;
        public int Dec
        {
            get => _dec;
            set => SetProperty(ref _dec, value);
        }
    }

    /// <summary>轴手动控制条中的轴</summary>
    public class ManualCtrlAxis : ObservableObject
    {
        public string Label { get; set; }

        private int _value;
        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        private int _step;
        public int Step
        {
            get => _step;
            set => SetProperty(ref _step, value);
        }

        public ManualCtrlAxis() { }
        public ManualCtrlAxis(string label, int value, int step = 1)
        {
            Label = label; Value = value; Step = step;
        }
    }

    /// <summary>底部状态栏信息</summary>
    public class StatusBarInfo
    {
        public string X { get; set; }
        public string Y { get; set; }
        public string Z { get; set; }
        public string A { get; set; }
        public string B { get; set; }
        public string C { get; set; }
        public string Spindle { get; set; }
    }
}
