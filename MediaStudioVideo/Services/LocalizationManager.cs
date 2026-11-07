using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Markup;
using System.Windows.Data;

namespace MediaStudioWpf48.Services
{
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        public static LocalizationManager Instance { get; } = new LocalizationManager();
        private string _language;
        private readonly Dictionary<string, Dictionary<string, string>> _text =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["zh-CN"] = new Dictionary<string, string>
            {
                ["AppTitle"] = "Media Studio · 多媒体工作台",
                ["Tools"] = "工具箱",
                ["Engine"] = "本地 FFmpeg",
                ["EngineReady"] = "FFmpeg 已就绪",
                ["EngineMissing"] = "未找到 FFmpeg",
                ["NoTools"] = "暂无可用工具",
                ["Language"] = "En",
                ["Theme"] = "Dark",
                ["Minimize"] = "最小化",
                ["Maximize"] = "最大化",
                ["Restore"] = "还原",
                ["Close"] = "关闭",
                ["Footer"] = ".NET Framework 4.8  ·  MVVM  ·  FFmpeg",
                ["InputOutput"] = "输入与输出",
                ["InputFile"] = "输入文件 / 目录",
                ["MusicFile"] = "背景音乐",
                ["OutputFile"] = "输出文件",
                ["InputHintFile"] = "选择或粘贴媒体文件路径",
                ["InputHintFolder"] = "选择或粘贴素材文件夹路径",
                ["MusicHint"] = "选择背景音乐文件（MP3 / WAV / AAC）",
                ["OutputHint"] = "选择或粘贴输出文件路径",
                ["SelectInput"] = "选择输入",
                ["BackgroundMusic"] = "背景音乐",
                ["SelectOutput"] = "选择输出",
                ["Parameters"] = "参数配置",
                ["TrimStart"] = "剪辑开始时间",
                ["Duration"] = "剪辑时长（秒）",
                ["Fps"] = "帧率 / 图片帧率",
                ["RegionX"] = "区域 X",
                ["RegionY"] = "区域 Y",
                ["RegionW"] = "区域宽度",
                ["RegionH"] = "区域高度",
                ["RotateFilter"] = "旋转 / 镜像方式",
                ["NoParams"] = "当前工具无需额外参数，选择输入与输出后即可开始处理。",
                ["RunLog"] = "执行任务 / 日志",
                ["Run"] = "开始处理",
                ["LogHint"] = "FFmpeg 输出日志将显示在这里",
                ["OpenOutputAfter"] = "完成后打开输出目录",
                ["LogLevel"] = "日志：",
                ["LogVerbose"] = "完整",
                ["LogSummary"] = "摘要",
                ["StatusReady"] = "就绪",
                ["StatusProcessing"] = "处理中",
                ["StatusDone"] = "完成",
                ["StatusFailed"] = "失败",
                ["HintBusy"] = "正在处理，请稍候…",
                ["HintNeedInput"] = "请先选择输入文件或目录",
                ["HintNeedOutput"] = "请先选择输出文件",
                ["HintNeedMusic"] = "请选择背景音乐文件",
                ["HintReady"] = "参数已就绪，可以开始处理",
                ["LogStart"] = "开始任务：",
                ["LogDone"] = "任务完成：",
                ["PickMedia"] = "媒体文件|*.mp4;*.mov;*.mkv;*.ts;*.m3u8;*.png;*.jpg;*.mp3;*.wav|所有文件|*.*",
                ["PickAudio"] = "音频|*.mp3;*.wav;*.aac;*.m4a",
                ["PickOutput"] = "输出文件|*.*|所有文件|*.*",
                ["NeedPaths"] = "请先选择输入和输出路径",
                ["NeedMusic"] = "请选择背景音乐",
                ["Completed"] = "处理完成",
                ["ErrorTitle"] = "处理失败",
                ["GroupVideo"] = "视频",
                ["GroupImage"] = "图像",
                ["ToolAudioName"] = "音乐提取",
                ["ToolAudioHelp"] = "从视频中提取 MP3 音轨",
                ["ToolMusicName"] = "添加背景音乐",
                ["ToolMusicHelp"] = "背景音乐混音并按视频时长截断",
                ["ToolConcatName"] = "视频拼接",
                ["ToolConcatHelp"] = "按文件名顺序拼接同规格视频",
                ["ToolTrimName"] = "视频剪辑",
                ["ToolTrimHelp"] = "按开始时间和时长截取片段",
                ["ToolM3u8Name"] = "M3U8 转 MP4",
                ["ToolM3u8Help"] = "下载并封装 HLS 流",
                ["ToolSlidesName"] = "文件夹图片合成视频",
                ["ToolSlidesHelp"] = "将文件夹内的 JPG 图片序列生成视频",
                ["ToolVdelogoName"] = "视频去码 / 去水印",
                ["ToolVdelogoHelp"] = "指定矩形区域进行修补",
                ["ToolVenhanceName"] = "视频画质增强",
                ["ToolVenhanceHelp"] = "降噪、Lanczos 放大与锐化",
                ["ToolGifName"] = "视频提取 GIF",
                ["ToolGifHelp"] = "调节帧率并生成调色板 GIF",
                ["ToolRotateName"] = "旋转 / 水平翻转",
                ["ToolRotateHelp"] = "旋转或水平 / 垂直镜像",
                ["ToolIdelogoName"] = "图片去码 / 去水印",
                ["ToolIdelogoHelp"] = "指定矩形区域进行修补",
                ["ToolIenhanceName"] = "图片高清增强",
                ["ToolIenhanceHelp"] = "Lanczos 放大与锐化"
            },
            ["en-US"] = new Dictionary<string, string>
            {
                ["AppTitle"] = "Media Studio · Media Workbench",
                ["Tools"] = "TOOLS",
                ["Engine"] = "Local FFmpeg",
                ["EngineReady"] = "FFmpeg ready",
                ["EngineMissing"] = "FFmpeg not found",
                ["NoTools"] = "No tools available",
                ["Language"] = "中",
                ["Theme"] = "Dark",
                ["Minimize"] = "Minimize",
                ["Maximize"] = "Maximize",
                ["Restore"] = "Restore",
                ["Close"] = "Close",
                ["Footer"] = ".NET Framework 4.8  ·  MVVM  ·  FFmpeg",
                ["InputOutput"] = "Input & Output",
                ["InputFile"] = "Input file / folder",
                ["MusicFile"] = "Background music",
                ["OutputFile"] = "Output file",
                ["InputHintFile"] = "Pick or paste a media file path",
                ["InputHintFolder"] = "Pick or paste a source folder path",
                ["MusicHint"] = "Pick a background music file (MP3 / WAV / AAC)",
                ["OutputHint"] = "Pick or paste an output file path",
                ["SelectInput"] = "Browse",
                ["BackgroundMusic"] = "Music",
                ["SelectOutput"] = "Browse",
                ["Parameters"] = "Parameters",
                ["TrimStart"] = "Trim start",
                ["Duration"] = "Duration (seconds)",
                ["Fps"] = "FPS / Image FPS",
                ["RegionX"] = "Region X",
                ["RegionY"] = "Region Y",
                ["RegionW"] = "Region width",
                ["RegionH"] = "Region height",
                ["RotateFilter"] = "Rotate / mirror",
                ["NoParams"] = "This tool needs no extra parameters. Pick input and output to start.",
                ["RunLog"] = "TASK / LOG",
                ["Run"] = "Start Processing",
                ["LogHint"] = "FFmpeg output will appear here",
                ["OpenOutputAfter"] = "Open output folder when done",
                ["LogLevel"] = "Log:",
                ["LogVerbose"] = "Full",
                ["LogSummary"] = "Summary",
                ["StatusReady"] = "Ready",
                ["StatusProcessing"] = "Processing",
                ["StatusDone"] = "Completed",
                ["StatusFailed"] = "Failed",
                ["HintBusy"] = "Processing, please wait…",
                ["HintNeedInput"] = "Select an input file or folder first",
                ["HintNeedOutput"] = "Select an output file first",
                ["HintNeedMusic"] = "Select a background music file",
                ["HintReady"] = "Ready to start processing",
                ["LogStart"] = "Started: ",
                ["LogDone"] = "Completed: ",
                ["PickMedia"] = "Media files|*.mp4;*.mov;*.mkv;*.ts;*.m3u8;*.png;*.jpg;*.mp3;*.wav|All files|*.*",
                ["PickAudio"] = "Audio|*.mp3;*.wav;*.aac;*.m4a",
                ["PickOutput"] = "Output files|*.*|All files|*.*",
                ["NeedPaths"] = "Select both input and output paths first.",
                ["NeedMusic"] = "Select a background music file.",
                ["Completed"] = "Processing completed.",
                ["ErrorTitle"] = "Processing failed",
                ["GroupVideo"] = "VIDEO",
                ["GroupImage"] = "IMAGE",
                ["ToolAudioName"] = "Extract Audio",
                ["ToolAudioHelp"] = "Extract an MP3 audio track from a video",
                ["ToolMusicName"] = "Add Background Music",
                ["ToolMusicHelp"] = "Mix background music and trim to the video length",
                ["ToolConcatName"] = "Concatenate Videos",
                ["ToolConcatHelp"] = "Join compatible videos in file-name order",
                ["ToolTrimName"] = "Trim Video",
                ["ToolTrimHelp"] = "Cut a clip by start time and duration",
                ["ToolM3u8Name"] = "M3U8 to MP4",
                ["ToolM3u8Help"] = "Download and remux an HLS stream",
                ["ToolSlidesName"] = "Images to Video",
                ["ToolSlidesHelp"] = "Build a video from a folder of JPG images",
                ["ToolVdelogoName"] = "Video Logo Removal",
                ["ToolVdelogoHelp"] = "Repair a rectangular region",
                ["ToolVenhanceName"] = "Video Enhance",
                ["ToolVenhanceHelp"] = "Denoise, Lanczos upscale and sharpen",
                ["ToolGifName"] = "Video to GIF",
                ["ToolGifHelp"] = "Adjust frame rate and generate a palette GIF",
                ["ToolRotateName"] = "Rotate / Mirror",
                ["ToolRotateHelp"] = "Rotate or mirror horizontally / vertically",
                ["ToolIdelogoName"] = "Image Logo Removal",
                ["ToolIdelogoHelp"] = "Repair a rectangular region",
                ["ToolIenhanceName"] = "Image Upscale",
                ["ToolIenhanceHelp"] = "Lanczos upscale and sharpen"
            }
        };

        private LocalizationManager()
        {
            var region = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            _language = region == "zh" ? "zh-CN" : "en-US";
        }

        public string Language => _language;
        public string this[string key] => _text[_language].TryGetValue(key, out var value) ? value : key;
        public event PropertyChangedEventHandler PropertyChanged;

        public void SetLanguage(string language)
        {
            if (language != "zh-CN" && language != "en-US") return;
            if (_language == language) return;
            _language = language;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }

        public void ToggleLanguage() => SetLanguage(_language == "zh-CN" ? "en-US" : "zh-CN");
    }

    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }
        public LocExtension(string key) { Key = key; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding("[" + Key + "]")
            {
                Source = LocalizationManager.Instance,
                Mode = BindingMode.OneWay,
                FallbackValue = Key,
                TargetNullValue = Key
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
