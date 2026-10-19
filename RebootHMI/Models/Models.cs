using System.Collections.Generic;
using MaterialDesignThemes.Wpf;
using RebootHMI.Core;

namespace RebootHMI.Models
{
    /// <summary>底部导航项</summary>
    public class NavItem
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public PackIconKind Icon { get; set; }
        /// <summary>多页面模块点击弹出子菜单</summary>
        public bool HasSubMenu { get; set; }
        public List<NavItem> Children { get; set; } = new List<NavItem>();
    }

    /// <summary>左侧操作按钮（开始/暂停/停止/急停/复位）</summary>
    public class OpButtonModel : ObservableObject
    {
        public string Name { get; set; }
        public PackIconKind Icon { get; set; }
        public string State { get; set; } // Running/Paused/EStop/Idle

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }
    }

    // ============ IO 信号 ============

    /// <summary>单个 IO 信号点（名称 + 状态文字 + 点亮）</summary>
    public class IoItem : ObservableObject
    {
        public string Name { get; set; }
        public string Option { get; set; }

        private bool _isOn;
        public bool IsOn
        {
            get => _isOn;
            set => SetProperty(ref _isOn, value);
        }

        public IoItem() { }

        public IoItem(string name, string option, bool isOn = false)
        {
            Name = name;
            Option = option;
            _isOn = isOn;
        }
    }

    /// <summary>一行 IO 信号（最多左/中/右三个信号点，null 表示该位置无内容）</summary>
    public class IoRow
    {
        public IoItem Left { get; set; }
        public IoItem Middle { get; set; }
        public IoItem Right { get; set; }
    }

    /// <summary>一张 IO 信号卡片</summary>
    public class IoColumn
    {
        public string Title { get; set; }
        public PackIconKind TitleIcon { get; set; } = PackIconKind.TransitConnection;
        public List<IoRow> Rows { get; set; } = new List<IoRow>();
    }

    // ============ 自动模式 ============

    /// <summary>气缸开关行（松开/夹紧 或 缩回/伸出）</summary>
    public class ToggleItem : ObservableObject
    {
        public string Label { get; set; }
        public string LeftText { get; set; }
        public string RightText { get; set; }

        private bool _isOn;
        public bool IsOn
        {
            get => _isOn;
            set => SetProperty(ref _isOn, value);
        }

        public ToggleItem() { }

        public ToggleItem(string label, string left, string right, bool isOn = false)
        {
            Label = label;
            LeftText = left;
            RightText = right;
            _isOn = isOn;
        }
    }

    /// <summary>料道控制下拉行</summary>
    public class DropdownItem
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public bool IsRed { get; set; }
    }

    // ============ 位置参数 ============

    public class TabItem : ObservableObject
    {
        public string Name { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public class AxisData : ObservableObject
    {
        public string Name { get; set; }
        public double Min { get; set; }
        public double Max { get; set; } = 136;

        private double _position = 73;
        public double Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }

        private double _speed = 0.5;
        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        private int _unit = 5;
        public int Unit
        {
            get => _unit;
            set => SetProperty(ref _unit, value);
        }
    }

    // ============ 报警 ============

    public class LogEntry
    {
        public string Time { get; set; }
        public string Text { get; set; }
        public string State { get; set; } // Normal / Pause / Run
    }

    public class AlarmItem
    {
        public string Time { get; set; }
        public string Text { get; set; }
    }
}
