using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Tang.RadarApplication.Models
{
    public enum ShapeType
    {
        Point,          // 目标点
        Line,           // 直线
        ArrowLine,      // 箭头直线
        Triangle,       // 三角形
        Rectangle,      // 矩形
        Ellipse,        // 椭圆
        Ring            // 圆环
    }

    public class ShapeModel : INotifyPropertyChanged
    {
        private int _id;
        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        private ShapeType _type;
        public ShapeType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        private List<Point> _points = new List<Point>();
        public List<Point> Points
        {
            get => _points;
            set { _points = value; OnPropertyChanged(); }
        }

        private string _text;
        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); }
        }

        private Color _strokeColor = Colors.DodgerBlue;
        public Color StrokeColor
        {
            get => _strokeColor;
            set { _strokeColor = value; OnPropertyChanged(); }
        }

        private Color _fillColor = Color.FromArgb(40, 30, 144, 255);
        public Color FillColor
        {
            get => _fillColor;
            set { _fillColor = value; OnPropertyChanged(); }
        }

        private double _strokeThickness = 1.5;
        public double StrokeThickness
        {
            get => _strokeThickness;
            set { _strokeThickness = value; OnPropertyChanged(); }
        }

        public string Description => $"ID: {Id} | 类型: {Type} | 坐标: ({Points?[0].X:F2}, {Points?[0].Y:F2})";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
