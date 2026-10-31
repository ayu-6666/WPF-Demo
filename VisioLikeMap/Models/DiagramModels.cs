using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace VisioLikeWpf48.Models
{
    public enum NodeShape { Rectangle, RoundedRectangle, Ellipse, Diamond, Parallelogram, Document }
    public enum AnchorSide { Top, Right, Bottom, Left }
    public enum DiagramDirection { LeftToRight, TopToBottom }

    [Serializable]
    public class TagModel
    {
        public string Text { get; set; }
        public string Color { get; set; }
        public TagModel() { Text = ""; Color = "#FFFFE6A6"; }
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
            Id=Guid.NewGuid(); Text="新任务"; Shape=NodeShape.Rectangle;
            Width=150; Height=76; Fill="#FFEAF2FF"; Stroke="#FF3976C5";
            Status=""; Tags=new List<TagModel>();
        }
    }

    [Serializable]
    public class ConnectionModel
    {
        public Guid Id { get; set; }
        public Guid FromNodeId { get; set; }
        public Guid ToNodeId { get; set; }
        public AnchorSide FromAnchor { get; set; }
        public AnchorSide ToAnchor { get; set; }
        public string TagText { get; set; }
        public bool ShowArrow { get; set; }
        public bool Bidirectional { get; set; }
        public bool IsDashed { get; set; }
        public bool Orthogonal { get; set; }
        public ConnectionModel()
        {
            Id=Guid.NewGuid(); TagText=""; ShowArrow=true; Bidirectional=false;
            IsDashed=false; Orthogonal=true; FromAnchor=AnchorSide.Right; ToAnchor=AnchorSide.Left;
        }
    }

    [Serializable]
    [XmlRoot("Diagram")]
    public class DiagramModel
    {
        public DiagramDirection Direction { get; set; }
        [XmlArray] public List<NodeModel> Nodes { get; set; }
        [XmlArray] public List<ConnectionModel> Connections { get; set; }
        public DiagramModel() { Direction=DiagramDirection.LeftToRight; Nodes=new List<NodeModel>(); Connections=new List<ConnectionModel>(); }
    }
}