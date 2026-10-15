using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Tang.RadarApplication.Models
{
    public enum TargetType { Point, Line, Arrow, Triangle, Rectangle, Ellipse, Ring }

    public class TargetItem : INotifyPropertyChanged
    {
        private static int _counter;
        public int Id { get; } = ++_counter;

        private TargetType _type = TargetType.Point;
        public TargetType Type { get => _type; set => Set(ref _type, value); }

        // 主位置（世界坐标，形状中心或起点）
        private double _x, _y;
        public double X { get => _x; set => Set(ref _x, value); }
        public double Y { get => _y; set => Set(ref _y, value); }

        // 直线/箭头终点（世界坐标）
        private double _x2, _y2;
        public double X2 { get => _x2; set => Set(ref _x2, value); }
        public double Y2 { get => _y2; set => Set(ref _y2, value); }

        // 尺寸（世界单位）
        private double _width = 2, _height = 2;
        public double Width { get => _width; set => Set(ref _width, value); }
        public double Height { get => _height; set => Set(ref _height, value); }

        private double _radius = 1, _innerRadius = 0.5;
        public double Radius { get => _radius; set => Set(ref _radius, value); }
        public double InnerRadius { get => _innerRadius; set => Set(ref _innerRadius, value); }

        private string _label = "";
        public string Label { get => _label; set => Set(ref _label, value); }

        private Color _color = Colors.DeepSkyBlue;
        public Color Color { get => _color; set => Set(ref _color, value); }

        private double _thickness = 1.5;
        public double Thickness { get => _thickness; set => Set(ref _thickness, value); }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void Set<T>(ref T field, T value, [CallerMemberName] string n = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }
    }
}
