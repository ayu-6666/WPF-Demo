using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace Tang.VideoThrimalgoAI.Services
{
    public class OnnxModelService
    {
        private InferenceSession? _session;

        public async Task<bool> LoadModelAsync(string modelPath)
        {
            if (!File.Exists(modelPath))
                return false;

            try
            {
                _session = await Task.Run(() => new InferenceSession(modelPath));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Mat> RunInferenceAsync(Mat inputImage)
        {
            if (_session == null)
                throw new InvalidOperationException("ONNX model not loaded.");

            return await Task.Run(() => inputImage.Clone());
        }
    }
}
