using System.IO;
using System.Xml.Serialization;
using MindMapWpf48.Models;

namespace MindMapWpf48.Services
{
    public static class DiagramFileService
    {
        public static void Save(DiagramModel model, string path)
        {
            var serializer = new XmlSerializer(typeof(DiagramModel));
            using (var stream = File.Create(path)) serializer.Serialize(stream, model);
        }
        public static DiagramModel Load(string path)
        {
            var serializer = new XmlSerializer(typeof(DiagramModel));
            using (var stream = File.OpenRead(path)) return (DiagramModel)serializer.Deserialize(stream);
        }
    }
}