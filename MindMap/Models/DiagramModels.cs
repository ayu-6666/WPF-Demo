using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace MindMapWpf48.Models
{
    public enum NodeShape { Rectangle, Square, RoundedRectangle, Ellipse, Diamond }
    [Serializable]
    public class TagModel
    {
        public string Text { get; set; }
        public string Color { get; set; }
        public TagModel() { Text = ""; Color = "#FFE8A3"; }
    }
    [Serializable]
    public class NodeModel
    {
        public Guid Id { get; set; }
        public string Text { get; set; }
        public NodeShape Shape { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Fill { get; set; }
        public string Stroke { get; set; }
        public string Status { get; set; }
        [XmlArray] public List<TagModel> Tags { get; set; }
        public NodeModel()
        {
            Id = Guid.NewGuid(); Text = "新任务"; Shape = NodeShape.Rectangle;
            Width = 150; Height = 72; Fill = "#FFEAF2FF"; Stroke = "#FF3976C5";
            Status = "待处理"; Tags = new List<TagModel>();
        }
    }
    [Serializable]
    public class ConnectionModel
    {
        public Guid Id { get; set; }
        public Guid FromNodeId { get; set; }
        public Guid ToNodeId { get; set; }
        public string Label { get; set; }
        public bool ShowArrow { get; set; }
        public ConnectionModel() { Id = Guid.NewGuid(); Label = ""; ShowArrow = true; }
    }
    [Serializable]
    [XmlRoot("Diagram")]
    public class DiagramModel
    {
        [XmlArray] public List<NodeModel> Nodes { get; set; }
        [XmlArray] public List<ConnectionModel> Connections { get; set; }
        public DiagramModel() { Nodes = new List<NodeModel>(); Connections = new List<ConnectionModel>(); }
    }
}