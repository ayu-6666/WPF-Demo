using System;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Tang.VideoThrimalgoAI.Services
{
    /// <summary>修复强度预设。</summary>
    public enum RepairPreset
    {
        Light,
        Standard,
        Strong
    }

    /// <summary>图像高清修复参数。</summary>
    public sealed class ImageRepairOptions
    {
        public RepairPreset Preset { get; set; } = RepairPreset.Standard;
        /// <summary>2x 插值放大后重新做细节重建；超大图会自动跳过以控制内存。</summary>
        public bool Upscale2x { get; set; }
        /// <summary>亮度微调（-50~50），映射为温和伽马目标。</summary>
        public double Brightness { get; set; }
        /// <summary>对比度微调，实际生效范围被限制在 0.9~1.15，避免拉爆。</summary>
        public double Contrast { get; set; } = 1.0;
        /// <summary>细节强度，1.2 为基准。</summary>
        public double Sharpness { get; set; } = 1.2;
    }

    public class ImageEnhancementService
    {
        private const long MaxUpscalePixels = 20_000_000;

        public async Task<Mat> RepairAsync(Mat image, ImageRepairOptions options)
            => await Task.Run(() => Repair(image, options));

        /// <summary>
        /// 兼容旧调用：亮度/对比度/锐度映射到轻度或标准修复。
        /// </summary>
        public Task<Mat> EnhanceAsync(Mat image, double brightness, double contrast, double sharpness, bool highDetail)
            => RepairAsync(image, new ImageRepairOptions
            {
                Preset = highDetail ? RepairPreset.Standard : RepairPreset.Light,
                Brightness = brightness,
                Contrast = contrast,
                Sharpness = sharpness
            });

        public Mat Enhance(Mat image, double brightness, double contrast, double sharpness, bool highDetail)
            => Repair(image, new ImageRepairOptions
            {
                Preset = highDetail ? RepairPreset.Standard : RepairPreset.Light,
                Brightness = brightness,
                Contrast = contrast,
                Sharpness = sharpness
            });

        /// <summary>
        /// 高清修复流程：保边降噪 → 可选 2x 放大 → 温和自动伽马/对比度 →
        /// 多尺度细节重建（拉普拉斯式细节叠加，带边缘门控与高光保护）。
        /// 不追求全局亮度抬升，尽量只恢复细节并限制高光裁剪。
        /// </summary>
        public Mat Repair(Mat image, ImageRepairOptions options)
        {
            if (image.Empty())
                throw new InvalidOperationException("输入图像为空");

            var preset = options.Preset;
            var gain0 = preset switch { RepairPreset.Light => 0.35, RepairPreset.Standard => 0.70, _ => 1.10 };
            var gain1 = preset switch { RepairPreset.Light => 0.20, RepairPreset.Standard => 0.40, _ => 0.65 };
            var threshold0 = preset switch { RepairPreset.Light => 3.0, RepairPreset.Standard => 2.5, _ => 2.0 };
            var threshold1 = preset switch { RepairPreset.Light => 5.0, RepairPreset.Standard => 4.0, _ => 3.5 };
            var targetLuma = preset switch { RepairPreset.Light => 126.0, RepairPreset.Standard => 128.0, _ => 130.0 };

            var gainScale = Math.Clamp(options.Sharpness / 1.2, 0.5, 1.8);
            gain0 *= gainScale;
            gain1 *= gainScale;

            using var lab = new Mat();
            Cv2.CvtColor(image, lab, ColorConversionCodes.BGR2Lab);
            Cv2.Split(lab, out var channels);
            var l = channels[0].Clone();
            var a = channels[1].Clone();
            var b = channels[2].Clone();
            DisposeAll(channels);

            try
            {
                // 只有明显噪声（σ≥15）才做亮度降噪；普通纹理/颗粒仅做彩噪中值，
                // 避免把胶片颗粒、发丝、星空这类高频纹理当成噪声抹平。
                var noiseSigma = EstimateNoiseSigma(l);
                if (noiseSigma >= 15)
                {
                    var lDenoised = new Mat();
                    Cv2.BilateralFilter(l, lDenoised, 7, (int)Math.Clamp(noiseSigma * 2.5, 30, 70), 5);
                    l.Dispose();
                    l = lDenoised;

                    var aDenoised = new Mat();
                    var bDenoised = new Mat();
                    Cv2.MedianBlur(a, aDenoised, 5);
                    Cv2.MedianBlur(b, bDenoised, 5);
                    a.Dispose();
                    b.Dispose();
                    a = aDenoised;
                    b = bDenoised;
                }
                else
                {
                    // 即使亮度干净，也轻压彩噪，避免细节增强后肤色出现色斑
                    var aClean = new Mat();
                    var bClean = new Mat();
                    Cv2.MedianBlur(a, aClean, 3);
                    Cv2.MedianBlur(b, bClean, 3);
                    a.Dispose();
                    b.Dispose();
                    a = aClean;
                    b = bClean;
                }

                var upscaled = false;
                if (options.Upscale2x && (long)image.Width * image.Height <= MaxUpscalePixels)
                {
                    var size = new Size(l.Width * 2, l.Height * 2);
                    var lUp = new Mat();
                    Cv2.Resize(l, lUp, size, 0, 0, InterpolationFlags.Lanczos4);
                    var aUp = new Mat();
                    var bUp = new Mat();
                    Cv2.Resize(a, aUp, size, 0, 0, InterpolationFlags.Cubic);
                    Cv2.Resize(b, bUp, size, 0, 0, InterpolationFlags.Cubic);
                    l.Dispose();
                    a.Dispose();
                    b.Dispose();
                    l = lUp;
                    a = aUp;
                    b = bUp;
                    upscaled = true;
                }

                // 温和色调：只调整亮度通道，避免整体偏色和发白
                using var lFloat = new Mat();
                l.ConvertTo(lFloat, MatType.CV_32F);
                var meanLuma = Cv2.Mean(lFloat)[0];
                var target = Math.Clamp(targetLuma + options.Brightness * 0.4, 110, 145);
                var gamma = 1.0;
                if (meanLuma > 1 && meanLuma < 254)
                    gamma = Math.Log(target / 255.0) / Math.Log(meanLuma / 255.0);
                gamma = Math.Clamp(gamma, 0.92, 1.08);

                using var normalized = new Mat();
                lFloat.ConvertTo(normalized, MatType.CV_32F, 1.0 / 255.0);
                using var powered = new Mat();
                Cv2.Pow(normalized, gamma, powered);

                var contrast = Math.Clamp(options.Contrast, 0.9, 1.15);
                using var tone = new Mat();
                powered.ConvertTo(tone, MatType.CV_32F, 255.0 * contrast, meanLuma * (1.0 - contrast));

                // 多尺度细节重建：fine（sigma≈1.1）+ medium（sigma≈2.2）
                var sigma1 = upscaled ? 1.6 : 1.1;
                var sigma2 = upscaled ? 3.2 : 2.2;
                using var g1 = new Mat();
                Cv2.GaussianBlur(tone, g1, new Size(0, 0), sigma1);
                using var g2 = new Mat();
                Cv2.GaussianBlur(g1, g2, new Size(0, 0), sigma2);

                using var d0 = new Mat();
                Cv2.Subtract(tone, g1, d0);
                using var d1 = new Mat();
                Cv2.Subtract(g1, g2, d1);

                using var guard = BuildHighlightGuard(tone);
                using var boost0 = BuildDetailBoost(d0, threshold0, gain0, guard);
                using var boost1 = BuildDetailBoost(d1, threshold1, gain1, guard);

                using var enhancedLuma = new Mat();
                Cv2.Add(tone, boost0, enhancedLuma);
                Cv2.Add(enhancedLuma, boost1, enhancedLuma);

                using var l8 = new Mat();
                enhancedLuma.ConvertTo(l8, MatType.CV_8U);

                using var merged = new Mat();
                Cv2.Merge(new[] { l8, a, b }, merged);

                var result = new Mat();
                Cv2.CvtColor(merged, result, ColorConversionCodes.Lab2BGR);
                return result;
            }
            finally
            {
                DisposeAll(l, a, b);
            }
        }

        /// <summary>细节增强：幅值门控（抑制平坦区噪声）+ 高光保护 + 限幅增益。</summary>
        private static Mat BuildDetailBoost(Mat detail, double threshold, double gain, Mat highlightGuard)
        {
            using var squared = new Mat();
            Cv2.Multiply(detail, detail, squared);
            using var magnitude = new Mat();
            Cv2.Sqrt(squared, magnitude);

            using var weight = new Mat();
            magnitude.ConvertTo(weight, MatType.CV_32F, 1.0 / Math.Max(threshold, 0.1));
            Cv2.Threshold(weight, weight, 1.0, 1.0, ThresholdTypes.Trunc);

            using var weighted = new Mat();
            Cv2.Multiply(detail, weight, weighted);
            Cv2.Multiply(weighted, highlightGuard, weighted);
            weighted.ConvertTo(weighted, MatType.CV_32F, gain, 0);

            return weighted.Clone();
        }

        /// <summary>高光保护：亮度越高，细节叠加越少，避免高光被推爆。</summary>
        private static Mat BuildHighlightGuard(Mat luma)
        {
            using var ceiling = new Mat(luma.Size(), MatType.CV_32F, Scalar.All(250));
            using var guard = new Mat();
            Cv2.Subtract(ceiling, luma, guard);
            guard.ConvertTo(guard, MatType.CV_32F, 1.0 / 30.0, 0);
            Cv2.Threshold(guard, guard, 0, 0, ThresholdTypes.Tozero);
            Cv2.Threshold(guard, guard, 1.0, 1.0, ThresholdTypes.Trunc);
            return guard.Clone();
        }

        /// <summary>稳健噪声估计：拉普拉斯响应绝对值的 50 分位 × 1.4826。</summary>
        private static double EstimateNoiseSigma(Mat gray)
        {
            using var laplacian = new Mat();
            Cv2.Laplacian(gray, laplacian, MatType.CV_32F);

            using var absLap = new Mat();
            Cv2.ConvertScaleAbs(laplacian, absLap);

            using var histogram = new Mat();
            Cv2.CalcHist(new[] { absLap }, new[] { 0 }, null, histogram,
                1, new[] { 256 }, new[] { new[] { 0f, 256f } });

            var half = absLap.Rows * absLap.Cols / 2.0;
            var accumulated = 0.0;
            for (var value = 0; value < 256; value++)
            {
                accumulated += histogram.At<float>(value);
                if (accumulated >= half)
                    return value * 1.4826;
            }
            return 255 * 1.4826;
        }

        private static void DisposeAll(params Mat[] mats)
        {
            foreach (var mat in mats.Where(m => !m.IsDisposed))
                mat.Dispose();
        }
    }
}
