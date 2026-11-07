using System;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Tang.VideoThrimalgoAI.Services
{
    public class WatermarkRemovalService
    {
        /// <summary>
        /// 默认水印区域：右下角约 30%（上限 260x200）。
        /// 针对小图做了钳制，避免越界导致 Inpaint 异常。
        /// </summary>
        public static Rect GetDefaultRegion(Mat image)
        {
            var width = Math.Min((int)(image.Width * 0.3), 260);
            var height = Math.Min((int)(image.Height * 0.3), 200);
            if (width <= 0 || height <= 0 || width >= image.Width || height >= image.Height)
                return default;

            return new Rect(image.Width - width, image.Height - height, width, height);
        }

        public async Task<Mat> RemoveWatermarkAsync(Mat image, Rect region)
            => await Task.Run(() => RemoveWatermark(image, region));

        public Mat RemoveWatermark(Mat image, Rect region)
        {
            // 区域与图像边界求交，越界或空区域时直接返回原图副本
            var left = Math.Max(region.X, 0);
            var top = Math.Max(region.Y, 0);
            var right = Math.Min(region.X + region.Width, image.Width);
            var bottom = Math.Min(region.Y + region.Height, image.Height);
            if (region.Width <= 0 || region.Height <= 0 || right <= left || bottom <= top)
                return image.Clone();

            var valid = new Rect(left, top, right - left, bottom - top);

            using var mask = Mat.Zeros(image.Size(), MatType.CV_8UC1).ToMat();
            Cv2.Rectangle(mask, valid, new Scalar(255), -1);

            using var inpainted = new Mat();
            Cv2.Inpaint(image, mask, inpainted, 5, InpaintMethod.Telea);

            // 掩模羽化后按透明度混合，消除修复区域与原图之间的硬边痕迹
            using var soft = new Mat();
            Cv2.GaussianBlur(mask, soft, new Size(0, 0), 6);
            return BlendByMask(image, inpainted, soft);
        }

        private static Mat BlendByMask(Mat original, Mat inpainted, Mat softMask)
        {
            var alpha = new Mat();
            softMask.ConvertTo(alpha, MatType.CV_32FC1, 1.0 / 255.0);
            var alpha3 = new Mat();
            Cv2.Merge(new[] { alpha, alpha, alpha }, alpha3);
            alpha.Dispose();

            var one3 = Mat.Ones(alpha3.Size(), MatType.CV_32FC3).ToMat();
            var invAlpha3 = new Mat();
            Cv2.Absdiff(alpha3, one3, invAlpha3);
            one3.Dispose();

            var srcF = new Mat();
            original.ConvertTo(srcF, MatType.CV_32FC3);
            var inpF = new Mat();
            inpainted.ConvertTo(inpF, MatType.CV_32FC3);

            var blended = (srcF.Mul(invAlpha3) + inpF.Mul(alpha3)).ToMat();
            var result = new Mat();
            blended.ConvertTo(result, MatType.CV_8UC3);

            alpha3.Dispose();
            invAlpha3.Dispose();
            srcF.Dispose();
            inpF.Dispose();
            blended.Dispose();
            return result;
        }
    }
}
