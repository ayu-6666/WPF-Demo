using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tang.VideoThrimalgoAI.Models;

namespace Tang.VideoThrimalgoAI.Services
{
    /// <summary>
    /// 以隐藏窗口方式调用本地 ffmpeg：解析进度、写日志、支持取消（杀进程树）。
    /// </summary>
    public sealed class FfmpegRunner
    {
        private static readonly Regex OutTimeRegex =
            new(@"^out_time=(\d+):(\d+):(\d+\.?\d*)", RegexOptions.Compiled);

        private static readonly string LogDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Tang.VideoThrimalgoAI", "logs");

        private readonly string _ffmpegPath;

        public FfmpegRunner(string ffmpegPath)
        {
            _ffmpegPath = ffmpegPath;
        }

        public async Task<VideoRunResult> RunAsync(
            IReadOnlyList<string> args,
            string outputPath,
            double durationSeconds,
            IProgress<FfmpegProgress>? progress,
            CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false)
            };
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            Directory.CreateDirectory(LogDirectory);
            var logPath = Path.Combine(LogDirectory,
                $"ffmpeg_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");

            var tail = new Queue<string>();
            var lastReport = DateTime.MinValue;
            var lastFraction = -1.0;
            var commandLine = BuildDisplayCommand(args);

            using var process = new Process { StartInfo = startInfo };
            using var logWriter = new StreamWriter(logPath, false, new UTF8Encoding(false));
            logWriter.WriteLine($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logWriter.WriteLine($"# {commandLine}");
            logWriter.WriteLine();

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 进程可能已经退出
                }
            });

            process.Start();

            var stderrTask = Task.Run(async () =>
            {
                while (true)
                {
                    var line = await process.StandardError.ReadLineAsync().ConfigureAwait(false);
                    if (line == null)
                        break;

                    logWriter.WriteLine(line);
                    lock (tail)
                    {
                        tail.Enqueue(line);
                        while (tail.Count > 40)
                            tail.Dequeue();
                    }

                    var match = OutTimeRegex.Match(line);
                    if (!match.Success)
                        continue;

                    var position = ParsePosition(match);
                    var now = DateTime.UtcNow;
                    var fraction = durationSeconds > 0
                        ? Math.Clamp(position.TotalSeconds / durationSeconds, 0, 1)
                        : -1;

                    if (progress != null
                        && (now - lastReport).TotalMilliseconds >= 180
                        && (fraction < 0 || Math.Abs(fraction - lastFraction) >= 0.01))
                    {
                        lastReport = now;
                        lastFraction = fraction;
                        progress.Report(new FfmpegProgress
                        {
                            Fraction = fraction,
                            Position = position,
                            Message = durationSeconds > 0
                                ? $"{position:hh\\:mm\\:ss} / {TimeSpan.FromSeconds(durationSeconds):hh\\:mm\\:ss}"
                                : position.ToString(@"hh\:mm\:ss")
                        });
                    }
                }
            }, CancellationToken.None);

            var stdoutTask = Task.Run(async () =>
            {
                while (await process.StandardOutput.ReadLineAsync().ConfigureAwait(false) != null)
                {
                    // ffmpeg 正常不写 stdout，这里排空避免缓冲区阻塞
                }
            }, CancellationToken.None);

            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(stderrTask, stdoutTask).ConfigureAwait(false);
            watch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                TryDelete(outputPath);
                throw new OperationCanceledException(cancellationToken);
            }

            if (process.ExitCode != 0)
            {
                var detail = string.Join(Environment.NewLine, tail);
                throw new InvalidOperationException(
                    $"FFmpeg 执行失败（退出码 {process.ExitCode}）。{Environment.NewLine}{detail}{Environment.NewLine}日志：{logPath}");
            }

            logWriter.Flush();
            logWriter.Dispose();
            TryDelete(logPath);

            progress?.Report(new FfmpegProgress
            {
                Fraction = 1,
                Position = TimeSpan.FromSeconds(durationSeconds),
                Message = "100%"
            });

            return new VideoRunResult
            {
                OutputPath = outputPath,
                CommandLine = commandLine,
                Elapsed = watch.Elapsed
            };
        }

        private static TimeSpan ParsePosition(Match match)
        {
            var hours = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            return TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        }

        private static string BuildDisplayCommand(IReadOnlyList<string> args)
        {
            var builder = new StringBuilder("ffmpeg");
            foreach (var arg in args)
            {
                builder.Append(' ');
                builder.Append(arg.Contains(' ') || arg.Contains('"')
                    ? "\"" + arg.Replace("\"", "\\\"") + "\""
                    : arg);
            }
            return builder.ToString();
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // 删除失败不影响取消流程
            }
        }
    }
}
