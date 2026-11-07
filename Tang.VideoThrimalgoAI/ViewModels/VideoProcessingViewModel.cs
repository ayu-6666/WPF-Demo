using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Tang.VideoThrimalgoAI.Models;
using Tang.VideoThrimalgoAI.Services;

namespace Tang.VideoThrimalgoAI.ViewModels
{
    /// <summary>视频页视图模型：选择素材、参数、进度、取消与结果输出。</summary>
    public partial class VideoProcessingViewModel : ObservableObject
    {
        private const string PreviewDirectoryName = "preview";

        private readonly string _previewDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tang.VideoThrimalgoAI", PreviewDirectoryName);

        private CancellationTokenSource? _cts;
        private string? _currentPreviewFile;
        private string? _ffmpegPath;
        private string? _ffprobePath;

        public VideoProcessingViewModel()
        {
            Functions = new ObservableCollection<string>();
            LayoutOptions = new ObservableCollection<string>();
            PipPositionOptions = new ObservableCollection<string>();
            AudioFormatOptions = new ObservableCollection<string>();
            TrimModeOptions = new ObservableCollection<string>();
            DitherOptions = new ObservableCollection<string>();

            BuildLocalizedLists();

            OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            if (!Directory.Exists(OutputFolder))
                OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            OpenVideoCommand = new AsyncRelayCommand(OpenVideoAsync);
            OpenOutputFolderCommand = new AsyncRelayCommand(OpenOutputFolderAsync);
            BrowseFfmpegCommand = new RelayCommand(BrowseFfmpeg);
            RedetectFfmpegCommand = new RelayCommand(RedetectFfmpeg);
            ChooseSecondVideoCommand = new AsyncRelayCommand(ChooseSecondVideoAsync);
            AddConcatVideosCommand = new AsyncRelayCommand(AddConcatVideosAsync);
            RemoveConcatVideoCommand = new RelayCommand<string>(RemoveConcatVideo);
            ClearRegionsCommand = new RelayCommand(ClearRegions);
            RemoveRegionCommand = new RelayCommand<WatermarkRegion>(RemoveRegion);
            StartCommand = new AsyncRelayCommand(StartAsync, () => CanStart);
            CancelCommand = new RelayCommand(Cancel, () => IsBusy);
            ToggleLanguageCommand = new RelayCommand(ToggleLanguage);

            SelectedFunctionIndex = 0;
            SelectedScaleIndex = 2;
            SelectedLayoutIndex = 0;
            SelectedPipPositionIndex = 3;
            SelectedAudioFormatIndex = 0;
            SelectedTrimModeIndex = 0;
            SelectedDitherIndex = 3;
            SelectedBitrateIndex = 1;

            LocalizationManager.LanguageChanged += OnLanguageChanged;

            DetectFfmpeg();
            StatusText = T("就绪", "Ready");
        }

        // ------------------------------------------------------------ 可用性

        public ObservableCollection<string> Functions { get; }
        public ObservableCollection<string> LayoutOptions { get; }
        public ObservableCollection<string> PipPositionOptions { get; }
        public ObservableCollection<string> AudioFormatOptions { get; }
        public ObservableCollection<string> TrimModeOptions { get; }
        public ObservableCollection<string> DitherOptions { get; }
        public ObservableCollection<string> ConcatFiles { get; } = new();
        public ObservableCollection<WatermarkRegion> WatermarkRegions { get; } = new();

        public IAsyncRelayCommand OpenVideoCommand { get; }
        public IAsyncRelayCommand OpenOutputFolderCommand { get; }
        public IRelayCommand BrowseFfmpegCommand { get; }
        public IRelayCommand RedetectFfmpegCommand { get; }
        public IAsyncRelayCommand ChooseSecondVideoCommand { get; }
        public IAsyncRelayCommand AddConcatVideosCommand { get; }
        public IRelayCommand<string> RemoveConcatVideoCommand { get; }
        public IRelayCommand ClearRegionsCommand { get; }
        public IRelayCommand<WatermarkRegion> RemoveRegionCommand { get; }
        public IAsyncRelayCommand StartCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IRelayCommand ToggleLanguageCommand { get; }

        // ------------------------------------------------------------ 引擎

