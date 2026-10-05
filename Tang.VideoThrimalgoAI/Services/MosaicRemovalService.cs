using System;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Tang.VideoThrimalgoAI.Services
{
    public class ImageEnhancementService
    {
        public async Task<Mat> EnhanceAsync(Mat image, double brightness, double contrast, double saturation)
        {
            return await Task.Run(() =>
            {
                var result = new Mat();
                image.ConvertTo(result, -1, contrast, brightness);

                var hsv = new Mat();
                Cv2.CvtColor(result, hsv, ColorConversionCodes.BGR2HSV);
                Cv2.Split(hsv, out Mat[] channels);
                channels[1].ConvertTo(channels[1], -1, saturation);
                Cv2.Merge(channels, hsv);

                var rgb = new Mat();
                Cv2.CvtColor(hsv, rgb, ColorConversionCodes.HSV2BGR);

                var blurred = new Mat();
                Cv2.GaussianBlur(rgb, blurred, new Size(3, 3), 0);

                var sharpened = new Mat();
                Cv2.AddWeighted(rgb, 1.25, blurred, -0.15, 0, sharpened);

                foreach (var channel in channels)
                {
                    channel.Dispose();
                }

                hsv.Dispose();
                rgb.Dispose();
                blurred.Dispose();

                return sharpened;
            });
        }
    }
}
