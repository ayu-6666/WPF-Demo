using System.Threading.Tasks;
using OpenCvSharp;

namespace Tang.VideoThrimalgoAI.Services
{
    public class MosaicRemovalService
    {
        public async Task<Mat> RemoveMosaicAsync(Mat image, int blockSize)
            => await Task.Run(() => RemoveMosaic(image, blockSize));

        public Mat RemoveMosaic(Mat image, int blockSize)
        {
            using var filtered = new Mat();
            Cv2.BilateralFilter(image, filtered, blockSize * 2 + 1, 75, 75);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(blockSize, blockSize));
            using var closed = new Mat();
            Cv2.MorphologyEx(filtered, closed, MorphTypes.Close, kernel);

            // 形态学闭运算后做一次轻度锐化，补偿平滑造成的细节损失
            using var blurred = new Mat();
            Cv2.GaussianBlur(closed, blurred, new Size(0, 0), 1.0);
            var result = new Mat();
            Cv2.AddWeighted(closed, 1.2, blurred, -0.2, 0, result);
            return result;
        }
    }
}
