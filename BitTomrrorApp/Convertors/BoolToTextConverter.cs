using BitTomrrorApp.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BitTomrrorApp.Convertors;

public class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "已探测" : "未探测";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToInverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// 状态 -> 徽章背景色
public class StateToColorConverter : IValueConverter
{
    private static readonly Brush Downloading = MakeBrush("#1976D2");
    private static readonly Brush Preparing = MakeBrush("#0288D1");
    private static readonly Brush Paused = MakeBrush("#9E9E9E");
    private static readonly Brush Completed = MakeBrush("#388E3C");
    private static readonly Brush Seeding = MakeBrush("#F57C00");
    private static readonly Brush Error = MakeBrush("#D32F2F");
    private static readonly Brush Waiting = MakeBrush("#7B1FA2");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DownloadStatus s)
        {
            return s switch
            {
                DownloadStatus.Downloading => Downloading,
                DownloadStatus.Preparing => Preparing,
                DownloadStatus.Paused => Paused,
                DownloadStatus.Completed => Completed,
                DownloadStatus.Seeding => Seeding,
                DownloadStatus.Error => Error,
                DownloadStatus.Waiting => Waiting,
                DownloadStatus.Stopped => Paused,
                _ => Waiting
            };
        }
        return Paused;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static Brush MakeBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}

// 状态 -> 操作按钮图标（暂停/继续）
public class StateToPauseIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DownloadStatus s)
        {
            return s switch
            {
                DownloadStatus.Downloading => "Pause",
                DownloadStatus.Preparing => "Pause",
                DownloadStatus.Seeding => "Pause",
                DownloadStatus.Paused => "Play",
                DownloadStatus.Waiting => "Play",
                DownloadStatus.Stopped => "Play",
                DownloadStatus.Completed => "CheckCircleOutline",
                DownloadStatus.Error => "Refresh",
                _ => "Play"
            };
        }
        return "Play";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
