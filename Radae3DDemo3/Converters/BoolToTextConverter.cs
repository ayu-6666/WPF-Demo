using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Radae3DDemo3.Converters
{
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? "已探测" : "未探测";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToInverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class KmToPixelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double km) return km * 4; // 1公里=4像素
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ScanAngleToPathConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || values[0] is not double scanAngle || values[1] is not double radius)
                return Geometry.Empty;


            var centerX = 400.0;
            var centerY = 400.0;
            // 尾迹从当前角度往前30度
            var startAngle = (scanAngle - 30) * Math.PI / 180;
            var endAngle = scanAngle * Math.PI / 180;

            // 扇形路径：中心 → 弧起点 → 弧终点 → 中心
            var pathFigure = new PathFigure { StartPoint = new Point(centerX, centerY) };
            pathFigure.Segments.Add(new LineSegment(
                new Point(centerX + radius * Math.Sin(startAngle), centerY - radius * Math.Cos(startAngle)), true));
            pathFigure.Segments.Add(new ArcSegment(
                new Point(centerX + radius * Math.Sin(endAngle), centerY - radius * Math.Cos(endAngle)),
                new Size(radius, radius), 30, false, SweepDirection.Clockwise, true));
            pathFigure.Segments.Add(new LineSegment(new Point(centerX, centerY), true));

            return new PathGeometry(new[] { pathFigure });
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
