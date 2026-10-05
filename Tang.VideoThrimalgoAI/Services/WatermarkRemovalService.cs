using System.Threading.Tasks;
using OpenCvSharp;

namespace Tang.VideoThrimalgoAI.Services
{
    public class MosaicRemovalService
    {
        public async Task<Mat> RemoveMosaicAsync(Mat image, int blockSize)
        {
            return await Task.Run(() =>
            {
                var filtered = new Mat();
                Cv2.BilateralFilter(image, filtered, blockSize * 2 + 1, 75, 75);

                var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(blockSize, blockSize));
                var result = new Mat();
                Cv2.MorphologyEx(filtered, result, MorphTypes.Close, kernel);

                kernel.Dispose();
                filtered.Dispose();
                return result;
            });
        }
    }
}
