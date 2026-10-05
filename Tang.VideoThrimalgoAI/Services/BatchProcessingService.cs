using System.Threading.Tasks;
using OpenCvSharp;

namespace Tang.VideoThrimalgoAI.Services
{
    public class WatermarkRemovalService
    {
        public async Task<Mat> RemoveWatermarkAsync(Mat image, Rect region)
        {
            return await Task.Run(() =>
            {
                var mask = new Mat(image.Size(), MatType.CV_8UC1, new Scalar(0));
                Cv2.Rectangle(mask, region, new Scalar(255), -1);

                var result = new Mat();
                Cv2.Inpaint(image, mask, result, 3, InpaintMethod.Telea);

                mask.Dispose();
                return result;
            });
        }
    }
}
