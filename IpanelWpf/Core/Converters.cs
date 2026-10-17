using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace IpanelWpf.Core
{
    /// <summary>枚举/字符串匹配转可见性（parameter 与 value 相等时可见）。</summary>
    public class EqualToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Equals(value?.ToString(), parameter?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>索引匹配转可见性（int(value) == int(parameter) 时可见；参数以 "ge:" 开头时为 >= 判断）。</summary>
    public class IndexToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var p = parameter?.ToString() ?? "0";
            if (!int.TryParse(value?.ToString(), out var v))
                return Visibility.Collapsed;
            if (p.StartsWith("ge:"))
            {
                return int.TryParse(p.Substring(3), out var min) && v >= min ? Visibility.Visible : Visibility.Collapsed;
            }
            return int.TryParse(p, out var e) && v == e ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>索引与参数相等转 bool（用于分段单选按钮）。</summary>
    public class IndexToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => int.TryParse(value?.ToString(), out var v) && int.TryParse(parameter?.ToString(), out var p) && v == p;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true && int.TryParse(parameter?.ToString(), out var p) ? p : Binding.DoNothing;
        }
    }

    /// <summary>导航选中态转前景色。</summary>
    public class NavBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var selected = value is bool b && b;
            var key = parameter?.ToString() == "child" && !selected ? "TextSecondaryBrush" : (selected ? "PrimaryBrush" : "TextSecondaryBrush");
            return Application.Current != null && Application.Current.Resources.Contains(key)
                ? (Brush)Application.Current.Resources[key]
                : Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }
}
