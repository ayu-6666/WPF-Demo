using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Tang.VideoThrimalgoAI.Services
{
    /// <summary>可用的超分模型。None 表示回退到纯算法管线。</summary>
    public enum SuperResolutionModel
    {
        None,
        RealEsrganX4
    }

    /// <summary>模型元信息：来源、许可证、大小、哈希，用于加载前完整性校验与界面展示。</summary>
    public sealed class SuperResolutionModelInfo
    {
        public SuperResolutionModel Model { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        /// <summary>模型原生倍率。</summary>
        public int NativeScale { get; init; } = 4;
        public long SizeBytes { get; init; }
        public string Sha256 { get; init; } = string.Empty;
        public string License { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        /// <summary>输入图像素上限，超过则回退纯算法，避免 CPU 推理时间与内存失控。</summary>
        public int MaxInputPixels { get; init; } = 4_000_000;
    }

    /// <summary>超分执行结果：包含实际是否命中模型，便于界面/批量提示真实模式。</summary>
    public sealed class SuperResolutionOutcome
    {
        public Mat Image { get; init; } = new Mat();
        public bool Applied { get; init; }
        public string Note { get; init; } = string.Empty;
    }

    /// <summary>
    /// 本地 ONNX 离线超分：仅读取模块 Models 目录下的模型文件，不联网。
    /// 推理采用分块 + 重叠 + 羽化融合，避免接缝；模型缺失/校验失败/过大时由调用方回退纯算法。
    /// </summary>
    public sealed class SuperResolutionService : IDisposable
    {
        // 分块 320px：在 6 核 CPU 上比 256px 少约 1/3 的块调用，单块并行效率更高；
        // 32px 重叠配合 16px 羽化斜坡，接缝不可见。
        private const int TileSize = 320;
        private const int TileOverlap = 32;
        private const string InputName = "input";
        private const string OutputName = "output";

        private static readonly object SessionLock = new object();
        private static readonly Dictionary<string, InferenceSession> SessionCache = new Dictionary<string, InferenceSession>(StringComparer.OrdinalIgnoreCase);

        private static readonly SuperResolutionModelInfo RealEsrganInfo = new SuperResolutionModelInfo
        {
            Model = SuperResolutionModel.RealEsrganX4,
            DisplayName = "Real-ESRGAN x4plus",
            FileName = "RealESRGAN_x4plus.onnx",
            NativeScale = 4,
            SizeBytes = 67_051_973,
            // 上游 Real-ESRGAN (RRDBNet) 官方权重导出的 ONNX，opset 14，动态 H/W，NCHW RGB [0,1]
            Sha256 = "39d5218cfcef542d667821a0d2072cfa51bfd857ab0e4ae7dc067c399a88d323",
            License = "BSD-3-Clause",
            Source = "https://github.com/xinntao/Real-ESRGAN",
            MaxInputPixels = 4_000_000
        };

        public static IReadOnlyList<SuperResolutionModelInfo> Models { get; } = new[] { RealEsrganInfo };

        /// <summary>模型目录：优先环境变量 TANG_SR_MODEL_DIR，否则为可执行文件同级的 Models 子目录。</summary>
        public static string ModelDirectory { get; } =
            Environment.GetEnvironmentVariable("TANG_SR_MODEL_DIR") is { Length: > 0 } custom
                ? custom
                : Path.Combine(AppContext.BaseDirectory, "Models");

        public static SuperResolutionModelInfo? GetInfo(SuperResolutionModel model)
            => Models.FirstOrDefault(m => m.Model == model);

        /// <summary>只检查文件是否存在，不做哈希校验（用于界面快速状态显示）。</summary>
        public static bool IsModelFilePresent(SuperResolutionModel model)
        {
            var info = GetInfo(model);
            return info != null && File.Exists(Path.Combine(ModelDirectory, info.FileName));
        }

        /// <summary>校验文件大小与 SHA-256，确保加载的是预期模型。</summary>
        public static bool Verify(SuperResolutionModelInfo info, out string reason)
        {
            var path = Path.Combine(ModelDirectory, info.FileName);
            if (!File.Exists(path))
            {
                reason = $"模型文件缺失：{path}";
                return false;
            }

            var length = new FileInfo(path).Length;
            if (length != info.SizeBytes)
            {
                reason = $"模型大小不符：{length} 字节，期望 {info.SizeBytes} 字节";
                return false;
            }

            using var stream = File.OpenRead(path);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
            if (!string.Equals(hash, info.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"模型哈希校验失败：{hash}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// 对 BGR 图像做超分，输出倍率为 <paramref name="targetScale"/>（2 或 4）。
        /// 先按模型原生 4x 推理，2x 时再降采样，保证 2x 也来自真实重建而非单纯插值。
        /// </summary>
        public SuperResolutionOutcome Upscale(Mat bgr, SuperResolutionModel model, int targetScale)
        {
            if (bgr.Empty())
                throw new InvalidOperationException("输入图像为空");

            if (model == SuperResolutionModel.None)
                return new SuperResolutionOutcome { Image = bgr.Clone(), Applied = false, Note = "未启用超分模型" };

            var info = GetInfo(model);
            if (info == null)
                return new SuperResolutionOutcome { Image = bgr.Clone(), Applied = false, Note = "未知超分模型，已回退纯算法" };

            if (!Verify(info, out var reason))
                return new SuperResolutionOutcome { Image = bgr.Clone(), Applied = false, Note = $"模型不可用（{reason}），已回退纯算法" };

            if ((long)bgr.Width * bgr.Height > info.MaxInputPixels)
                return new SuperResolutionOutcome
                {
                    Image = bgr.Clone(),
                    Applied = false,
                    Note = $"图像过大（{(long)bgr.Width * bgr.Height / 10000 / 100.0:F1}MP > 上限 {info.MaxInputPixels / 10000 / 100.0:F1}MP），已回退纯算法"
                };

            targetScale = targetScale >= 4 ? 4 : 2;

            InferenceSession session;
            try
            {
                session = GetOrCreateSession(info);
            }
            catch (Exception ex)
            {
                return new SuperResolutionOutcome { Image = bgr.Clone(), Applied = false, Note = $"模型加载失败（{ex.Message}），已回退纯算法" };
            }

            var full4x = RunTiled(session, bgr, info.NativeScale);
            if (targetScale == info.NativeScale)
                return new SuperResolutionOutcome { Image = full4x, Applied = true, Note = $"{info.DisplayName} 4x" };

            var resized = new Mat();
            Cv2.Resize(full4x, resized,
                new Size(bgr.Width * targetScale, bgr.Height * targetScale), 0, 0, InterpolationFlags.Area);
            full4x.Dispose();
            return new SuperResolutionOutcome { Image = resized, Applied = true, Note = $"{info.DisplayName} 4x→{targetScale}x" };
        }

        private static InferenceSession GetOrCreateSession(SuperResolutionModelInfo info)
        {
            var path = Path.Combine(ModelDirectory, info.FileName);
            lock (SessionLock)
            {
                if (SessionCache.TryGetValue(path, out var cached))
                    return cached;

                var options = new SessionOptions
                {
                    // 本地 CPU 推理，不联网；限制线程避免与界面渲染抢占
                    IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount),
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };
                var session = new InferenceSession(path, options);
                SessionCache[path] = session;
                return session;
            }
        }

        /// <summary>
        /// 分块推理：按 256px 窗口、32px 重叠逐块推理，直接把块按羽化权重融合进 8U 输出图。
        /// 只保留单块缓冲，避免大图 float 累加爆内存；块按行优先顺序写入，左/上斜坡与
        /// 前一块的覆盖区平滑过渡。
        /// </summary>
        private static Mat RunTiled(InferenceSession session, Mat bgr, int scale)
        {
            var width = bgr.Width;
            var height = bgr.Height;
            var result = new Mat(height * scale, width * scale, MatType.CV_8UC3, Scalar.All(0));
            var written = new Mat(result.Size(), MatType.CV_8UC1, Scalar.All(0));

            var yPositions = TilePositions(height, TileSize, TileSize - TileOverlap);
            var xPositions = TilePositions(width, TileSize, TileSize - TileOverlap);

            foreach (var y in yPositions)
            {
                var tileHeight = Math.Min(TileSize, height - y);
                foreach (var x in xPositions)
                {
                    var tileWidth = Math.Min(TileSize, width - x);
                    using var tileRoi = new Mat(bgr, new Rect(x, y, tileWidth, tileHeight));
                    using var tileResult = RunSingle(session, tileRoi, scale);
                    using var mask = BuildFeatherMask(tileWidth, tileHeight, scale, x > 0, y > 0);

                    var destination = new Rect(x * scale, y * scale, tileWidth * scale, tileHeight * scale);
                    BlendInto(result, written, tileResult, mask, destination);
                }
            }

            written.Dispose();
            return result;
        }

        /// <summary>把单块结果按羽化权重写入输出图；未写入区域 alpha 视为 1，避免边缘发暗。</summary>
        private static void BlendInto(Mat output, Mat written, Mat tileResult, Mat mask, Rect destination)
        {
            using var outputRoi = new Mat(output, destination);
            using var writtenRoi = new Mat(written, destination);

            using var tileFloat = new Mat();
            tileResult.ConvertTo(tileFloat, MatType.CV_32FC3);
            using var oldPart = new Mat();
            outputRoi.ConvertTo(oldPart, MatType.CV_32FC3);

            using var alpha = mask.Clone();
            using var unwritten = new Mat();
            Cv2.Threshold(writtenRoi, unwritten, 0, 255, ThresholdTypes.BinaryInv);
            alpha.SetTo(Scalar.All(1.0), unwritten);

            using var ones = new Mat(destination.Size, MatType.CV_32FC1, Scalar.All(1));
            using var inverse = new Mat();
            Cv2.Subtract(ones, alpha, inverse);
            using var alpha3 = new Mat();
            using var inverse3 = new Mat();
            Cv2.Merge(new[] { alpha, alpha, alpha }, alpha3);
            Cv2.Merge(new[] { inverse, inverse, inverse }, inverse3);

            Cv2.Multiply(oldPart, inverse3, oldPart);
            Cv2.Multiply(tileFloat, alpha3, tileFloat);
            Cv2.Add(oldPart, tileFloat, oldPart);
            oldPart.ConvertTo(outputRoi, MatType.CV_8UC3);

            writtenRoi.SetTo(Scalar.All(255));
        }

        /// <summary>分块位置：保证块尽量铺满且末块与其他块重叠，不产生细小碎块。</summary>
        private static List<int> TilePositions(int length, int tile, int step)
        {
            var positions = new List<int>();
            if (length <= tile)
            {
                positions.Add(0);
                return positions;
            }

            for (var position = 0; position + tile < length; position += step)
                positions.Add(position);

            var last = length - tile;
            if (positions.Count == 0 || positions[^1] != last)
            {
                // 末块与前一块重叠过大时用末块替换，避免反复处理同一大片区域
                if (positions.Count > 0 && last - positions[^1] < step / 2)
                    positions[^1] = last;
                else
                    positions.Add(last);
            }
            return positions;
        }

        /// <summary>羽化权重：只在与前一块重叠的左/上边界做线性斜坡，其余区域满权。</summary>
        private static Mat BuildFeatherMask(int tileWidth, int tileHeight, int scale, bool rampLeft, bool rampTop)
        {
            var ramp = Math.Max(1, TileOverlap / 2);
            using var maskSmall = new Mat(tileHeight, tileWidth, MatType.CV_32FC1, Scalar.All(1));
            for (var row = 0; row < tileHeight; row++)
            {
                var vertical = rampTop && row < ramp ? (row + 1) / (float)(ramp + 1) : 1.0f;
                for (var column = 0; column < tileWidth; column++)
                {
                    var horizontal = rampLeft && column < ramp ? (column + 1) / (float)(ramp + 1) : 1.0f;
                    maskSmall.Set(row, column, vertical * horizontal);
                }
            }

            var mask = new Mat();
            Cv2.Resize(maskSmall, mask, new Size(tileWidth * scale, tileHeight * scale), 0, 0, InterpolationFlags.Linear);
            return mask;
        }

        /// <summary>单块推理：BGR→RGB→NCHW float[0,1] 输入，输出 CHW float 还原为 BGR 8U。</summary>
        private static Mat RunSingle(InferenceSession session, Mat tileRoi, int scale)
        {
            using var rgb = new Mat();
            Cv2.CvtColor(tileRoi, rgb, ColorConversionCodes.BGR2RGB);
            using var floatRgb = new Mat();
            rgb.ConvertTo(floatRgb, MatType.CV_32FC3, 1.0 / 255.0);
            if (!floatRgb.IsContinuous())
            {
                using var contiguous = floatRgb.Clone();
                return InferAndDecode(session, contiguous, scale);
            }
            return InferAndDecode(session, floatRgb, scale);
        }

        private static Mat InferAndDecode(InferenceSession session, Mat floatRgb, int scale)
        {
            var height = floatRgb.Rows;
            var width = floatRgb.Cols;
            var pixelCount = height * width;
            var interleaved = new float[pixelCount * 3];
            Marshal.Copy(floatRgb.Data, interleaved, 0, interleaved.Length);

            // HWC → CHW
            var chw = new float[interleaved.Length];
            for (var channel = 0; channel < 3; channel++)
                for (var index = 0; index < pixelCount; index++)
                    chw[channel * pixelCount + index] = interleaved[index * 3 + channel];

            var tensor = new DenseTensor<float>(chw, new[] { 1, 3, height, width });
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(InputName, tensor) };
            using var results = session.Run(inputs, new[] { OutputName });
            var output = results.First().AsTensor<float>();

            var outHeight = output.Dimensions[2];
            var outWidth = output.Dimensions[3];
            var outPixels = outHeight * outWidth;
            var buffer = output.ToArray();

            using var channelB = new Mat(outHeight, outWidth, MatType.CV_32FC1);
            using var channelG = new Mat(outHeight, outWidth, MatType.CV_32FC1);
            using var channelR = new Mat(outHeight, outWidth, MatType.CV_32FC1);
            Marshal.Copy(buffer, 0, channelR.Data, outPixels);
            Marshal.Copy(buffer, outPixels, channelG.Data, outPixels);
            Marshal.Copy(buffer, outPixels * 2, channelB.Data, outPixels);

            using var mergedRgb = new Mat();
            Cv2.Merge(new[] { channelR, channelG, channelB }, mergedRgb);
            using var mergedBgr = new Mat();
            Cv2.CvtColor(mergedRgb, mergedBgr, ColorConversionCodes.RGB2BGR);

            var result = new Mat();
            mergedBgr.ConvertTo(result, MatType.CV_8UC3, 255.0);
            return result;
        }

        public void Dispose()
        {
            lock (SessionLock)
            {
                foreach (var session in SessionCache.Values)
                    session.Dispose();
                SessionCache.Clear();
            }
        }
    }
}
