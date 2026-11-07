using System.IO;
using OpenCvSharp;

namespace Tang.VideoThrimalgoAI.Services
{
    /// <summary>
    /// 基于 ImDecode/ImEncode 的图片读写。
    /// Cv2.ImRead/ImWrite 在 Windows 上对中文等非 ASCII 路径支持不可靠，
    /// 这里统一通过字节流编解码，保证任意路径（含中文、空格）读写正确。
    /// </summary>
    public static class ImageFileIO
    {
        public static Mat ReadMat(string path)
        {
            var bytes = File.ReadAllBytes(path);
            return Mat.ImDecode(bytes, ImreadModes.Color);
        }

        public static void WriteMat(Mat image, string path)
        {
            File.WriteAllBytes(path, image.ImEncode(".png"));
        }
    }
}
