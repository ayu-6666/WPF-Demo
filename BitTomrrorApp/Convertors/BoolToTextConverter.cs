using BitTomrrorApp.Models;
using System.Globalization;
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

// 状态 -> 徽章背景色
public class StateToColorConverter : IValueConverter
{
    private static readonly Brush Downloading = MakeBrush("#1976D2");
    private static readonly Brush Paused = MakeBrush("#9E9E9E");
    private static readonly Brush Completed = MakeBrush("#388E3C");
    private static readonly Brush Seeding = MakeBrush("#F57C00");
    private static readonly Brush Error = MakeBrush("#D32F2F");
    private static readonly Brush Waiting = MakeBrush("#7B1FA2");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TorrentState s)
        {
            return s switch
            {
                TorrentState.Downloading => Downloading,
                TorrentState.Paused => Paused,
                TorrentState.Completed => Completed,
                TorrentState.Seeding => Seeding,
                TorrentState.Error => Error,
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
        if (value is TorrentState s)
        {
            return s switch
            {
                TorrentState.Downloading => "Pause",
                TorrentState.Seeding => "Pause",
                TorrentState.Paused => "Play",
                TorrentState.Completed => "Refresh",
                TorrentState.Error => "AlertCircleOutline",
                _ => "Play"
            };
        }
        return "Play";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
