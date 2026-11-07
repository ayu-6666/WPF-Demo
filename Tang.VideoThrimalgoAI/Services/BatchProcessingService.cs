using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;
using Tang.VideoThrimalgoAI.Models;

namespace Tang.VideoThrimalgoAI.Services
{
    public class BatchProgressInfo
    {
        public int Completed { get; init; }
        public int Total { get; init; }
        public string CurrentFile { get; init; } = string.Empty;
    }

    public class BatchResult
    {
        public int Total { get; set; }
        public List<string> Outputs { get; } = new List<string>();
        /// <summary>失败明细，格式 “文件名 - 原因”。</summary>
        public List<string> Failures { get; } = new List<string>();
        /// <summary>整批级别的提示（如超分模型不可用后回退纯算法）。</summary>
        public List<string> Warnings { get; } = new List<string>();
    }

    public class BatchProcessingService
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp"
        };

        private readonly ImageEnhancementService _enhancementService = new ImageEnhancementService();
        private readonly SuperResolutionService _superResolutionService = new SuperResolutionService();
        private readonly MosaicRemovalService _mosaicRemovalService = new MosaicRemovalService();
        private readonly WatermarkRemovalService _watermarkRemovalService = new WatermarkRemovalService();
        private readonly DeskewService _deskewService = new DeskewService();

        /// <summary>
        /// 遍历所选文件夹（含子文件夹）中的常见图片格式逐个处理。
        /// 单个文件失败只记录明细，不中断整体流程；整体在后台线程执行，进度通过 IProgress 回报 UI。
        /// 修复强度/2x 放大/亮度对比度锐度沿用界面当前设置，保证批量与单张一致。
        /// </summary>
        public Task<BatchResult> ProcessFolderAsync(
            string folder, ProcessingType type, string outputDirectory,
            ImageRepairOptions repairOptions,
            SuperResolutionModel superResolutionModel,
            IProgress<BatchProgressInfo>? progress)
        {
            return Task.Run(() =>
            {
                var result = new BatchResult();
                Directory.CreateDirectory(outputDirectory);

                // 模型不可用时只提示一次并整批回退纯算法；单文件失败仍按“解码失败/处理失败”区分
                var modelUsable = false;
                if (type == ProcessingType.SuperResolution && superResolutionModel != SuperResolutionModel.None)
                {
                    var info = SuperResolutionService.GetInfo(superResolutionModel);
                    if (info == null)
                    {
                        result.Warnings.Add($"超分模型不可用：未知模型；本批已回退纯算法");
                    }
                    else if (SuperResolutionService.Verify(info, out var reason))
                    {
                        modelUsable = true;
                    }
                    else
                    {
                        result.Warnings.Add($"超分模型不可用：{reason}；本批已回退纯算法");
                    }
                }

                var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                    .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
                    // 跳过本工具生成的 *_processed 结果，避免输入输出同目录时重复处理
                    .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("_processed", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                result.Total = files.Count;
                var completed = 0;

                foreach (var file in files)
                {
                    try
                    {
                        using var source = ImageFileIO.ReadMat(file);
                        if (source.Empty())
                        {
                            result.Failures.Add($"{Path.GetFileName(file)} - 无法解码图片");
                        }
                        else
                        {
                            using var processed = ProcessOne(source, type, repairOptions, superResolutionModel, modelUsable);
                            var outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(file) + "_processed.png");
                            ImageFileIO.WriteMat(processed, outputPath);
                            result.Outputs.Add(outputPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Failures.Add($"{Path.GetFileName(file)} - 处理失败：{ex.Message}");
                    }
                    finally
                    {
                        completed++;
                        progress?.Report(new BatchProgressInfo
                        {
                            Completed = completed,
                            Total = files.Count,
                            CurrentFile = Path.GetFileName(file)
                        });
                    }
                }

                return result;
            });
        }

        private Mat ProcessOne(Mat source, ProcessingType type, ImageRepairOptions repairOptions,
            SuperResolutionModel superResolutionModel, bool modelUsable)
        {
            switch (type)
            {
                case ProcessingType.ImageEnhancement:
                    return _enhancementService.Repair(source, CloneOptions(repairOptions, RepairPreset.Light));
                case ProcessingType.SuperResolution:
                    if (superResolutionModel != SuperResolutionModel.None && modelUsable)
                    {
                        var outcome = _superResolutionService.Upscale(
                            source, superResolutionModel, repairOptions.Upscale2x ? 2 : 4);
                        if (outcome.Applied)
                            return outcome.Image;
                        outcome.Image.Dispose();
                    }
                    return _enhancementService.Repair(source, repairOptions);
                case ProcessingType.MosaicRemoval:
                    return _mosaicRemovalService.RemoveMosaic(source, 8);
                case ProcessingType.WatermarkRemoval:
                    return _watermarkRemovalService.RemoveWatermark(source, WatermarkRemovalService.GetDefaultRegion(source));
                case ProcessingType.Deskew:
                    return _deskewService.CorrectTilt(source).Image;
                default:
                    return source.Clone();
            }
        }

        private static ImageRepairOptions CloneOptions(ImageRepairOptions source, RepairPreset preset)
            => new ImageRepairOptions
            {
                Preset = preset,
                Upscale2x = source.Upscale2x,
                Brightness = source.Brightness,
                Contrast = source.Contrast,
                Sharpness = source.Sharpness
            };
    }
}
