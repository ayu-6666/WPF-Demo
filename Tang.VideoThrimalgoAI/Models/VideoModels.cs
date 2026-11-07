using System;

namespace Tang.VideoThrimalgoAI.Models
{
    /// <summary>ffprobe 解析出的媒体信息。</summary>
    public sealed class VideoInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public double Fps { get; set; }
        public double DurationSeconds { get; set; }
        public bool HasAudio { get; set; }
        public string VideoCodec { get; set; } = string.Empty;
        public string AudioCodec { get; set; } = string.Empty;

        public bool HasVideo => Width > 0 && Height > 0;
    }

    /// <summary>ffmpeg 进度回调（在后台线程触发）。</summary>
    public sealed class FfmpegProgress
    {
        public double Fraction { get; set; }
        public TimeSpan Position { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>水印区域（源视频像素坐标）。</summary>
    public sealed class WatermarkRegion
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public override string ToString() => $"{X},{Y} · {Width}×{Height}";
    }

    public enum ComposeLayout
    {
        SideBySide,
        PictureInPicture
    }

    public enum PipCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public enum AudioOutputFormat
    {
        Mp3,
        Aac,
        Wav
    }

    public sealed class EnhanceOptions
    {
        public double Scale { get; set; } = 2.0;
        public int Denoise { get; set; } = 40;
        public int Sharpen { get; set; } = 40;
    }

    public sealed class MosaicOptions
    {
        public int Denoise { get; set; } = 60;
        public int Sharpen { get; set; } = 30;
        public int TemporalFrames { get; set; } = 3;
    }

    public sealed class WatermarkOptions
    {
        public WatermarkRegion[] Regions { get; set; } = Array.Empty<WatermarkRegion>();
    }

    public sealed class ComposeOptions
    {
        public ComposeLayout Layout { get; set; } = ComposeLayout.SideBySide;
        public PipCorner PipCorner { get; set; } = PipCorner.BottomRight;
        public double PipScale { get; set; } = 0.32;
    }

    public sealed class AudioOptions
    {
        public AudioOutputFormat Format { get; set; } = AudioOutputFormat.Mp3;
        public int BitrateKbps { get; set; } = 192;
    }

    public sealed class TrimOptions
    {
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public bool FastKeyframeCopy { get; set; } = true;
    }

    public sealed class GifOptions
    {
        public int Fps { get; set; } = 12;
        public int Width { get; set; } = 480;
        public bool LoopForever { get; set; } = true;
        public string Dither { get; set; } = "sierra2_4a";
    }

    /// <summary>一次 ffmpeg 执行的结果。</summary>
    public sealed class VideoRunResult
    {
        public string OutputPath { get; set; } = string.Empty;
        public string CommandLine { get; set; } = string.Empty;
        public TimeSpan Elapsed { get; set; }
        public string Note { get; set; } = string.Empty;
    }
}