        private string _engineStatus = string.Empty;
        public string EngineStatus
        {
            get => _engineStatus;
            private set => SetProperty(ref _engineStatus, value);
        }

        public string? FfmpegPath
        {
            get => _ffmpegPath;
            private set
            {
                if (SetProperty(ref _ffmpegPath, value))
                {
                    OnPropertyChanged(nameof(CanStart));
                    StartCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private void DetectFfmpeg()
        {
            var found = FfmpegLocator.FindFfmpeg();
            if (found == null)
            {
                FfmpegPath = null;
                _ffprobePath = null;
                EngineStatus = T("未检测到 ffmpeg.exe，请点击“浏览”指定本地路径。",
                    "ffmpeg.exe not found. Click Browse to pick a local path.");
                return;
            }

            ApplyFfmpegPath(found, persist: false);
        }

        private void ApplyFfmpegPath(string path, bool persist)
        {
            if (!File.Exists(path))
            {
                FfmpegPath = null;
                EngineStatus = T("指定的 ffmpeg.exe 不存在。", "The selected ffmpeg.exe does not exist.");
                return;
            }

            FfmpegPath = path;
            _ffprobePath = FfmpegLocator.FindFfprobe(path);
            if (_ffprobePath == null)
            {
                EngineStatus = T("已找到 ffmpeg，但同目录缺少 ffprobe.exe。",
                    "ffmpeg was found, but ffprobe.exe is missing in the same folder.");
                return;
            }

            if (persist)
                FfmpegLocator.SaveFfmpegPath(path);

            var version = FfmpegLocator.ReadVersion(path);
            EngineStatus = version ?? T("FFmpeg 版本未知", "Unknown FFmpeg version");
        }

        private void BrowseFfmpeg()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ffmpeg.exe|ffmpeg.exe|所有文件|*.*",
                Title = T("选择本地 ffmpeg.exe", "Select local ffmpeg.exe")
            };
            if (dialog.ShowDialog(GetOwner()) != true)
                return;

            ApplyFfmpegPath(dialog.FileName, persist: true);
        }

        private void RedetectFfmpeg()
        {
            FfmpegLocator.SaveFfmpegPath(string.Empty);
            DetectFfmpeg();
        }

        // ------------------------------------------------------------ 素材与输出

        private string? _sourcePath;
        public string? SourcePath
        {
            get => _sourcePath;
            private set
            {
                if (SetProperty(ref _sourcePath, value))
                {
                    OnPropertyChanged(nameof(HasSource));
                    OnPropertyChanged(nameof(SourceFileName));
                    OnPropertyChanged(nameof(CanStart));
                    StartCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasSource => SourcePath != null;
        public string SourceFileName => SourcePath == null
            ? T("未选择视频", "No video selected")
            : Path.GetFileName(SourcePath);

        private string _outputFolder;
        public string OutputFolder
        {
            get => _outputFolder;
            private set => SetProperty(ref _outputFolder, value);
        }

        private VideoInfo? _sourceInfo;
        public VideoInfo? SourceInfo
        {
            get => _sourceInfo;
            private set
            {
                if (SetProperty(ref _sourceInfo, value))
                    RaiseMediaInfo();
            }
        }

        public string ResolutionText => SourceInfo is { HasVideo: true } info ? $"{info.Width} × {info.Height}" : "-";
        public string DurationText => SourceInfo == null || SourceInfo.DurationSeconds <= 0
            ? "-"
            : TimeSpan.FromSeconds(SourceInfo.DurationSeconds).ToString(@"hh\:mm\:ss");
        public string FpsText => SourceInfo == null || SourceInfo.Fps <= 0 ? "-" : $"{SourceInfo.Fps:F2} fps";
        public string AudioText => SourceInfo == null
            ? "-"
            : SourceInfo.HasAudio
                ? T("有", "Yes") + (string.IsNullOrWhiteSpace(SourceInfo.AudioCodec) ? string.Empty : $" ({SourceInfo.AudioCodec})")
                : T("无", "No");
        public int VideoWidth => SourceInfo?.Width ?? 0;
        public int VideoHeight => SourceInfo?.Height ?? 0;

        private ImageSource? _previewImage;
        public ImageSource? PreviewImage
        {
            get => _previewImage;
            private set
            {
                if (SetProperty(ref _previewImage, value))
                    OnPropertyChanged(nameof(IsPreviewEmpty));
            }
        }

        public bool IsPreviewEmpty => PreviewImage == null;

        private string _outputPath = string.Empty;
        public string OutputPath
        {
            get => _outputPath;
            private set => SetProperty(ref _outputPath, value);
        }

        private string _elapsedText = "-";
        public string ElapsedText
        {
            get => _elapsedText;
            private set => SetProperty(ref _elapsedText, value);
        }

        private void RaiseMediaInfo()
        {
            OnPropertyChanged(nameof(ResolutionText));
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(FpsText));
            OnPropertyChanged(nameof(AudioText));
            OnPropertyChanged(nameof(VideoWidth));
            OnPropertyChanged(nameof(VideoHeight));
            OnPropertyChanged(nameof(DurationSeconds));
            OnPropertyChanged(nameof(TrimMaximum));
        }

        private async Task OpenVideoAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.flv;*.wmv;*.ts;*.m3u8|所有文件|*.*",
                Multiselect = false,
                Title = T("选择视频文件", "Select a video file")
            };
            if (dialog.ShowDialog(GetOwner()) != true)
                return;

            await LoadVideoAsync(dialog.FileName);
        }

        public async Task LoadVideoAsync(string path)
        {
            SourcePath = path;
            PreviewImage = null;
            WatermarkRegions.Clear();
            RaiseRoiState();

            try
            {
                StatusText = T("正在读取媒体信息…", "Probing media…");
                var service = CreateService();
                var info = await service.ProbeAsync(path, CancellationToken.None);
                SourceInfo = info;

                if (!info.HasVideo)
                    throw new InvalidOperationException(T("该文件不包含可解码的视频画面。",
                        "This file has no decodable video stream."));

                StartSeconds = 0;
                EndSeconds = info.DurationSeconds;
                UpdateTrimText();

                await LoadPreviewAsync(service, path);
                StatusText = T("已加载视频", "Video loaded");
            }
            catch (Exception ex)
            {
                StatusText = T("读取失败：", "Load failed: ") + ex.Message;
                MessageBox.Show(GetOwner(), ex.Message, T("读取视频失败", "Video load failed"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadPreviewAsync(VideoJobService service, string path)
        {
            try
            {
                Directory.CreateDirectory(_previewDirectory);
                var target = Path.Combine(_previewDirectory, $"thumb_{DateTime.Now:yyyyMMddHHmmssfff}.png");
                var runner = new FfmpegRunner(service.FfmpegPath);
                await runner.RunAsync(
                    new[]
                    {
                        "-y", "-hide_banner", "-nostdin",
                        "-ss", "0.1", "-i", path,
                        "-frames:v", "1", "-vf", "scale=960:-2:flags=lanczos",
                        target
                    },
                    target, 0, null, CancellationToken.None);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(target, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                PreviewImage = bitmap;

                if (_currentPreviewFile != null && File.Exists(_currentPreviewFile))
                {
                    try { File.Delete(_currentPreviewFile); } catch { }
                }
                _currentPreviewFile = target;
            }
            catch
            {
                PreviewImage = null;
            }
        }

        private async Task OpenOutputFolderAsync()
        {
            try
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = T("选择输出目录", "Choose output folder"),
                    ShowNewFolderButton = true
                };

                var owner = GetWinFormsOwner();
                var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    OutputFolder = dialog.SelectedPath;
                    StatusText = T("输出目录已设置：", "Output folder set: ") + OutputFolder;
                }
            }
            catch (Exception ex)
            {
                StatusText = T("无法打开文件夹选择框：", "Cannot open folder dialog: ") + ex.Message;
            }

            await Task.CompletedTask;
        }

        // ------------------------------------------------------------ 第二个视频 / 拼接列表

        private string? _secondVideoPath;
        public string? SecondVideoPath
        {
            get => _secondVideoPath;
            private set
            {
                if (SetProperty(ref _secondVideoPath, value))
                {
                    OnPropertyChanged(nameof(SecondVideoName));
                    OnPropertyChanged(nameof(CanStart));
                    StartCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string SecondVideoName => SecondVideoPath == null
            ? T("未选择第二个视频", "No second video selected")
            : Path.GetFileName(SecondVideoPath);

        private async Task ChooseSecondVideoAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.flv;*.wmv;*.ts|所有文件|*.*",
                Title = T("选择第二个视频", "Select the second video")
            };
            if (dialog.ShowDialog(GetOwner()) != true)
                return;

            SecondVideoPath = dialog.FileName;
            await Task.CompletedTask;
        }

        private async Task AddConcatVideosAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.flv;*.wmv;*.ts|所有文件|*.*",
                Multiselect = true,
                Title = T("添加待拼接视频", "Add clips to concatenate")
            };
            if (dialog.ShowDialog(GetOwner()) != true)
                return;

            foreach (var file in dialog.FileNames)
            {
                if (!ConcatFiles.Contains(file))
                    ConcatFiles.Add(file);
            }

            OnPropertyChanged(nameof(ConcatSummary));
            OnPropertyChanged(nameof(CanStart));
            StartCommand.NotifyCanExecuteChanged();
            await Task.CompletedTask;
        }

        private void RemoveConcatVideo(string? file)
        {
            if (file == null)
                return;
            ConcatFiles.Remove(file);
            OnPropertyChanged(nameof(ConcatSummary));
            OnPropertyChanged(nameof(CanStart));
            StartCommand.NotifyCanExecuteChanged();
        }

        public string ConcatSummary => ConcatFiles.Count == 0
            ? T("列表为空，按顺序添加待拼接视频", "List is empty; add clips in playback order")
            : string.Format(T("{0} 个片段，按列表顺序拼接", "{0} clip(s), joined in list order"), ConcatFiles.Count);

        // ------------------------------------------------------------ 功能与参数

        private int _selectedFunctionIndex;
        public int SelectedFunctionIndex
        {
            get => _selectedFunctionIndex;
            set
            {
                if (!SetProperty(ref _selectedFunctionIndex, value))
                    return;
                RaisePanelVisibility();
                OnPropertyChanged(nameof(CanStart));
                StartCommand?.NotifyCanExecuteChanged();
            }
        }

        private JobKind CurrentJob => SelectedFunctionIndex switch
        {
            0 => JobKind.Enhance,
            1 => JobKind.Mosaic,
            2 => JobKind.Watermark,
            3 => JobKind.Compose,
            4 => JobKind.Concat,
            5 => JobKind.M3u8,
            6 => JobKind.Audio,
            7 => JobKind.Trim,
            _ => JobKind.Gif
        };

        public bool ShowEnhance => CurrentJob == JobKind.Enhance;
        public bool ShowMosaic => CurrentJob == JobKind.Mosaic;
        public bool ShowWatermark => CurrentJob == JobKind.Watermark;
        public bool ShowCompose => CurrentJob == JobKind.Compose;
        public bool ShowConcat => CurrentJob == JobKind.Concat;
        public bool ShowM3u8 => CurrentJob == JobKind.M3u8;
        public bool ShowAudio => CurrentJob == JobKind.Audio;
        public bool ShowTrim => CurrentJob == JobKind.Trim;
        public bool ShowGif => CurrentJob == JobKind.Gif;

        private void RaisePanelVisibility()
        {
            foreach (var name in new[]
                     {
                         nameof(ShowEnhance), nameof(ShowMosaic), nameof(ShowWatermark),
                         nameof(ShowCompose), nameof(ShowConcat), nameof(ShowM3u8),
                         nameof(ShowAudio), nameof(ShowTrim), nameof(ShowGif)
                     })
            {
                OnPropertyChanged(name);
            }
        }

        public ObservableCollection<string> ScaleOptions { get; } = new() { "1x", "1.5x", "2x", "3x", "4x" };

        private int _selectedScaleIndex;
        public int SelectedScaleIndex
        {
            get => _selectedScaleIndex;
            set => SetProperty(ref _selectedScaleIndex, value);
        }

        private double _denoise = 40;
        public double Denoise
        {
            get => _denoise;
            set => SetProperty(ref _denoise, value);
        }

        private double _sharpen = 40;
        public double Sharpen
        {
            get => _sharpen;
            set => SetProperty(ref _sharpen, value);
        }

        private double _temporalFrames = 3;
        public double TemporalFrames
        {
            get => _temporalFrames;
            set => SetProperty(ref _temporalFrames, value);
        }

        private int _selectedLayoutIndex;
        public int SelectedLayoutIndex
        {
            get => _selectedLayoutIndex;
            set
            {
                if (SetProperty(ref _selectedLayoutIndex, value))
                    OnPropertyChanged(nameof(IsPipLayout));
            }
        }

        public bool IsPipLayout => SelectedLayoutIndex == 1;

        private int _selectedPipPositionIndex;
        public int SelectedPipPositionIndex
        {
            get => _selectedPipPositionIndex;
            set => SetProperty(ref _selectedPipPositionIndex, value);
        }

        private double _pipScale = 32;
        public double PipScale
        {
            get => _pipScale;
            set => SetProperty(ref _pipScale, value);
        }

        private int _selectedAudioFormatIndex;
        public int SelectedAudioFormatIndex
        {
            get => _selectedAudioFormatIndex;
            set => SetProperty(ref _selectedAudioFormatIndex, value);
        }

        public ObservableCollection<string> BitrateOptions { get; } = new() { "128", "192", "256", "320" };

        private int _selectedBitrateIndex;
        public int SelectedBitrateIndex
        {
            get => _selectedBitrateIndex;
            set => SetProperty(ref _selectedBitrateIndex, value);
        }

        private int _selectedTrimModeIndex;
        public int SelectedTrimModeIndex
        {
            get => _selectedTrimModeIndex;
            set => SetProperty(ref _selectedTrimModeIndex, value);
        }

        private double _startSeconds;
        public double StartSeconds
        {
            get => _startSeconds;
            set
            {
                var clamped = Math.Max(0, value);
                if (!SetProperty(ref _startSeconds, clamped))
                    return;
                if (EndSeconds < clamped)
                    EndSeconds = clamped;
                UpdateTrimText();
            }
        }

        private double _endSeconds;
        public double EndSeconds
        {
            get => _endSeconds;
            set
            {
                if (!SetProperty(ref _endSeconds, Math.Max(0, value)))
                    return;
                UpdateTrimText();
            }
        }

        public double DurationSeconds => SourceInfo?.DurationSeconds ?? 0;
        public double TrimMaximum => Math.Max(1, DurationSeconds);
        public string StartTimeText => FormatTime(StartSeconds);
        public string EndTimeText => FormatTime(EndSeconds);
        public string ClipLengthText => FormatTime(Math.Max(0, EndSeconds - StartSeconds));

        private void UpdateTrimText()
        {
            OnPropertyChanged(nameof(StartTimeText));
            OnPropertyChanged(nameof(EndTimeText));
            OnPropertyChanged(nameof(ClipLengthText));
            OnPropertyChanged(nameof(ClipLengthSeconds));
        }

        public double ClipLengthSeconds => Math.Max(0, EndSeconds - StartSeconds);

        private double _gifFps = 12;
        public double GifFps
        {
            get => _gifFps;
            set => SetProperty(ref _gifFps, value);
        }

        private double _gifWidth = 480;
        public double GifWidth
        {
            get => _gifWidth;
            set => SetProperty(ref _gifWidth, value);
        }

        private bool _gifLoop = true;
        public bool GifLoop
        {
            get => _gifLoop;
            set => SetProperty(ref _gifLoop, value);
        }

        private int _selectedDitherIndex;
        public int SelectedDitherIndex
        {
            get => _selectedDitherIndex;
            set => SetProperty(ref _selectedDitherIndex, value);
        }

        // ------------------------------------------------------------ 水印区域

        public string RoiSummary => WatermarkRegions.Count == 0
            ? T("尚未框选区域，默认使用右下角 30% 区域", "No region selected; the bottom-right 30% area is used by default")
            : string.Format(T("已框选 {0} 个区域", "{0} region(s) selected"), WatermarkRegions.Count);

        public bool HasRegions => WatermarkRegions.Count > 0;

        public void AddRegion(WatermarkRegion region)
        {
            if (region.Width < 4 || region.Height < 4)
                return;
            WatermarkRegions.Add(region);
            RaiseRoiState();
        }

        private void RemoveRegion(WatermarkRegion? region)
        {
            if (region == null)
                return;
            WatermarkRegions.Remove(region);
            RaiseRoiState();
        }

        private void ClearRegions()
        {
            WatermarkRegions.Clear();
            RaiseRoiState();
        }

        private void RaiseRoiState()
        {
            OnPropertyChanged(nameof(RoiSummary));
            OnPropertyChanged(nameof(HasRegions));
        }

        // ------------------------------------------------------------ 进度 / 状态

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (!SetProperty(ref _isBusy, value))
                    return;
                OnPropertyChanged(nameof(CanStart));
                StartCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }
        }

