using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using Tang.VideoThrimalgoAI.Models;

namespace Tang.VideoThrimalgoAI.Services
{
    /// <summary>
    /// Batch Processing Service
    /// Handles processing multiple images with configurable options
    /// </summary>
    public class BatchProcessingService
    {
        private readonly ImageEnhancementService _enhancementService;
        private readonly MosaicRemovalService _mosaicService;
        private readonly WatermarkRemovalService _watermarkService;

        public event EventHandler<ProcessingProgressEventArgs>? ProgressChanged;
        public event EventHandler<ProcessingErrorEventArgs>? ErrorOccurred;

        private CancellationTokenSource? _cancellationTokenSource;

        public BatchProcessingService()
        {
            _enhancementService = new ImageEnhancementService();
            _mosaicService = new MosaicRemovalService();
            _watermarkService = new WatermarkRemovalService();
        }

        /// <summary>
        /// Process batch of images
        /// </summary>
        public async Task<List<ImageProcessingModel>> ProcessBatchAsync(
            ProcessingTask task,
            CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var results = new List<ImageProcessingModel>();

            try
            {
                for (int i = 0; i < task.InputFiles.Count; i++)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    string inputFile = task.InputFiles[i];
                    var result = await ProcessSingleImageAsync(inputFile, task, _cancellationTokenSource.Token);
                    
                    if (result != null)
                        results.Add(result);

                    task.ProcessedFiles = i + 1;
                    ProgressChanged?.Invoke(this, new ProcessingProgressEventArgs
                    {
                        CurrentFile = i + 1,
                        TotalFiles = task.InputFiles.Count,
                        Progress = task.Progress,
                        CurrentFileName = Path.GetFileName(inputFile)
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ProcessingErrorEventArgs { Error = ex });
            }

            return results;
        }

        /// <summary>
        /// Process single image based on task type
        /// </summary>
        private async Task<ImageProcessingModel?> ProcessSingleImageAsync(
            string inputPath,
            ProcessingTask task,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(inputPath))
                return null;

            var startTime = DateTime.Now;
            var model = new ImageProcessingModel
            {
                InputImagePath = inputPath,
                ProcessingType = task.Type.ToString(),
                Status = ProcessingStatus.Processing
            };

            try
            {
                Mat image = Cv2.ImRead(inputPath);
                if (image.Empty())
                    throw new Exception("Failed to load image");

                model.Width = image.Width;
                model.Height = image.Height;

                Mat processedImage = null;

                switch (task.Type)
                {
                    case ProcessingType.ImageEnhancement:
                        processedImage = await _enhancementService.EnhanceImageAsync(image, task.Config);
                        break;

                    case ProcessingType.MosaicRemoval:
                        processedImage = await _mosaicService.RemoveMosaicAsync(
                            image,
                            task.Config.MosaicBlockSize,
                            task.Config.MosaicRemovalStrength);
                        break;

                    case ProcessingType.WatermarkRemoval:
                        // Default to removing bottom-right corner watermark
                        processedImage = await _watermarkService.RemoveCornerWatermarkAsync(image, 100);
                        break;

                    case ProcessingType.SuperResolution:
                        // For super-resolution, use bicubic interpolation as fallback
                        processedImage = new Mat();
                        Cv2.Resize(image, processedImage,
                            new Size(image.Width * task.Config.SuperResolutionScale,
                                    image.Height * task.Config.SuperResolutionScale),
                            0, 0, InterpolationFlags.Cubic);
                        break;

                    default:
                        processedImage = image.Clone();
                        break;
                }

                if (processedImage != null && !processedImage.Empty())
                {
                    // Save output image
                    string outputFileName = Path.GetFileNameWithoutExtension(inputPath) + "_processed.png";
                    string outputPath = Path.Combine(task.OutputDirectory, outputFileName);

                    Cv2.ImWrite(outputPath, processedImage);

                    model.OutputImagePath = outputPath;
                    model.Status = ProcessingStatus.Completed;
                    model.ProcessingTime = (DateTime.Now - startTime).TotalSeconds;

                    processedImage.Dispose();
                }
                else
                {
                    throw new Exception("Image processing returned null");
                }

                image.Dispose();
            }
            catch (Exception ex)
            {
                model.Status = ProcessingStatus.Failed;
                model.ErrorMessage = ex.Message;
                ErrorOccurred?.Invoke(this, new ProcessingErrorEventArgs { Error = ex });
            }

            return model;
        }

        /// <summary>
        /// Cancel current batch processing
        /// </summary>
        public void CancelProcessing()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Get supported image extensions
        /// </summary>
        public static string[] GetSupportedExtensions()
        {
            return new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp" };
        }

        /// <summary>
        /// Validate image file
        /// </summary>
        public static bool IsValidImageFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                string ext = Path.GetExtension(filePath).ToLower();
                if (!GetSupportedExtensions().Contains(ext))
                    return false;

                Mat test = Cv2.ImRead(filePath);
                bool isValid = !test.Empty();
                test.Dispose();

                return isValid;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ProcessingProgressEventArgs : EventArgs
    {
        public int CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public double Progress { get; set; }
        public string CurrentFileName { get; set; } = string.Empty;
    }

    public class ProcessingErrorEventArgs : EventArgs
    {
        public Exception Error { get; set; } = null!;
    }
}
