using System;
using System.Collections.ObjectModel;
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
using Tang.VideoThrimalgoAI.Services;

namespace Tang.VideoThrimalgoAI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly ImageEnhancementService _enhancementService = new ImageEnhancementService();
        private readonly MosaicRemovalService _mosaicService = new MosaicRemovalService();
        private readonly WatermarkRemovalService _watermarkService = new WatermarkRemovalService();
        private readonly BatchProcessingService _batchProcessingService = new BatchProcessingService();

        public ObservableCollection<string> ProcessingModes { get; } = new ObservableCollection<string>
        {
            "画质增强",
            "高清修复",
            "去马赛克",
            "去水印"
        };

        public ObservableCollection<string> RecentFiles { get; } = new ObservableCollection<string>();

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

        private double _brightnessValue = 10;
        public double BrightnessValue
        {
            get => _brightnessValue;
            set => SetProperty(ref _brightnessValue, value);
        }

        private double _contrastValue = 1.2;
        public double ContrastValue
        {
            get => _contrastValue;
            set => SetProperty(ref _contrastValue, value);
        }

        private double _sharpnessValue = 1.5;
        public double SharpnessValue
        {
            get => _sharpnessValue;
            set => SetProperty(ref _sharpnessValue, value);
        }

        public IAsyncRelayCommand OpenImageCommand { get; }
        public IAsyncRelayCommand ProcessImageCommand { get; }
        public IAsyncRelayCommand OpenFolderCommand { get; }
        public IAsyncRelayCommand ProcessBatchCommand { get; }

        public MainViewModel()
        {
            OpenImageCommand = new AsyncRelayCommand(OpenImageAsync);
            ProcessImageCommand = new AsyncRelayCommand(ProcessImageAsync);
            OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync);
            ProcessBatchCommand = new AsyncRelayCommand(ProcessBatchAsync);
            RecentFiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(RecentFiles));
        }

        private async Task OpenImageAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            var filePath = dialog.FileName;
            if (!File.Exists(filePath))
                return;

            SelectedFileName = Path.GetFileName(filePath);
            CurrentImageSource = LoadBitmap(filePath);
            UpdateImageSize(filePath);

            if (!RecentFiles.Contains(filePath))
            {
                RecentFiles.Insert(0, filePath);
                if (RecentFiles.Count > 8)
                    RecentFiles.RemoveAt(RecentFiles.Count - 1);
            }

            StatusText = "已加载图像";
            await Task.CompletedTask;
        }

        private async Task ProcessImageAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedFileName) || SelectedFileName == "未打开图像")
            {
                StatusText = "请先选择图像文件";
                return;
            }

            var sourcePath = RecentFiles.FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                StatusText = "无法定位当前图像文件";
                return;
            }

            if (!File.Exists(sourcePath))
            {
                StatusText = "当前文件不存在";
                return;
            }

            try
            {
                StatusText = "正在处理...";
                using var original = Cv2.ImRead(sourcePath, ImreadModes.Color);
                if (original.Empty())
                    throw new InvalidOperationException("图片读取失败");

                Mat result = SelectedMode switch
                {
                    "画质增强" => await _enhancementService.EnhanceAsync(original, BrightnessValue, ContrastValue, 1.1),
                    "高清修复" => await _enhancementService.EnhanceAsync(original, BrightnessValue, ContrastValue, 1.3),
                    "去马赛克" => await _mosaicService.RemoveMosaicAsync(original, 8),
                    "去水印" => await _watermarkService.RemoveWatermarkAsync(original, new Rect(original.Width - 180, original.Height - 140, 180, 140)),
                    _ => original.Clone()
                };

                var outputFileName = $"{Path.GetFileNameWithoutExtension(sourcePath)}_processed.png";
                var outputPath = Path.Combine(OutputFolder, outputFileName);
                Directory.CreateDirectory(OutputFolder);
                Cv2.ImWrite(outputPath, result);

                CurrentImageSource = LoadBitmap(outputPath);
                SelectedFileName = Path.GetFileName(outputPath);
                StatusText = $"处理完成：{outputPath}";
            }
            catch (Exception ex)
            {
                StatusText = $"处理失败：{ex.Message}";
            }
        }

        private async Task OpenFolderAsync()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                OutputFolder = dialog.SelectedPath;
                StatusText = $"输出目录已设置：{OutputFolder}";
            }

            await Task.CompletedTask;
        }

        private async Task ProcessBatchAsync()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*",
                    Multiselect = true
                };

                if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
                    return;

                var outputs = await _batchProcessingService.ProcessBatchAsync(dialog.FileNames.ToList(), GetProcessingType(), OutputFolder);
                StatusText = $"批量处理完成，共处理 {outputs.Count} 张图像";

                if (outputs.Count > 0)
                    CurrentImageSource = LoadBitmap(outputs.First());
            }
            catch (Exception ex)
            {
                StatusText = $"批量处理失败：{ex.Message}";
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
                _ => ProcessingType.ImageEnhancement
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

        private void UpdateImageSize(string filePath)
        {
            try
            {
                using var image = Cv2.ImRead(filePath, ImreadModes.Color);
                ImageSizeText = $"{image.Width} x {image.Height}";
            }
            catch
            {
                ImageSizeText = "0 x 0";
            }
        }
    }
}