        private bool _isIndeterminate = true;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            private set => SetProperty(ref _isIndeterminate, value);
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            private set => SetProperty(ref _progressValue, value);
        }

        private string _progressText = string.Empty;
        public string ProgressText
        {
            get => _progressText;
            private set => SetProperty(ref _progressText, value);
        }

        private string _statusText = string.Empty;
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public bool CanStart =>
            !IsBusy
            && _ffmpegPath != null
            && _ffprobePath != null
            && SourcePath != null
            && SourceInfo is { HasVideo: true }
            && (CurrentJob switch
            {
                JobKind.Compose => SecondVideoPath != null,
                JobKind.Concat => ConcatFiles.Count >= 2,
                _ => true
            });

        private void Cancel() => _cts?.Cancel();

        // ------------------------------------------------------------ 执行

        private async Task StartAsync()
        {
            if (IsBusy || !CanStart)
                return;

            var ffmpeg = _ffmpegPath;
            var ffprobe = _ffprobePath;
            if (ffmpeg == null || ffprobe == null)
            {
                ShowError(T("未找到本地 FFmpeg，请先指定路径。", "Local FFmpeg is not configured."));
                return;
            }

            var source = SourcePath!;
            var kind = CurrentJob;
            var suffix = GetSuffix(kind);
            var extension = GetOutputExtension(kind);

            try
            {
                Directory.CreateDirectory(OutputFolder);
            }
            catch (Exception ex)
            {
                ShowError(T("无法创建输出目录：", "Cannot create output folder: ") + ex.Message);
                return;
            }

            var outputPath = Path.Combine(OutputFolder,
                $"{Path.GetFileNameWithoutExtension(source)}_{suffix}{extension}");

            var service = new VideoJobService(ffmpeg, ffprobe);
            var options = BuildOptions(kind);

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var watch = Stopwatch.StartNew();
            IsBusy = true;
            IsIndeterminate = true;
            ProgressValue = 0;
            ProgressText = T("正在启动本地 FFmpeg…", "Starting local FFmpeg…");
            StatusText = T("正在处理…", "Processing…");
            OutputPath = string.Empty;

            var progress = new Progress<FfmpegProgress>(p =>
            {
                if (p.Fraction >= 0)
                {
                    IsIndeterminate = false;
                    ProgressValue = Math.Clamp(p.Fraction * 100, 0, 100);
                    var percent = (int)Math.Round(ProgressValue);
                    ProgressText = p.Message.Length > 0 ? $"{percent}% · {p.Message}" : $"{percent}%";
                }
                else
                {
                    IsIndeterminate = true;
                    ProgressText = p.Message;
                }
            });

            try
            {
                VideoRunResult result = kind switch
                {
                    JobKind.Enhance => await service.EnhanceAsync(source, outputPath,
                        (EnhanceOptions)options!, progress, token),
                    JobKind.Mosaic => await service.RemoveMosaicAsync(source, outputPath,
                        (MosaicOptions)options!, progress, token),
                    JobKind.Watermark => await service.RemoveWatermarkAsync(source, outputPath,
                        (WatermarkOptions)options!, progress, token),
                    JobKind.Compose => await service.ComposeAsync(source, SecondVideoPath!, outputPath,
                        (ComposeOptions)options!, progress, token),
                    JobKind.Concat => await service.ConcatAsync(ConcatFiles.ToList(), outputPath,
                        progress, token),
                    JobKind.M3u8 => await service.ConvertM3u8Async(source, outputPath, progress, token),
                    JobKind.Audio => await service.ExtractAudioAsync(source, outputPath,
                        (AudioOptions)options!, progress, token),
                    JobKind.Trim => await service.TrimAsync(source, outputPath,
                        (TrimOptions)options!, progress, token),
                    _ => await service.CreateGifAsync(source, outputPath,
                        (GifOptions)options!, progress, token)
                };

                watch.Stop();
                IsIndeterminate = false;
                ProgressValue = 100;
                ProgressText = "100%";
                ElapsedText = $"{watch.Elapsed.TotalSeconds:F1}s";
                OutputPath = result.OutputPath;
                StatusText = T("处理完成：", "Completed: ") + result.OutputPath
                    + (string.IsNullOrWhiteSpace(result.Note) ? string.Empty : $"（{result.Note}）");
            }
            catch (OperationCanceledException)
            {
                IsIndeterminate = false;
                ProgressValue = 0;
                ProgressText = string.Empty;
                StatusText = T("已取消", "Cancelled");
                ElapsedText = "-";
            }
            catch (Exception ex)
            {
                IsIndeterminate = false;
                ProgressText = string.Empty;
                StatusText = T("处理失败：", "Failed: ") + ex.Message;
                ShowError(ex.Message);
            }
            finally
            {
                IsBusy = false;
                watch.Stop();
                _cts?.Dispose();
                _cts = null;
            }
        }

