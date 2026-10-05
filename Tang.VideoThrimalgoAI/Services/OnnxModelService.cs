using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;
using Tang.VideoThrimalgoAI.Models;

namespace Tang.VideoThrimalgoAI.Services
{
    public class BatchProcessingService
    {
        private readonly ImageEnhancementService _enhancementService = new ImageEnhancementService();
        private readonly MosaicRemovalService _mosaicRemovalService = new MosaicRemovalService();
        private readonly WatermarkRemovalService _watermarkRemovalService = new WatermarkRemovalService();

        public async Task<List<string>> ProcessBatchAsync(List<string> filePaths, ProcessingType type, string outputDirectory)
        {
            var results = new List<string>();
            Directory.CreateDirectory(outputDirectory);

            foreach (var file in filePaths.Where(File.Exists))
            {
                using var source = Cv2.ImRead(file, ImreadModes.Color);
                if (source.Empty())
                    continue;

                Mat processed = type switch
                {
                    ProcessingType.ImageEnhancement => await _enhancementService.EnhanceAsync(source, 10, 1.2, 1.1),
                    ProcessingType.SuperResolution => await _enhancementService.EnhanceAsync(source, 10, 1.3, 1.2),
                    ProcessingType.MosaicRemoval => await _mosaicRemovalService.RemoveMosaicAsync(source, 8),
                    ProcessingType.WatermarkRemoval => await _watermarkRemovalService.RemoveWatermarkAsync(source, new Rect(source.Width - 180, source.Height - 140, 180, 140)),
                    _ => source.Clone()
                };

                var outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(file) + "_processed.png");
                Cv2.ImWrite(outputPath, processed);
                results.Add(outputPath);
                processed.Dispose();
            }

            return results;
        }
    }
}
