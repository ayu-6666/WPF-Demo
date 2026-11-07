using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MediaStudioWpf48.Services
{
    public class FfmpegService
    {
        public string Exe { get; private set; }
        public event Action<string> Log;

        public FfmpegService()
        {
            Exe = ResolveFfmpegPath();
        }

        public static string ResolveFfmpegPath()
        {
            string configured = Environment.GetEnvironmentVariable("FFMPEG_PATH", EnvironmentVariableTarget.Process);
            if (string.IsNullOrWhiteSpace(configured))
                configured = Environment.GetEnvironmentVariable("FFMPEG_PATH", EnvironmentVariableTarget.User);
            if (string.IsNullOrWhiteSpace(configured))
                configured = Environment.GetEnvironmentVariable("FFMPEG_PATH", EnvironmentVariableTarget.Machine);

            string found = ResolveCandidate(configured);
            if (!string.IsNullOrEmpty(found)) return found;

            found = ResolveCandidate(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"));
            if (!string.IsNullOrEmpty(found)) return found;

            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string folder in pathValue.Split(Path.PathSeparator))
            {
                found = ResolveCandidate(Path.Combine(folder.Trim().Trim('"'), "ffmpeg.exe"));
                if (!string.IsNullOrEmpty(found)) return found;
            }
            return null;
        }

        private static string ResolveCandidate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
            try
            {
                if (Directory.Exists(value))
                {
                    string exe = Path.Combine(value, "ffmpeg.exe");
                    return File.Exists(exe) ? Path.GetFullPath(exe) : null;
                }
                if (File.Exists(value))
                {
                    if (string.Equals(Path.GetFileName(value), "ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                        return Path.GetFullPath(value);
                    if (string.Equals(Path.GetFileName(value), "ffprobe.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string sibling = Path.Combine(Path.GetDirectoryName(value), "ffmpeg.exe");
                        return File.Exists(sibling) ? Path.GetFullPath(sibling) : null;
                    }
                }
            }
            catch { }
            return null;
        }

        public async Task Run(string args)
        {
            // Re-resolve each run so environment changes made after app launch can be picked up.
            Exe = ResolveFfmpegPath();
            if (string.IsNullOrEmpty(Exe) || !File.Exists(Exe))
                throw new FileNotFoundException(
                    "找不到 FFmpeg。请将环境变量 FFMPEG_PATH 设置为 ffmpeg.exe 的完整路径或其所在目录；" +
                    "也可以将 ffmpeg.exe 放在程序目录或加入 PATH。当前 FFMPEG_PATH=" +
                    (Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "(未设置)"));

            var startInfo = new ProcessStartInfo
            {
                FileName = Exe,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(Exe),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var process = new Process { StartInfo = startInfo })
            {
                var error = new StringBuilder();
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null) { lock (error) error.AppendLine(e.Data); Log?.Invoke(e.Data); }
                };
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null) Log?.Invoke(e.Data);
                };
                try
                {
                    if (!process.Start()) throw new InvalidOperationException("无法启动 FFmpeg 进程。");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("启动 FFmpeg 失败：" + Exe + Environment.NewLine + ex.Message, ex);
                }
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                await Task.Run(() => process.WaitForExit());
                if (process.ExitCode != 0)
                    throw new Exception("FFmpeg 执行失败，退出码：" + process.ExitCode + Environment.NewLine + error);
            }
        }
    }
}