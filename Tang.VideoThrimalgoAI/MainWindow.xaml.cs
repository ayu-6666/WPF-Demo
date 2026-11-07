using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenCvSharp;
using Tang.VideoThrimalgoAI.Models;
using Tang.VideoThrimalgoAI.Services;

// OpenCvSharp 与 WPF 存在同名类型，显式消歧
using Window = System.Windows.Window;

namespace Tang.VideoThrimalgoAI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            StateChanged += (_, _) => UpdateMaximizeButtonGlyph();
            Loaded += (_, _) => UpdateMaximizeButtonGlyph();
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void OnCloseClick(object sender, RoutedEventArgs e)
            => Close();

        private void UpdateMaximizeButtonGlyph()
            => MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly ImageEnhancementService _enhancementService = new ImageEnhancementService();
        private readonly SuperResolutionService _superResolutionService = new SuperResolutionService();
        private readonly MosaicRemovalService _mosaicService = new MosaicRemovalService();
        private readonly WatermarkRemovalService _watermarkService = new WatermarkRemovalService();
        private readonly DeskewService _deskewService = new DeskewService();
        private readonly BatchProcessingService _batchProcessingService = new BatchProcessingService();

        /// <summary>当前预览图像的完整路径，修复处理始终针对该文件，而不是猜测最近列表。</summary>
        private string? _currentFilePath;

        /// <summary>本次处理实际采用的模式（ONNX 超分或纯算法回退），用于状态栏如实提示。</summary>
        private string _lastModeNote = "纯算法";

        public ObservableCollection<string> ProcessingModes { get; } = new ObservableCollection<string>
        {
            "画质增强",
            "高清修复",
            "去马赛克",
            "去水印",
            "水平校正"
        };

        public ObservableCollection<string> RecentFiles { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> RepairPresets { get; } = new ObservableCollection<string>
        {
            "轻度",
            "标准",
            "强"
        };

        private string _selectedRepairPreset = "标准";
        public string SelectedRepairPreset
        {
            get => _selectedRepairPreset;
            set => SetProperty(ref _selectedRepairPreset, value);
        }

        private bool _upscale2x;
        public bool Upscale2x
        {
            get => _upscale2x;
            set
            {
                if (SetProperty(ref _upscale2x, value))
                    OnPropertyChanged(nameof(SuperResolutionStatusText));
            }
        }

        /// <summary>超分模型选项：关闭时使用纯算法；Real-ESRGAN 使用本地 ONNX 离线推理。</summary>
        public ObservableCollection<string> SuperResolutionModels { get; } = new ObservableCollection<string>
        {
            "关闭",
            "Real-ESRGAN 4x"
        };

        private string _selectedSuperResolutionModel = "关闭";
        public string SelectedSuperResolutionModel
        {
            get => _selectedSuperResolutionModel;
            set
            {
                if (SetProperty(ref _selectedSuperResolutionModel, value))
                    OnPropertyChanged(nameof(SuperResolutionStatusText));
            }
        }

        private bool _superResolutionAvailable;
        private string _superResolutionDetail = string.Empty;

        /// <summary>界面提示：当前是否真正启用 ONNX 超分、输出倍率与不可用原因。</summary>
        public string SuperResolutionStatusText
        {
            get
            {
                if (GetSelectedSuperResolutionModel() == SuperResolutionModel.None)
                    return "超分模型：关闭（使用纯算法修复）";

                var scale = Upscale2x ? 2 : 4;
                return _superResolutionAvailable
                    ? $"超分模型：Real-ESRGAN 4x 可用（输出 {scale}x，本地离线推理）"
                    : $"超分模型不可用：{_superResolutionDetail}（将回退纯算法）";
            }
        }

        private string _selectedMode = "画质增强";
        public string SelectedMode
        {
            get => _selectedMode;
            set => SetProperty(ref _selectedMode, value);
        }

        private string _selectedFileName = "未打开图像";
        public string SelectedFileName
        {
            get => _selectedFileName;
            set => SetProperty(ref _selectedFileName, value);
        }

        private string _outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        public string OutputFolder
        {
            get => _outputFolder;
            set => SetProperty(ref _outputFolder, value);
        }

        private string _statusText = "就绪";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private ImageSource? _currentImageSource;
        public ImageSource? CurrentImageSource
        {
            get => _currentImageSource;
            set => SetProperty(ref _currentImageSource, value);
        }

        private string _imageSizeText = "0 x 0";
        public string ImageSizeText
        {
            get => _imageSizeText;
            set => SetProperty(ref _imageSizeText, value);
        }

        private double _brightnessValue = 0;
        public double BrightnessValue
        {
            get => _brightnessValue;
            set => SetProperty(ref _brightnessValue, value);
        }

        private double _contrastValue = 1.0;
        public double ContrastValue
        {
            get => _contrastValue;
            set => SetProperty(ref _contrastValue, value);
        }

        private double _sharpnessValue = 1.2;
        public double SharpnessValue
        {
            get => _sharpnessValue;
            set => SetProperty(ref _sharpnessValue, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        private bool _isBatchIndeterminate;
        public bool IsBatchIndeterminate
        {
            get => _isBatchIndeterminate;
            private set => SetProperty(ref _isBatchIndeterminate, value);
        }

        private double _batchProgressValue;
        public double BatchProgressValue
        {
            get => _batchProgressValue;
            private set => SetProperty(ref _batchProgressValue, value);
        }

        private string _batchProgressText = string.Empty;
        public string BatchProgressText
        {
            get => _batchProgressText;
            private set => SetProperty(ref _batchProgressText, value);
        }

        public IAsyncRelayCommand OpenImageCommand { get; }
        public IAsyncRelayCommand ProcessImageCommand { get; }
        public IAsyncRelayCommand OpenFolderCommand { get; }
        public IAsyncRelayCommand ProcessBatchCommand { get; }

        public MainViewModel()
        {
            RefreshSuperResolutionStatus();
            OpenImageCommand = new AsyncRelayCommand(OpenImageAsync);
            ProcessImageCommand = new AsyncRelayCommand(ProcessImageAsync);
            OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync);
            ProcessBatchCommand = new AsyncRelayCommand(ProcessBatchAsync);
        }

        private async Task OpenImageAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp|所有文件|*.*",
                Multiselect = false,
                Title = "打开图片"
            };

            var owner = GetActiveWindow();
            if (dialog.ShowDialog(owner) != true)
                return;

            LoadImageFile(dialog.FileName);
            await Task.CompletedTask;
        }

        private void LoadImageFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                StatusText = "图片文件不存在";
                return;
            }

            try
            {
                using var mat = ImageFileIO.ReadMat(filePath);
                if (mat.Empty())
                {
                    StatusText = "无法解码图片文件";
                    return;
                }

                _currentFilePath = filePath;
                SelectedFileName = Path.GetFileName(filePath);
                CurrentImageSource = LoadBitmap(filePath);
                ImageSizeText = $"{mat.Width} x {mat.Height}";

                // 始终把当前文件移到最近列表首位（修复旧逻辑打开已有项时顺序不更新的问题）
                RecentFiles.Remove(filePath);
                RecentFiles.Insert(0, filePath);
                if (RecentFiles.Count > 8)
                    RecentFiles.RemoveAt(RecentFiles.Count - 1);

                StatusText = "已加载图像";
            }
            catch (Exception ex)
            {
                StatusText = $"加载失败：{ex.Message}";
            }
        }

        private async Task ProcessImageAsync()
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(_currentFilePath) || !File.Exists(_currentFilePath))
            {
                StatusText = "请先打开图片，再开始修复";
                return;
            }

            IsBusy = true;
            IsBatchIndeterminate = true;
            BatchProgressText = string.Empty;
            var watch = Stopwatch.StartNew();

            try
            {
                StatusText = $"正在处理：{SelectedMode}...";
                var sourcePath = _currentFilePath;
                using var original = await Task.Run(() => ImageFileIO.ReadMat(sourcePath));
                if (original.Empty())
                    throw new InvalidOperationException("图片解码失败");

                Mat result;
                double deskewAngle = 0;
                _lastModeNote = "纯算法";
                switch (SelectedMode)
                {
                    case "画质增强":
                        result = await _enhancementService.RepairAsync(original, BuildRepairOptions(RepairPreset.Light));
                        break;
                    case "高清修复":
                        result = await ProcessHighDetailAsync(original);
                        break;
                    case "去马赛克":
                        result = await _mosaicService.RemoveMosaicAsync(original, 8);
                        break;
                    case "去水印":
                        result = await _watermarkService.RemoveWatermarkAsync(original, WatermarkRemovalService.GetDefaultRegion(original));
                        break;
                    case "水平校正":
                        var deskew = await _deskewService.CorrectTiltAsync(original);
                        result = deskew.Image;
                        deskewAngle = deskew.Angle;
                        break;
                    default:
                        result = original.Clone();
                        break;
                }

                using (result)
                {
                    Directory.CreateDirectory(OutputFolder);
                    var outputPath = Path.Combine(OutputFolder, $"{Path.GetFileNameWithoutExtension(sourcePath)}_processed.png");
                    await Task.Run(() => ImageFileIO.WriteMat(result, outputPath));

                    CurrentImageSource = LoadBitmap(outputPath);
                    SelectedFileName = Path.GetFileName(outputPath);
                    ImageSizeText = $"{result.Width} x {result.Height}";
                    watch.Stop();

                    StatusText = SelectedMode == "水平校正"
                        ? $"处理完成（校正角度 {deskewAngle:F1}°，耗时 {watch.Elapsed.TotalSeconds:F1}s）：{outputPath}"
                        : SelectedMode == "高清修复"
                            ? $"高清修复完成（{SelectedRepairPreset}强度，{_lastModeNote}，{result.Width}x{result.Height}，耗时 {watch.Elapsed.TotalSeconds:F1}s）：{outputPath}"
                            : $"处理完成（耗时 {watch.Elapsed.TotalSeconds:F1}s）：{outputPath}";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"处理失败：{ex.Message}";
            }
            finally
            {
                IsBusy = false;
                IsBatchIndeterminate = false;
            }
        }

        private async Task OpenFolderAsync()
        {
            try
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择输出目录",
                    ShowNewFolderButton = true
                };

                var owner = GetWinFormsOwner();
                var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    OutputFolder = dialog.SelectedPath;
                    StatusText = $"输出目录已设置：{OutputFolder}";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"无法打开文件夹选择框：{ex.Message}";
            }

            await Task.CompletedTask;
        }

        private async Task ProcessBatchAsync()
        {
            if (IsBusy)
                return;

            string inputFolder;
            try
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择要批量修复的文件夹（将遍历其中及子文件夹中的常见图片格式）"
                };

                var owner = GetWinFormsOwner();
                var dialogResult = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                if (dialogResult != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                    return;

                inputFolder = dialog.SelectedPath;
            }
            catch (Exception ex)
            {
                StatusText = $"无法打开文件夹选择框：{ex.Message}";
                return;
            }

            IsBusy = true;
            IsBatchIndeterminate = true;
            BatchProgressValue = 0;
            BatchProgressText = "正在扫描图片...";
            var watch = Stopwatch.StartNew();

            try
            {
                // Progress 回调在创建时捕获 UI 线程上下文，批处理循环整体在后台线程执行
                var progress = new Progress<BatchProgressInfo>(p =>
                {
                    IsBatchIndeterminate = false;
                    if (p.Total > 0)
                        BatchProgressValue = p.Completed * 100.0 / p.Total;
                    BatchProgressText = $"批量进度 {p.Completed}/{p.Total}：{p.CurrentFile}";
                });

                var result = await _batchProcessingService.ProcessFolderAsync(
                    inputFolder, GetProcessingType(), OutputFolder,
                    BuildRepairOptions(GetProcessingType() == ProcessingType.ImageEnhancement ? RepairPreset.Light : null),
                    GetSelectedSuperResolutionModel(),
                    progress);
                watch.Stop();

                if (result.Total == 0)
                {
                    StatusText = $"所选文件夹中未找到图片（支持 png/jpg/jpeg/bmp/tif/tiff/webp）：{inputFolder}";
                    MessageBox.Show(GetActiveWindow(), StatusText, "批量修复", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var summary = $"批量完成：成功 {result.Outputs.Count} / 失败 {result.Failures.Count} / 共 {result.Total}，耗时 {watch.Elapsed.TotalSeconds:F1}s";
                StatusText = $"{summary}，输出目录：{OutputFolder}";

                if (result.Outputs.Count > 0)
                {
                    var first = result.Outputs[0];
                    CurrentImageSource = LoadBitmap(first);
                    SelectedFileName = Path.GetFileName(first);
                    using var mat = ImageFileIO.ReadMat(first);
                    ImageSizeText = $"{mat.Width} x {mat.Height}";
                }

                if (result.Warnings.Count > 0)
                {
                    MessageBox.Show(GetActiveWindow(), string.Join(Environment.NewLine, result.Warnings),
                        "批量处理提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                if (result.Failures.Count > 0)
                {
                    var detail = string.Join(Environment.NewLine, result.Failures.Take(8));
                    if (result.Failures.Count > 8)
                        detail += Environment.NewLine + $"……等共 {result.Failures.Count} 项失败";
                    MessageBox.Show(GetActiveWindow(), detail, $"批量修复：{summary}", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"批量处理失败：{ex.Message}";
            }
            finally
            {
                IsBusy = false;
                IsBatchIndeterminate = false;
            }
        }

        private ProcessingType GetProcessingType()
        {
            return SelectedMode switch
            {
                "画质增强" => ProcessingType.ImageEnhancement,
                "高清修复" => ProcessingType.SuperResolution,
                "去马赛克" => ProcessingType.MosaicRemoval,
                "去水印" => ProcessingType.WatermarkRemoval,
                "水平校正" => ProcessingType.Deskew,
                _ => ProcessingType.ImageEnhancement
            };
        }

        private SuperResolutionModel GetSelectedSuperResolutionModel()
            => SelectedSuperResolutionModel == "Real-ESRGAN 4x"
                ? SuperResolutionModel.RealEsrganX4
                : SuperResolutionModel.None;

        /// <summary>校验本地模型（大小 + SHA-256），结果用于界面状态与批量提示。</summary>
        private void RefreshSuperResolutionStatus()
        {
            _superResolutionAvailable = false;
            _superResolutionDetail = "模型文件缺失";
            try
            {
                var info = SuperResolutionService.GetInfo(SuperResolutionModel.RealEsrganX4);
                if (info == null)
                    return;

                var path = Path.Combine(SuperResolutionService.ModelDirectory, info.FileName);
                if (!File.Exists(path))
                {
                    _superResolutionDetail = $"未找到 {info.FileName}（{SuperResolutionService.ModelDirectory}）";
                    return;
                }

                if (!SuperResolutionService.Verify(info, out var reason))
                {
                    _superResolutionDetail = reason;
                    return;
                }

                _superResolutionAvailable = true;
                _superResolutionDetail = string.Empty;
            }
            catch (Exception ex)
            {
                _superResolutionDetail = ex.Message;
            }
            OnPropertyChanged(nameof(SuperResolutionStatusText));
        }

        /// <summary>
        /// 高清修复：优先本地 ONNX 超分；模型缺失/校验失败/加载失败时回退纯算法，
        /// 并通过 _lastModeNote 让状态栏如实显示实际使用的模式。
        /// </summary>
        private async Task<Mat> ProcessHighDetailAsync(Mat original)
        {
            var model = GetSelectedSuperResolutionModel();
            if (model != SuperResolutionModel.None)
            {
                var targetScale = Upscale2x ? 2 : 4;
                var outcome = await Task.Run(() => _superResolutionService.Upscale(original, model, targetScale));
                if (outcome.Applied)
                {
                    _lastModeNote = $"ONNX {outcome.Note}";
                    return outcome.Image;
                }

                outcome.Image.Dispose();
                _lastModeNote = $"{outcome.Note} → 纯算法回退";
            }

            return await _enhancementService.RepairAsync(original, BuildRepairOptions());
        }

        private ImageRepairOptions BuildRepairOptions(RepairPreset? forcedPreset = null)
        {
            var preset = forcedPreset;
            if (preset == null)
            {
                preset = SelectedRepairPreset switch
                {
                    "轻度" => RepairPreset.Light,
                    "强" => RepairPreset.Strong,
                    _ => RepairPreset.Standard
                };
            }

            return new ImageRepairOptions
            {
                Preset = preset.Value,
                Upscale2x = Upscale2x,
                Brightness = BrightnessValue,
                Contrast = ContrastValue,
                Sharpness = SharpnessValue
            };
        }

        private ImageSource LoadBitmap(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.DecodePixelWidth = 1200;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static Window? GetActiveWindow()
            => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

        private static System.Windows.Forms.IWin32Window? GetWinFormsOwner()
        {
            var window = GetActiveWindow();
            return window == null ? null : new WinFormsWindowOwner(window);
        }

        /// <summary>把 WPF Window 适配为 WinForms IWin32Window，确保对话框挂在主窗口前。</summary>
        private sealed class WinFormsWindowOwner : System.Windows.Forms.IWin32Window
        {
            public IntPtr Handle { get; }

            public WinFormsWindowOwner(Window window)
                => Handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        }
    }
}