        private object? BuildOptions(JobKind kind)
        {
            switch (kind)
            {
                case JobKind.Enhance:
                    var scale = SelectedScaleIndex switch
                    {
                        0 => 1.0, 1 => 1.5, 2 => 2.0, 3 => 3.0, _ => 4.0
                    };
                    return new EnhanceOptions
                    {
                        Scale = scale,
                        Denoise = (int)Math.Round(Denoise),
                        Sharpen = (int)Math.Round(Sharpen)
                    };
                case JobKind.Mosaic:
                    return new MosaicOptions
                    {
                        Denoise = (int)Math.Round(Denoise),
                        Sharpen = (int)Math.Round(Sharpen),
                        TemporalFrames = (int)Math.Round(TemporalFrames)
                    };
                case JobKind.Watermark:
                    return new WatermarkOptions { Regions = WatermarkRegions.ToArray() };
                case JobKind.Compose:
                    return new ComposeOptions
                    {
                        Layout = SelectedLayoutIndex == 1 ? ComposeLayout.PictureInPicture : ComposeLayout.SideBySide,
                        PipCorner = SelectedPipPositionIndex switch
                        {
                            0 => PipCorner.TopLeft, 1 => PipCorner.TopRight,
                            2 => PipCorner.BottomLeft, _ => PipCorner.BottomRight
                        },
                        PipScale = Math.Clamp(PipScale / 100.0, 0.1, 0.8)
                    };
                case JobKind.Audio:
                    return new AudioOptions
                    {
                        Format = SelectedAudioFormatIndex switch
                        {
                            1 => AudioOutputFormat.Aac, 2 => AudioOutputFormat.Wav, _ => AudioOutputFormat.Mp3
                        },
                        BitrateKbps = int.TryParse(BitrateOptions[SelectedBitrateIndex], out var bitrate) ? bitrate : 192
                    };
                case JobKind.Trim:
                    return new TrimOptions
                    {
                        StartSeconds = StartSeconds,
                        EndSeconds = EndSeconds > StartSeconds ? EndSeconds : DurationSeconds,
                        FastKeyframeCopy = SelectedTrimModeIndex == 0
                    };
                case JobKind.Gif:
                    return new GifOptions
                    {
                        Fps = (int)Math.Round(GifFps),
                        Width = (int)Math.Round(GifWidth),
                        LoopForever = GifLoop,
                        Dither = SelectedDitherIndex switch
                        {
                            0 => "none",
                            1 => "bayer:bayer_scale=5",
                            2 => "floyd_steinberg",
                            _ => "sierra2_4a"
                        }
                    };
                default:
                    return null;
            }
        }

