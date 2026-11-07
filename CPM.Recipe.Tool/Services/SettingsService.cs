using System;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CPM.Recipe.Tool.Services
{
    /// <summary>用户偏好（主题 / 语言 / 输出目录），保存到 %LOCALAPPDATA%\CPM.Recipe.Tool\settings.json。</summary>
    public sealed class AppSettings
    {
        public string Theme { get; set; } = "dark";
        public string Language { get; set; } = "zh-CN";

        /// <summary>用户选择的迁移输出目录；为空时按默认规则推导。</summary>
        public string OutputRoot { get; set; }

        /// <summary>EC Configuration 页签使用的 ChamberConfig 目录（可通过 --chamber-root 覆盖）。</summary>
        public string ChamberRoot { get; set; }
    }

    public static class SettingsService
    {
        private static string SettingsDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CPM.Recipe.Tool");

        private static string SettingsFile => Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var obj = JObject.Parse(File.ReadAllText(SettingsFile));
                    return new AppSettings
                    {
                        Theme = (obj["Theme"]?.ToString() ?? "dark"),
                        Language = (obj["Language"]?.ToString() ?? "zh-CN"),
                        OutputRoot = (obj["OutputRoot"]?.ToString() ?? ""),
                        ChamberRoot = (obj["ChamberRoot"]?.ToString() ?? "")
                    };
                }
            }
            catch { /* 设置损坏时回退默认值，不影响启动 */ }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                File.WriteAllText(SettingsFile,
                    JsonConvert.SerializeObject(settings, Formatting.Indented));
            }
            catch { /* 无权限时静默忽略，不影响主流程 */ }
        }
    }

    /// <summary>运行期主题切换：DarkTheme 提供样式 + 深色色值；LightTheme 仅覆盖色值（后合并优先）。</summary>
    public static class ThemeManager
    {
        public const string Dark = "dark";
        public const string Light = "light";

        public static string Current { get; private set; } = Dark;

        public static void Apply(string theme)
        {
            var target = string.Equals(theme, Light, StringComparison.OrdinalIgnoreCase) ? Light : Dark;
            var dicts = Application.Current.Resources.MergedDictionaries;

            ResourceDictionary lightDict = null;
            foreach (var d in dicts)
            {
                var src = d.Source?.OriginalString ?? "";
                if (src.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase)) { lightDict = d; break; }
            }

            if (target == Light)
            {
                if (lightDict == null)
                    dicts.Add(new ResourceDictionary { Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative) });
            }
            else if (lightDict != null)
            {
                dicts.Remove(lightDict);
            }

            Current = target;
        }

        public static bool IsLight => Current == Light;
    }
}
