using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tang.VideoThrimalgoAI.Models;

namespace Tang.VideoThrimalgoAI.Services
{
    /// <summary>
    /// 所有视频功能都以本地 ffmpeg/ffprobe 命令实现，不访问网络。
    /// </summary>
    public sealed class VideoJobService
    {
        private readonly FfmpegRunner _runner;
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;

        public VideoJobService(string ffmpegPath, string ffprobePath)
        {
            _ffmpegPath = ffmpegPath;
            _ffprobePath = ffprobePath;
            _runner = new FfmpegRunner(ffmpegPath);
        }

        /// <summary>读取媒体信息（ffprobe）。</summary>
        public async Task<VideoInfo> ProbeAsync(string inputPath, CancellationToken cancellationToken)
        {
            var args = new[]
            {
                "-v", "error", "-print_format", "json",
                "-show_format", "-show_streams", inputPath
            };

            var json = await RunCaptureAsync(_ffprobePath, args, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var info = new VideoInfo();

            if (root.TryGetProperty("format", out var format))
            {
                if (format.TryGetProperty("duration", out var durationElement))
                    info.DurationSeconds = ParseDouble(durationElement.GetString());

                if (format.TryGetProperty("size", out var sizeElement))
                {
                    // 仅用于界面展示时可选，这里不处理
                }
            }

            if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    var type = stream.TryGetProperty("codec_type", out var typeElement)
                        ? typeElement.GetString()
                        : null;

                    if (type == "video" && !info.HasVideo)
                    {
                        info.Width = stream.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                        info.Height = stream.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                        info.VideoCodec = stream.TryGetProperty("codec_name", out var codec)
                            ? codec.GetString() ?? string.Empty
                            : string.Empty;
                        info.Fps = ParseFps(stream);
                        if (info.DurationSeconds <= 0 && stream.TryGetProperty("duration", out var sd))
                            info.DurationSeconds = ParseDouble(sd.GetString());
                    }
                    else if (type == "audio")
                    {
                        info.HasAudio = true;
                        info.AudioCodec = stream.TryGetProperty("codec_name", out var ac)
                            ? ac.GetString() ?? string.Empty
                            : string.Empty;
                    }
                }
            }

            return info;
        }

        /// <summary>高清修复：Lanczos 放大 + HQDN3D 降噪 + Unsharp 锐化。</summary>
        public Task<VideoRunResult> EnhanceAsync(
            string input, string output, EnhanceOptions options,
            IProgress<FfmpegProgress>? progress, CancellationToken cancellationToken)
            => RunVideoAsync(input, output, BuildEnhanceFilters(options), progress, cancellationToken);

        /// <summary>去码：降噪 + 时域融合 + 锐化，属于近似弱化。</summary>
        public Task<VideoRunResult> RemoveMosaicAsync(
            string input, string output, MosaicOptions options,
            IProgress<FfmpegProgress>? progress, CancellationToken cancellationToken)
            => RunVideoAsync(input, output, BuildMosaicFilters(options), progress, cancellationToken);

        /// <summary>去水印：多个 delogo 区域串联，逐帧插值覆盖。</summary>
        public async Task<VideoRunResult> RemoveWatermarkAsync(
            string input, string output, WatermarkOptions options,
            IProgress<FfmpegProgress>? progress, CancellationToken cancellationToken)
        {
            var info = await ProbeAsync(input, cancellationToken).ConfigureAwait(false);
            if (!info.HasVideo)
                throw new InvalidOperationException("无法读取视频画面尺寸，去水印已中止。");

            var regions = options.Regions.Length > 0
                ? options.Regions
                : new[]
                {
                    new WatermarkRegion
                    {
                        X = (int)Math.Round(info.Width * 0.7),
                        Y = (int)Math.Round(info.Height * 0.7),
                        Width = (int)Math.Round(info.Width * 0.3),
                        Height = (int)Math.Round(info.Height * 0.3)
                    }
                };

            var filters = regions.Select(region => BuildDelogoFilter(region, info.Width, info.Height)).ToArray();
            return await RunVideoAsync(input, output, string.Join(",", filters),
                progress, cancellationToken, info.DurationSeconds, info.Fps).ConfigureAwait(false);
        }

