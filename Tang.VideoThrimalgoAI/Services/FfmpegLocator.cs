using System;
using System.IO;
using System.Windows;

namespace Tang.VideoThrimalgoAI.Services
{
    /// <summary>
    /// 中英文切换：在应用级资源里替换视频页词条字典。
    /// 视频页所有静态文案使用 DynamicResource，因此切换后立即生效。
    /// </summary>
    public static class LocalizationManager
    {
        public const string Chinese = "zh-CN";
        public const string English = "en-US";

        private const string DictionaryMarker = "Strings.";

        private static string _current = Chinese;

        public static string Current => _current;

        public static bool IsChinese => _current == Chinese;

        public static event Action? LanguageChanged;

        public static void Toggle() => Set(IsChinese ? English : Chinese);

        public static void Set(string culture)
        {
            if (culture != Chinese && culture != English)
                return;
            if (_current == culture)
                return;

            var app = Application.Current;
            if (app != null)
            {
                ResourceDictionary? existing = null;
                foreach (var dict in app.Resources.MergedDictionaries)
                {
                    if (dict.Source != null && dict.Source.OriginalString.Contains(DictionaryMarker))
                    {
                        existing = dict;
                        break;
                    }
                }

                var replaced = new ResourceDictionary
                {
                    Source = new Uri($"Resources/Strings.{culture}.xaml", UriKind.Relative)
                };

                if (existing != null)
                {
                    var index = app.Resources.MergedDictionaries.IndexOf(existing);
                    app.Resources.MergedDictionaries[index] = replaced;
                }
                else
                {
                    app.Resources.MergedDictionaries.Add(replaced);
                }
            }

            _current = culture;
            LanguageChanged?.Invoke();
        }

        /// <summary>代码内提示文本的本地化。</summary>
        public static string T(string chinese, string english)
            => IsChinese ? chinese : english;
    }

    /// <summary>负责定位、保存并校验本地 FFmpeg 路径。</summary>
    public static class FfmpegLocator
    {
        private static readonly string SettingsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tang.VideoThrimalgoAI");
        private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "settings.json");

        /// <summary>本机默认 FFmpeg 目录（可在界面改配，配置保存在 settings.json）。</summary>
        public const string DefaultBinDirectory = @"D:\tang\software\ffmpeg\bin";

        public static string? LoadSavedFfmpegPath()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                    return null;
                var json = File.ReadAllText(SettingsFile);
                var settings = System.Text.Json.JsonSerializer.Deserialize<FfmpegSettings>(json);
                return string.IsNullOrWhiteSpace(settings?.FfmpegPath) ? null : settings!.FfmpegPath;
            }
            catch
            {
                return null;
            }
        }

        public static void SaveFfmpegPath(string path)
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = System.Text.Json.JsonSerializer.Serialize(new FfmpegSettings { FfmpegPath = path });
                File.WriteAllText(SettingsFile, json);
            }
            catch
            {
                // 配置写入失败不影响本次运行
            }
        }

        /// <summary>按“已保存配置 → 本机默认目录 → PATH → 常见安装位置”的顺序查找。</summary>
        public static string? FindFfmpeg()
        {
            var saved = LoadSavedFfmpegPath();
            if (saved != null && File.Exists(saved))
                return saved;

            var defaultPath = Path.Combine(DefaultBinDirectory, "ffmpeg.exe");
            if (File.Exists(defaultPath))
                return defaultPath;

            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathVariable.Split(';'))
            {
                var candidate = Path.Combine(dir.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            foreach (var dir in new[]
                     {
                         @"C:\ffmpeg\bin", @"C:\ffmpeg", @"C:\Program Files\ffmpeg\bin",
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"scoop\shims"),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"scoop\apps\ffmpeg\current\bin"),
                         @"C:\ProgramData\chocolatey\bin"
                     })
            {
                var candidate = Path.Combine(dir, "ffmpeg.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        public static string? FindFfprobe(string? ffmpegPath)
        {
            if (ffmpegPath != null)
            {
                var sibling = Path.Combine(Path.GetDirectoryName(ffmpegPath)!, "ffprobe.exe");
                if (File.Exists(sibling))
                    return sibling;
            }

            var ffprobe = FindFfmpeg();
            if (ffprobe != null)
            {
                var sibling = Path.Combine(Path.GetDirectoryName(ffprobe)!, "ffprobe.exe");
                if (File.Exists(sibling))
                    return sibling;
            }

            return null;
        }

        /// <summary>执行 ffmpeg -version，返回首行版本信息；失败返回 null。</summary>
        public static string? ReadVersion(string ffmpegPath)
        {
            try
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                var first = process.StandardOutput.ReadLine();
                process.WaitForExit(5000);
                return string.IsNullOrWhiteSpace(first) ? null : first.Trim();
            }
            catch
            {
                return null;
            }
        }

        private sealed class FfmpegSettings
        {
            public string? FfmpegPath { get; set; }
        }
    }
}
