using System.Windows;
using System.Windows.Media;

namespace MediaStudioWpf48.Services
{
    public static class ThemeManager
    {
        public static bool IsDark { get; private set; }

        public static void Apply(bool dark)
        {
            IsDark = dark;
            var r = Application.Current.Resources;
            Set(r, "WindowBg", dark ? "#101827" : "#F3F6FB");
            Set(r, "PanelBg", dark ? "#1A273A" : "#FFFFFF");
            Set(r, "PanelAltBg", dark ? "#152234" : "#F7F9FD");
            Set(r, "SidebarBg", dark ? "#1A273A" : "#FFFFFF");
            Set(r, "TitleBg", dark ? "#0B1320" : "#FFFFFF");
            Set(r, "FooterBg", dark ? "#0B1320" : "#E9EEF6");
            Set(r, "TextPrimary", dark ? "#F0F4FA" : "#172338");
            Set(r, "TextSecondary", dark ? "#A8B6C9" : "#53647D");
            Set(r, "TextTertiary", dark ? "#72859C" : "#8494AB");
            Set(r, "ButtonText", "#FFFFFF");
            Set(r, "Accent", dark ? "#6D96FF" : "#245FE5");
            Set(r, "SecondaryBg", dark ? "#1E2C42" : "#E8EFFC");
            Set(r, "SecondaryText", dark ? "#BFD2FF" : "#2051B4");
            Set(r, "SecondaryHoverOverlay", dark ? "#336D96FF" : "#2E245FE5");
            Set(r, "Danger", dark ? "#E26875" : "#C44755");
            Set(r, "DangerSoft", dark ? "#3A2028" : "#FBEAEC");
            Set(r, "LineColor", dark ? "#26364B" : "#DCE4F0");
            Set(r, "BorderStrong", dark ? "#40516A" : "#C8D3E2");
            Set(r, "InputBg", dark ? "#111C2D" : "#FFFFFF");
            Set(r, "InputBorder", dark ? "#40516A" : "#C8D3E2");
            Set(r, "InputHoverBorder", dark ? "#5C7291" : "#9FB2CC");
            Set(r, "FocusRing", dark ? "#6D96FF" : "#245FE5");
            Set(r, "RowHover", dark ? "#22314A" : "#E9F1FC");
            Set(r, "RowSelected", dark ? "#263750" : "#DDE8FA");
            Set(r, "RowSelectedBar", dark ? "#6D96FF" : "#245FE5");
            Set(r, "RowAltBg", dark ? "#17273B" : "#F8FAFD");
            Set(r, "ScrollThumb", dark ? "#3B4C63" : "#C3CFDF");
            Set(r, "ScrollThumbHover", dark ? "#54687F" : "#9FB0C6");
            Set(r, "ScrollTrack", dark ? "#1B2738" : "#EAEFF7");
            Set(r, "ProgressTrack", dark ? "#26364B" : "#E4EAF3");
            Set(r, "Success", dark ? "#4CC38A" : "#1F9D6A");
            Set(r, "LogBg", dark ? "#0F1A29" : "#F7F9FC");
            Set(r, "StatusChipBg", dark ? "#1E2C42" : "#EAF1FE");
            Set(r, "HoverOverlay", dark ? "#1FFFFFFF" : "#1A000000");
            Set(r, "PressedOverlay", dark ? "#3DFFFFFF" : "#33000000");
            Set(r, "DisabledBg", dark ? "#1B2635" : "#E6EBF2");
            Set(r, "DisabledText", dark ? "#66788D" : "#A3AFBE");
        }

        private static void Set(ResourceDictionary r, string key, string hex)
        {
            r[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