        /// <summary>并排 / 画中画合成，以第一个视频为基准统一分辨率与帧率。</summary>
        public async Task<VideoRunResult> ComposeAsync(
            string primary, string secondary, string output, ComposeOptions options,
            IProgress<FfmpegProgress>? progress, CancellationToken cancellationToken)
        {
            var infoA = await ProbeAsync(primary, cancellationToken).ConfigureAwait(false);
            var infoB = await ProbeAsync(secondary, cancellationToken).ConfigureAwait(false);
            if (!infoA.HasVideo || !infoB.HasVideo)
                throw new InvalidOperationException("两个视频中至少有一个无法解码画面。");

            if (string.Equals(Path.GetFullPath(primary), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFullPath(secondary), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("合成输出文件不能与任一输入视频相同，避免覆盖源素材。");

            var width = MakeEven(infoA.Width);
            var height = MakeEven(infoA.Height);
            var fps = infoA.Fps > 0 ? infoA.Fps : 25;
            var duration = Math.Min(infoA.DurationSeconds, infoB.DurationSeconds);
            if (duration <= 0)
                duration = Math.Max(infoA.DurationSeconds, infoB.DurationSeconds);

            string filter;
            int outputWidth;
            int outputHeight;

            if (options.Layout == ComposeLayout.SideBySide)
            {
                outputWidth = width * 2;
                outputHeight = height;
                filter =
                    $"[0:v]scale={width}:{height}:force_original_aspect_ratio=decrease," +
                    $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps={F(fps)}[a];" +
                    $"[1:v]scale={width}:{height}:force_original_aspect_ratio=decrease," +
                    $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps={F(fps)}[b];" +
                    "[a][b]hstack=inputs=2[v]";
            }
            else
            {
                outputWidth = width;
                outputHeight = height;
                var pipWidth = Math.Max(2, MakeEven((int)Math.Round(width * options.PipScale)));
                var (x, y) = options.PipCorner switch
                {
                    PipCorner.TopLeft => ("16", "16"),
                    PipCorner.TopRight => ("W-w-16", "16"),
                    PipCorner.BottomLeft => ("16", "H-h-16"),
                    _ => ("W-w-16", "H-h-16")
                };
                filter =
                    $"[0:v]scale={width}:{height}:force_original_aspect_ratio=decrease," +
                    $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps={F(fps)}[base];" +
                    $"[1:v]scale={pipWidth}:-2:flags=lanczos,setsar=1,format=yuva420p[pip];" +
                    $"[base][pip]overlay=x={x}:y={y}:shortest=0[v]";
            }

            var args = new List<string>
            {
                "-y", "-hide_banner", "-nostdin",
                "-i", primary, "-i", secondary,
                "-filter_complex", filter,
                "-map", "[v]", "-map", "0:a:0?",
                "-c:v", "libx264", "-preset", "medium", "-crf", "18",
                "-pix_fmt", "yuv420p", "-movflags", "+faststart",
                "-c:a", "aac", "-b:a", "192k",
                "-r", F(fps)
            };
            if (duration > 0)
            {
                args.Add("-t");
                args.Add(F(duration));
            }
            args.Add(output);

            var result = await _runner.RunAsync(args, output, duration, progress, cancellationToken)
                .ConfigureAwait(false);
            result.Note = $"输出 {outputWidth}×{outputHeight} @ {fps:F2}fps";
            return result;
        }

        /// <summary>顺序拼接：先统一分辨率/帧率/像素格式，再 concat。输出静音。</summary>
        public async Task<VideoRunResult> ConcatAsync(
            IReadOnlyList<string> inputs, string output,
            IProgress<FfmpegProgress>? progress, CancellationToken cancellationToken)
        {
            if (inputs.Count < 2)
                throw new InvalidOperationException("拼接至少需要两个视频。");

            // 拼接输出不得等于任一段输入，否则会覆盖素材
            var outputFull = Path.GetFullPath(output);
            if (inputs.Any(input => string.Equals(Path.GetFullPath(input), outputFull, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("拼接输出文件不能与任一输入视频相同，避免覆盖源素材。");

            var infos = new List<VideoInfo>();
            var totalDuration = 0.0;
            foreach (var input in inputs)
            {
                var info = await ProbeAsync(input, cancellationToken).ConfigureAwait(false);
                if (!info.HasVideo)
                    throw new InvalidOperationException($"无法解码视频：{Path.GetFileName(input)}");
                infos.Add(info);
                totalDuration += info.DurationSeconds;
            }

            var width = MakeEven(infos[0].Width);
            var height = MakeEven(infos[0].Height);
            var fps = infos[0].Fps > 0 ? infos[0].Fps : 25;

            var builder = new StringBuilder();
            for (var index = 0; index < inputs.Count; index++)
            {
                builder.Append(
                    $"[{index}:v]scale={width}:{height}:force_original_aspect_ratio=decrease," +
                    $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1," +
                    $"fps={F(fps)},format=yuv420p[v{index}];");
            }

            for (var index = 0; index < inputs.Count; index++)
                builder.Append($"[v{index}]");
            builder.Append($"concat=n={inputs.Count}:v=1:a=0[v]");

            var args = new List<string>
            {
                "-y", "-hide_banner", "-nostdin"
            };
            foreach (var input in inputs)
            {
                args.Add("-i");
                args.Add(input);
            }

            args.AddRange(new[]
            {
                "-filter_complex", builder.ToString(),
                "-map", "[v]",
                "-c:v", "libx264", "-preset", "medium", "-crf", "18",
                "-pix_fmt", "yuv420p", "-movflags", "+faststart",
                "-an", output
            });

            var result = await _runner.RunAsync(args, output, totalDuration, progress, cancellationToken)
                .ConfigureAwait(false);
            result.Note = $"拼接 {inputs.Count} 段，输出 {width}×{height} @ {fps:F2}fps（静音）";
            return result;
        }

        /// <summary>本地 M3U8 → MP4：先尝试无损转封装，失败后自动重编码。</summary>
        public async Task<VideoRunResult> ConvertM3u8Async(
            string input, string output,
            IProgress<FfmpegProgress>? progress, CancellationToken cancellationToken)
        {
            if (!input.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请选择本地 .m3u8 播放列表。");

            EnsureOutputIsDistinct(input, output);

            var duration = 0.0;
            try
            {
                var info = await ProbeAsync(input, cancellationToken).ConfigureAwait(false);
                duration = info.DurationSeconds;
            }
            catch
            {
                // 部分 HLS 无法在 ffprobe 阶段得到时长，进度改为不定长
            }

            var note = BuildHlsNote(input);

            var copyArgs = new[]
            {
                "-y", "-hide_banner", "-nostdin", "-allowed_extensions", "ALL",
                "-i", input,
                "-map", "0",
                "-c", "copy",
                "-movflags", "+faststart",
                output
            };

            VideoRunResult result;
            try
            {
                result = await _runner.RunAsync(copyArgs, output, duration, progress, cancellationToken)
                    .ConfigureAwait(false);
                result.Note = note + "（无损转封装）";
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 转封装失败（容器/编码不兼容）时回退重编码
                var transcodeArgs = new[]
                {
                    "-y", "-hide_banner", "-nostdin", "-allowed_extensions", "ALL",
                    "-i", input,
                    "-c:v", "libx264", "-preset", "medium", "-crf", "20",
                    "-pix_fmt", "yuv420p", "-movflags", "+faststart",
                    "-c:a", "aac", "-b:a", "192k",
                    output
                };
                result = await _runner.RunAsync(transcodeArgs, output, duration, progress, cancellationToken)
                    .ConfigureAwait(false);
                result.Note = note + $"（转封装失败后已重编码：{ex.Message.Split('\n')[0]}）";
                return result;
            }
        }

        /// <summary>提取音轨为独立音频文件。</summary>
        public async Task<VideoRunResult> ExtractAudioAsync(
            string input, string output, AudioOptions options,
            IProgress<FfmpegProgress>? progress, CancellationToken cancellationToken)
        {
            EnsureOutputIsDistinct(input, output);

            var info = await ProbeAsync(input, cancellationToken).ConfigureAwait(false);
            if (!info.HasAudio)
                throw new InvalidOperationException("源视频不包含音轨，无法提取音频。");

            var args = new List<string>
            {
                "-y", "-hide_banner", "-nostdin",
                "-i", input,
                "-vn", "-map", "0:a:0"
            };

            switch (options.Format)
            {
                case AudioOutputFormat.Mp3:
                    args.AddRange(new[] { "-c:a", "libmp3lame", "-b:a", $"{options.BitrateKbps}k" });
                    break;
                case AudioOutputFormat.Aac:
                    args.AddRange(new[] { "-c:a", "aac", "-b:a", $"{options.BitrateKbps}k" });
                    break;
                default:
                    args.AddRange(new[] { "-c:a", "pcm_s16le" });
                    break;
            }

            args.Add(output);
            return await _runner.RunAsync(args, output, info.DurationSeconds, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>按起止时间导出片段，支持快速（关键帧）与精确（重编码）两种模式。</summary>
        public async Task<VideoRunResult> TrimAsync(
            string input, string output, TrimOptions options,
            IProgress<FfmpegProgress>? progress, CancellationToken cancellationToken)
        {
            EnsureOutputIsDistinct(input, output);

            var info = await ProbeAsync(input, cancellationToken).ConfigureAwait(false);
            var total = info.DurationSeconds > 0 ? info.DurationSeconds : options.EndSeconds;
            var start = Math.Max(0, options.StartSeconds);
            var end = options.EndSeconds > start ? options.EndSeconds : total;
            var clipLength = Math.Max(0.1, end - start);

            var args = new List<string>
            {
                "-y", "-hide_banner", "-nostdin",
                "-ss", F(start),
                "-i", input,
                "-t", F(clipLength)
            };

            if (options.FastKeyframeCopy)
            {
                args.AddRange(new[] { "-c", "copy", "-movflags", "+faststart" });
            }
            else
            {
                args.AddRange(new[]
                {
                    "-c:v", "libx264", "-preset", "medium", "-crf", "18",
                    "-pix_fmt", "yuv420p", "-movflags", "+faststart",
                    "-c:a", "aac", "-b:a", "192k"
                });
            }

            args.Add(output);
            var result = await _runner.RunAsync(args, output, clipLength, progress, cancellationToken)
                .ConfigureAwait(false);
            result.Note = options.FastKeyframeCopy
                ? $"快速模式：起点对齐到关键帧，片段 {clipLength:F2}s"
                : $"精确模式：重编码，片段 {clipLength:F2}s";
            return result;
        }

        /// <summary>高质量 GIF：palettegen/paletteuse + 抖动。</summary>
        public async Task<VideoRunResult> CreateGifAsync(
            string input, string output, GifOptions options,
            IProgress<FfmpegProgress>? progress, CancellationToken cancellationToken)
        {
            EnsureOutputIsDistinct(input, output);

            var info = await ProbeAsync(input, cancellationToken).ConfigureAwait(false);
            var fps = Math.Clamp(options.Fps, 1, 50);
            var width = Math.Clamp(options.Width, 64, 1920);
            var dither = string.IsNullOrWhiteSpace(options.Dither) ? "sierra2_4a" : options.Dither;
            var loop = options.LoopForever ? "0" : "1";

            var filter =
                $"fps={fps},scale={width}:-2:flags=lanczos," +
                "split[s0][s1];" +
                "[s0]palettegen=max_colors=256:stats_mode=full:reserve_transparent=0[p];" +
                $"[s1][p]paletteuse=dither={dither}:diff_mode=rectangle";

            var args = new[]
            {
                "-y", "-hide_banner", "-nostdin",
                "-i", input,
                "-an", "-vf", filter, "-loop", loop, output
            };

            var result = await _runner.RunAsync(args, output, info.DurationSeconds, progress, cancellationToken)
                .ConfigureAwait(false);
            result.Note = $"GIF {width}px 宽 @ {fps}fps（256 色量化 + {dither}）";
            return result;
        }

        // ---------------------------------------------------------------- helpers

        private async Task<VideoRunResult> RunVideoAsync(
            string input, string output, string videoFilter,
            IProgress<FfmpegProgress>? progress, CancellationToken cancellationToken,
            double? knownDuration = null, double? knownFps = null)
        {
            var duration = knownDuration ?? 0;
            if (duration <= 0)
            {
                var info = await ProbeAsync(input, cancellationToken).ConfigureAwait(false);
                duration = info.DurationSeconds;
            }

            // 防止输出路径与输入相同导致源素材被覆盖
            EnsureOutputIsDistinct(input, output);

            var args = new List<string>
            {
                "-y", "-hide_banner", "-nostdin",
                "-i", input
            };
            if (!string.IsNullOrWhiteSpace(videoFilter))
            {
                args.Add("-vf");
                args.Add(videoFilter);
            }

            args.AddRange(new[]
            {
                "-map", "0:v:0", "-map", "0:a:0?",
                "-c:v", "libx264", "-preset", "medium", "-crf", "18",
                "-pix_fmt", "yuv420p", "-movflags", "+faststart",
                "-c:a", "aac", "-b:a", "192k"
            });
            args.Add(output);

            return await _runner.RunAsync(args, output, duration, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        private static void EnsureOutputIsDistinct(string input, string output)
        {
            if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("输出文件不能与输入视频相同，避免覆盖源素材。");
        }

        private static string BuildEnhanceFilters(EnhanceOptions options)
        {
            var filters = new List<string>();
            if (options.Denoise > 0)
                filters.Add(BuildHqdn3d(options.Denoise));

            if (options.Scale > 1.01)
            {
                var scale = F(options.Scale);
                filters.Add($"scale=trunc(iw*{scale}/2)*2:trunc(ih*{scale}/2)*2:flags=lanczos");
            }

            if (options.Sharpen > 0)
            {
                var amount = F(options.Sharpen / 100.0 * 1.5);
                filters.Add($"unsharp=5:5:{amount}:5:5:0");
            }

            return string.Join(",", filters);
        }

        private static string BuildMosaicFilters(MosaicOptions options)
        {
            var filters = new List<string>();
            if (options.Denoise > 0)
                filters.Add(BuildHqdn3d(options.Denoise));

            if (options.TemporalFrames > 1)
                filters.Add($"tmix=frames={Math.Clamp(options.TemporalFrames, 2, 5)}");

            if (options.Sharpen > 0)
            {
                var amount = F(options.Sharpen / 100.0 * 1.2);
                filters.Add($"unsharp=5:5:{amount}:5:5:0");
            }

            return string.Join(",", filters);
        }

        private static string BuildHqdn3d(int denoise)
        {
            var level = Math.Clamp(denoise, 0, 100) / 100.0;
            var lumaSpatial = 1 + 4 * level;
            var chromaSpatial = 1 + 2 * level;
            var lumaTemporal = 2 + 6 * level;
            var chromaTemporal = 2 + 4 * level;
            return $"hqdn3d={F(lumaSpatial)}:{F(chromaSpatial)}:{F(lumaTemporal)}:{F(chromaTemporal)}";
        }

        private static string BuildDelogoFilter(WatermarkRegion region, int width, int height)
        {
            var x = Math.Clamp(region.X, 1, Math.Max(1, width - 3));
            var y = Math.Clamp(region.Y, 1, Math.Max(1, height - 3));
            var w = Math.Clamp(region.Width, 2, Math.Max(2, width - 2 - x));
            var h = Math.Clamp(region.Height, 2, Math.Max(2, height - 2 - y));
            return $"delogo=x={x}:y={y}:w={w}:h={h}:show=0";
        }

        private static string BuildHlsNote(string playlistPath)
        {
            try
            {
                var text = File.ReadAllText(playlistPath);
                foreach (var line in text.Split('\n'))
                {
                    if (!line.StartsWith("#EXT-X-KEY", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (line.Contains("METHOD=NONE", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (line.Contains("URI=\"http", StringComparison.OrdinalIgnoreCase))
                        return "检测到远程密钥加密分片，离线环境无法解密";
                    return "检测到加密分片（本地密钥可尝试解密）";
                }
            }
            catch
            {
                // 读不到播放列表时交给 ffmpeg 报错
            }
            return "本地 HLS";
        }

        private async Task<string> RunCaptureAsync(string executable, IReadOnlyList<string> args, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            };
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            using var process = new Process { StartInfo = startInfo };
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            });

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException($"ffprobe 读取失败（退出码 {process.ExitCode}）：{detail.Trim()}");
            }

            return stdout;
        }

        private static double ParseFps(JsonElement stream)
        {
            foreach (var key in new[] { "avg_frame_rate", "r_frame_rate" })
            {
                if (!stream.TryGetProperty(key, out var element))
                    continue;
                var value = element.GetString();
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                var parts = value.Split('/');
                if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
                    && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)
                    && denominator > 0)
                    return numerator / denominator;
            }
            return 0;
        }

        private static double ParseDouble(string? value)
            => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

        private static int MakeEven(int value) => value % 2 == 0 ? value : Math.Max(2, value - 1);

        private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        public string FfmpegPath => _ffmpegPath;
        public string FfprobePath => _ffprobePath;
    }
}
