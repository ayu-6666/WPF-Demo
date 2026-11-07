using System.Diagnostics;
using System.IO;

namespace BitTomrrorApp.Services;

public sealed class FfmpegLocator
{
    public string Path { get; private set; } = "";
    public bool Available { get; private set; }
    public string Version { get; private set; } = "";
    public string StatusText => Available
        ? $"FFmpeg 可用（{Version}）"
        : "FFmpeg 不可用（M3U8 将使用内置拼接）";

    public async Task CheckAsync(string configuredPath)
    {
        Available = false;
        Version = "";
        Path = "";

        var candidate = ResolveCandidate(configuredPath);
        if (candidate == null)
            return;

        Path = candidate;
        try
        {
            var psi = new ProcessStartInfo(candidate, "-version")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process == null) return;
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                return;
            }

            var text = (await outputTask) + (await errorTask);
            var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
            var index = firstLine.IndexOf("ffmpeg version", StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var rest = firstLine[(index + "ffmpeg version".Length)..].Trim();
                Version = rest.Split(' ')[0];
            }
            Available = process.ExitCode == 0;
        }
        catch
        {
            Available = false;
        }
    }

    private static string ResolveCandidate(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var expanded = configuredPath.Trim().Trim('"');
            if (File.Exists(expanded)) return expanded;
            var exe = System.IO.Path.Combine(expanded, "ffmpeg.exe");
            if (File.Exists(exe)) return exe;
        }

        var env = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var exe = System.IO.Path.Combine(env, "ffmpeg.exe");
            if (File.Exists(exe)) return exe;
            if (File.Exists(env)) return env;
        }

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var exe = System.IO.Path.Combine(dir.Trim(), "ffmpeg.exe");
            if (File.Exists(exe)) return exe;
        }

        return null;
    }
}
