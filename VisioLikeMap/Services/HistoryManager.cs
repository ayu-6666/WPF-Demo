using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using VisioLikeWpf48.Models;
namespace VisioLikeWpf48.Services
{
    public sealed class HistoryManager
    {
        private readonly Stack<string> undo=new Stack<string>();
        private readonly Stack<string> redo=new Stack<string>();
        private readonly XmlSerializer serializer=new XmlSerializer(typeof(DiagramModel));
        private string Encode(DiagramModel m){using(var w=new StringWriter()){serializer.Serialize(w,m);return w.ToString();}}
        private DiagramModel Decode(string s){using(var r=new StringReader(s))return (DiagramModel)serializer.Deserialize(r);}
        public void Reset(DiagramModel m){undo.Clear();redo.Clear();undo.Push(Encode(m));}
        public void Commit(DiagramModel m){undo.Push(Encode(m));redo.Clear();if(undo.Count>100){var a=undo.ToArray();undo.Clear();for(int i=a.Length-1;i>=a.Length-100;i--)undo.Push(a[i]);}}
        public bool CanUndo{get{return undo.Count>1;}}
        public bool CanRedo{get{return redo.Count>0;}}
        public DiagramModel Undo(DiagramModel current){if(!CanUndo)return current;redo.Push(undo.Pop());return Decode(undo.Peek());}
        public DiagramModel Redo(DiagramModel current){if(!CanRedo)return current;var s=redo.Pop();undo.Push(s);return Decode(s);}
    }
}