        private static string GetSuffix(JobKind kind) => kind switch
        {
            JobKind.Enhance => "enhanced",
            JobKind.Mosaic => "demosaic",
            JobKind.Watermark => "delogo",
            JobKind.Compose => "composed",
            JobKind.Concat => "concat",
            JobKind.M3u8 => "m3u8",
            JobKind.Audio => "audio",
            JobKind.Trim => "trim",
            _ => "anim"
        };

        private string GetOutputExtension(JobKind kind) => kind switch
        {
            JobKind.Gif => ".gif",
            JobKind.Audio => SelectedAudioFormatIndex switch
            {
                1 => ".m4a",
                2 => ".wav",
                _ => ".mp3"
            },
            _ => ".mp4"
        };

        // ------------------------------------------------------------ 语言

        private void ToggleLanguage()
        {
            LocalizationManager.Toggle();
        }

        private void OnLanguageChanged()
        {
            BuildLocalizedLists();
            RaiseMediaInfo();
            RaiseRoiState();
            OnPropertyChanged(nameof(SourceFileName));
            OnPropertyChanged(nameof(SecondVideoName));
            OnPropertyChanged(nameof(ConcatSummary));
        }

        private void BuildLocalizedLists()
        {
            var functionIndex = SelectedFunctionIndex;
            var layoutIndex = SelectedLayoutIndex;
            var pipIndex = SelectedPipPositionIndex;
            var audioIndex = SelectedAudioFormatIndex;
            var trimIndex = SelectedTrimModeIndex;
            var ditherIndex = SelectedDitherIndex;

            Replace(Functions, new[]
            {
                T("高清修复", "Enhance"),
                T("去码（马赛克弱化）", "Mosaic Weaken"),
                T("去水印", "Remove Watermark"),
                T("多视频合成", "Multi-video Compose"),
                T("视频拼接", "Concatenate"),
                T("M3U8 转 MP4", "M3U8 to MP4"),
                T("提取音轨", "Extract Audio"),
                T("分段裁剪", "Trim Clip"),
                T("GIF 生成", "GIF Export")
            });
            Replace(LayoutOptions, new[]
            {
                T("并排", "Side by side"), T("画中画", "Picture in picture")
            });
            Replace(PipPositionOptions, new[]
            {
                T("左上", "Top left"), T("右上", "Top right"),
                T("左下", "Bottom left"), T("右下", "Bottom right")
            });
            Replace(AudioFormatOptions, new[]
            {
                T("MP3", "MP3"), T("AAC (M4A)", "AAC (M4A)"), T("WAV (PCM)", "WAV (PCM)")
            });
            Replace(TrimModeOptions, new[]
            {
                T("快速（关键帧对齐）", "Fast (keyframe aligned)"),
                T("精确（重编码）", "Precise (re-encode)")
            });
            Replace(DitherOptions, new[]
            {
                T("无", "None"), "Bayer", "Floyd-Steinberg", "Sierra 2-4A"
            });

            SelectedFunctionIndex = functionIndex;
            SelectedLayoutIndex = layoutIndex;
            SelectedPipPositionIndex = pipIndex;
            SelectedAudioFormatIndex = audioIndex;
            SelectedTrimModeIndex = trimIndex;
            SelectedDitherIndex = ditherIndex;
        }

