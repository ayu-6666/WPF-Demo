using System.Globalization;
using WxChatApp.Services;

namespace WxChatApp.Helpers;

public static class TimeFormatter
{
    private static bool En => Loc.IsEnglish;

    public static DateTime Parse(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
        {
            return DateTime.Now;
        }
        return DateTime.TryParse(iso, null, DateTimeStyles.RoundtripKind, out var time)
            ? time.ToLocalTime()
            : DateTime.Now;
    }

    public static string ConversationTime(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
        {
            return "";
        }
        var time = Parse(iso);
        var today = DateTime.Today;
        if (time.Date == today)
        {
            return time.ToString("HH:mm", Loc.Culture);
        }
        if (time.Date == today.AddDays(-1))
        {
            return Loc.T("Time.Yesterday");
        }
        if (time.Year == today.Year)
        {
            return En ? time.ToString("MMM d", Loc.Culture) : time.ToString("M月d日", Loc.Culture);
        }
        return En ? time.ToString("M/d/yyyy", Loc.Culture) : time.ToString("yyyy/M/d", Loc.Culture);
    }

    public static string MessageTime(string? iso)
    {
        var time = Parse(iso);
        var today = DateTime.Today;
        if (time.Date == today)
        {
            return time.ToString("HH:mm", Loc.Culture);
        }
        if (time.Date == today.AddDays(-1))
        {
            return Loc.T("Time.Yesterday") + " " + time.ToString("HH:mm", Loc.Culture);
        }
        if (time.Year == today.Year)
        {
            return En
                ? time.ToString("MMM d HH:mm", Loc.Culture)
                : time.ToString("M月d日 HH:mm", Loc.Culture);
        }
        return En
            ? time.ToString("MMM d, yyyy HH:mm", Loc.Culture)
            : time.ToString("yyyy年M月d日 HH:mm", Loc.Culture);
    }

    public static string MomentTime(string? iso)
    {
        var time = Parse(iso);
        var span = DateTime.Now - time;
        if (span.TotalMinutes < 1)
        {
            return Loc.T("Time.JustNow");
        }
        if (span.TotalHours < 1)
        {
            return Loc.T("Time.MinutesAgo", (int)span.TotalMinutes);
        }
        var today = DateTime.Today;
        if (time.Date == today)
        {
            return time.ToString("HH:mm", Loc.Culture);
        }
        if (time.Date == today.AddDays(-1))
        {
            return Loc.T("Time.Yesterday") + " " + time.ToString("HH:mm", Loc.Culture);
        }
        return En
            ? time.ToString("MMM d HH:mm", Loc.Culture)
            : time.ToString("M月d日 HH:mm", Loc.Culture);
    }
}
