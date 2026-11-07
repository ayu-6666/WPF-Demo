using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Tang.VideoThrimalgoAI.Services
{
    /// <summary>
    /// 水平校正（纠偏/拉直）：通过霍夫直线检测估计图像中主导的水平方向倾角，
    /// 以线段长度加权的中位数作为稳健估计，再将图像反向旋转回正。
    /// </summary>
    public class DeskewService
    {
        /// <summary>可接受的最大校正角度，超过视为场景内斜线干扰而非整体倾斜。</summary>
        private const double MaxCorrectionAngle = 30.0;

        public async Task<(Mat Image, double Angle)> CorrectTiltAsync(Mat image)
            => await Task.Run(() => CorrectTilt(image));

        public (Mat Image, double Angle) CorrectTilt(Mat image)
        {
            var angle = EstimateSkewAngle(image);
            if (Math.Abs(angle) < 0.05)
                return (image.Clone(), 0);

            var center = new Point2f(image.Width / 2f, image.Height / 2f);
            var matrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);
            var rotated = new Mat();
            Cv2.WarpAffine(image, rotated, matrix, image.Size(),
                InterpolationFlags.Cubic, BorderTypes.Replicate);
            return (rotated, angle);
        }

        public double EstimateSkewAngle(Mat image)
        {
            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            using var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);
            using var edges = new Mat();
            Cv2.Canny(blurred, edges, 60, 180);

            var minLineLength = Math.Max(40, Math.Min(image.Cols / 4.0, 240));
            var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 1800.0, 60, minLineLength, 12);
            if (lines == null || lines.Length == 0)
                return 0;

            var candidates = new List<(double Angle, double Weight)>();
            foreach (var line in lines)
            {
                double dx = line.P2.X - line.P1.X;
                double dy = line.P2.Y - line.P1.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                double degree = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                // 只统计接近水平的主方向（±45°以内），排除竖直斜线干扰
                if (Math.Abs(degree) <= 45)
                    candidates.Add((degree, length));
            }

            if (candidates.Count == 0)
                return 0;

            var total = candidates.Sum(c => c.Weight);
            if (total <= 0)
                return 0;

            var angle = 0d;
            var accumulated = 0d;
            foreach (var candidate in candidates.OrderBy(c => c.Angle))
            {
                accumulated += candidate.Weight;
                if (accumulated >= total / 2)
                {
                    angle = candidate.Angle;
                    break;
                }
            }

            return Math.Abs(angle) > MaxCorrectionAngle ? 0 : angle;
        }
    }
}