        private static void Replace(ObservableCollection<string> target, string[] values)
        {
            target.Clear();
            foreach (var value in values)
                target.Add(value);
        }

        // ------------------------------------------------------------ 杂项

        private VideoJobService CreateService()
        {
            if (_ffmpegPath == null || _ffprobePath == null)
                throw new InvalidOperationException(T("未配置本地 FFmpeg。", "Local FFmpeg is not configured."));
            return new VideoJobService(_ffmpegPath, _ffprobePath);
        }

        private static string FormatTime(double seconds)
        {
            if (seconds <= 0)
                return "00:00.000";
            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1
                ? span.ToString(@"hh\:mm\:ss\.fff")
                : span.ToString(@"mm\:ss\.fff");
        }

        private void ShowError(string message)
        {
            MessageBox.Show(GetOwner(), message, T("视频处理", "Video processing"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static string T(string chinese, string english) => LocalizationManager.T(chinese, english);

        private static Window? GetOwner()
            => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

        private static System.Windows.Forms.IWin32Window? GetWinFormsOwner()
        {
            var window = GetOwner();
            return window == null ? null : new WinFormsWindowOwner(window);
        }

        private sealed class WinFormsWindowOwner : System.Windows.Forms.IWin32Window
        {
            public IntPtr Handle { get; }
            public WinFormsWindowOwner(Window window)
                => Handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        }

        private enum JobKind
        {
            Enhance,
            Mosaic,
            Watermark,
            Compose,
            Concat,
            M3u8,
            Audio,
            Trim,
            Gif
        }
    }
}
