using Microsoft.Win32;
using VisioLikeWpf48.Models;
using VisioLikeWpf48.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;

namespace VisioLikeWpf48
{
    public partial class MainWindow : Window
    {
        private DiagramModel model=new DiagramModel();
        private readonly HistoryManager history=new HistoryManager();
        private bool restoring;
        public MainWindow()
        {
            InitializeComponent();
            Surface.Model=model;Surface.Changed+=Commit;Surface.Hint+=s=>StatusText.Text=s;
            Loaded+=(s,e)=>{CreateDemo();history.Reset(model);Surface.RenderDiagram();Surface.Fit();UpdateZoom();};
            PreviewKeyDown+=Window_PreviewKeyDown;
            Surface.MouseWheel+=(s,e)=>UpdateZoom();
        }
        private NodeModel NewNode(NodeShape shape,double x,double y,string text)
        {
            double w=150,h=76;if(shape==NodeShape.Ellipse){w=130;h=76;}if(shape==NodeShape.Diamond){w=170;h=110;}if(shape==NodeShape.Document){w=160;h=85;}
            return new NodeModel{Shape=shape,X=x,Y=y,Width=w,Height=h,Text=text};
        }
        private void CreateDemo()
        {
            var a=NewNode(NodeShape.Ellipse,70,190,"开始");
            var b=NewNode(NodeShape.Rectangle,340,190,"需求分析");
            var c=NewNode(NodeShape.Diamond,650,170,"需求明确？");
            var d=NewNode(NodeShape.RoundedRectangle,990,80,"开发实现");
            var e=NewNode(NodeShape.Document,990,330,"补充需求文档");
            b.Tags.Add(new TagModel{Text="分析",Color="#FFFFE6A6"});d.Tags.Add(new TagModel{Text="进行中",Color="#FFCDEFD8"});
            model.Nodes.AddRange(new[]{a,b,c,d,e});
            AddInitialConnection(a,b,"");AddInitialConnection(b,c,"");AddInitialConnection(c,d,"是");AddInitialConnection(c,e,"否");
        }
        private void AddInitialConnection(NodeModel a,NodeModel b,string tag)
        {
            var c=new ConnectionModel{FromNodeId=a.Id,ToNodeId=b.Id,TagText=tag};
            c.FromAnchor=model.Direction==DiagramDirection.LeftToRight?AnchorSide.Right:AnchorSide.Bottom;
            c.ToAnchor=model.Direction==DiagramDirection.LeftToRight?AnchorSide.Left:AnchorSide.Top;
            model.Connections.Add(c);
        }
        private void AddShape_Click(object sender,RoutedEventArgs e)
        {
            var btn=(ToggleButton)sender;var shape=(NodeShape)Enum.Parse(typeof(NodeShape),(string)btn.Tag);
            var n=NewNode(shape,150+(model.Nodes.Count%4)*45,120+(model.Nodes.Count%5)*45,shape.ToString());
            model.Nodes.Add(n);Surface.SelectedNodes.Clear();Surface.SelectedNodes.Add(n.Id);Surface.RenderDiagram();Commit();
            foreach(var child in FindVisualChildren<ToggleButton>(this))if(child.Tag!=null)child.IsChecked=child==btn;
        }
        private System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject dep) where T:DependencyObject
        {
            if(dep==null)yield break;for(int i=0;i<VisualTreeHelper.GetChildrenCount(dep);i++){var c=VisualTreeHelper.GetChild(dep,i);if(c is T)yield return (T)c;foreach(var x in FindVisualChildren<T>(c))yield return x;}
        }
        private NodeModel SingleSelected()
        {
            if(Surface.SelectedNodes.Count!=1)return null;return model.Nodes.FirstOrDefault(n=>n.Id==Surface.SelectedNodes.First());
        }
        private string Prompt(string title,string initial)
        {
            var w=new Window{Title=title,Width=340,Height=145,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};
            var p=new StackPanel{Margin=new Thickness(12)};var t=new TextBox{Text=initial,Padding=new Thickness(5),Margin=new Thickness(0,0,0,9)};
            var b=new Button{Content="确定",Width=72,HorizontalAlignment=HorizontalAlignment.Right,IsDefault=true};b.Click+=(s,e)=>{w.DialogResult=true;w.Close();};p.Children.Add(t);p.Children.Add(b);w.Content=p;return w.ShowDialog()==true?t.Text:null;
        }
        private void EditText_Click(object sender,RoutedEventArgs e){var n=SingleSelected();if(n==null){StatusText.Text="请先单选一个节点。";return;}var s=Prompt("编辑节点文字",n.Text);if(s!=null){n.Text=s;Surface.RenderDiagram();Commit();}}
        private void AddTag_Click(object sender,RoutedEventArgs e){var n=SingleSelected();if(n==null){StatusText.Text="请先单选一个节点。";return;}var s=Prompt("添加节点 Tag","新标签");if(!string.IsNullOrWhiteSpace(s)){n.Tags.Add(new TagModel{Text=s});Surface.RenderDiagram();Commit();}}
        private void Connect_Click(object sender,RoutedEventArgs e){Surface.ConnectMode=true;StatusText.Text="连接模式：依次点击起点节点和目标节点。";}
        private void Delete_Click(object sender,RoutedEventArgs e){Surface.DeleteSelection();}
        private void Commit(){if(!restoring)history.Commit(model);}
        private void Undo_Click(object sender,RoutedEventArgs e){if(!history.CanUndo)return;model=history.Undo(model);RefreshModel();StatusText.Text="已撤销操作。";}
        private void Redo_Click(object sender,RoutedEventArgs e){if(!history.CanRedo)return;model=history.Redo(model);RefreshModel();StatusText.Text="已重做操作。";}
        private void RefreshModel(){restoring=true;Surface.Model=model;Surface.SelectedNodes.Clear();Surface.RenderDiagram();restoring=false;}
        private void HorizontalLayout_Click(object sender,RoutedEventArgs e){HorizontalLayout.IsChecked=true;VerticalLayout.IsChecked=false;Surface.Arrange(DiagramDirection.LeftToRight);StatusText.Text="已切换为从左到右布局。";UpdateZoom();}
        private void VerticalLayout_Click(object sender,RoutedEventArgs e){HorizontalLayout.IsChecked=false;VerticalLayout.IsChecked=true;Surface.Arrange(DiagramDirection.TopToBottom);StatusText.Text="已切换为从上到下布局。";UpdateZoom();}
        private void Fit_Click(object sender,RoutedEventArgs e){Surface.Fit();UpdateZoom();}
        private void Save_Click(object sender,RoutedEventArgs e){var d=new SaveFileDialog{Filter="流程图项目 (*.xml)|*.xml",FileName="流程图.xml"};if(d.ShowDialog()==true){DiagramSerializer.Save(model,d.FileName);StatusText.Text="项目已保存："+d.FileName;}}
        private void Load_Click(object sender,RoutedEventArgs e){var d=new OpenFileDialog{Filter="流程图项目 (*.xml)|*.xml"};if(d.ShowDialog()==true){model=DiagramSerializer.Load(d.FileName);Surface.Model=model;Surface.SelectedNodes.Clear();Surface.RenderDiagram();history.Reset(model);if(model.Direction==DiagramDirection.LeftToRight){HorizontalLayout.IsChecked=true;VerticalLayout.IsChecked=false;}else{HorizontalLayout.IsChecked=false;VerticalLayout.IsChecked=true;}Surface.Fit();UpdateZoom();StatusText.Text="项目已打开。";}}
        private void Export_Click(object sender,RoutedEventArgs e)
        {
            if(model.Nodes.Count==0)return;var d=new SaveFileDialog{Filter="PNG 图片 (*.png)|*.png",FileName="流程图高清.png"};if(d.ShowDialog()!=true)return;
            var input=Prompt("高清导出倍率（1、2、4、8）","4");int factor;if(!int.TryParse(input,out factor)||!(factor==1||factor==2||factor==4||factor==8)){MessageBox.Show("请输入 1、2、4 或 8。");return;}
            double minX=model.Nodes.Min(n=>n.X),minY=model.Nodes.Min(n=>n.Y),maxX=model.Nodes.Max(n=>n.X+n.Width),maxY=model.Nodes.Max(n=>n.Y+n.Height);
            const double margin=45;double w=maxX-minX+margin*2,h=maxY-minY+margin*2;
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White,null,new Rect(0,0,w,h));dc.PushTransform(new TranslateTransform(margin-minX,margin-minY));
                foreach(var c in model.Connections)DrawConnectionExport(dc,c);
                foreach(var n in model.Nodes)DrawNodeExport(dc,n);
                dc.Pop();
            }
            var scaled=new DrawingVisual();using(var dc=scaled.RenderOpen()){dc.PushTransform(new ScaleTransform(factor,factor));dc.DrawDrawing(visual.Drawing);dc.Pop();}
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(w*factor),(int)Math.Ceiling(h*factor),96,96,PixelFormats.Pbgra32);bitmap.Render(scaled);
            var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bitmap));using(var fs=System.IO.File.Create(d.FileName))enc.Save(fs);
            StatusText.Text="PNG 已导出（"+factor+"x）。";
        }
        private Point GetAnchor(NodeModel n,AnchorSide s){switch(s){case AnchorSide.Top:return new Point(n.X+n.Width/2,n.Y);case AnchorSide.Bottom:return new Point(n.X+n.Width/2,n.Y+n.Height);case AnchorSide.Left:return new Point(n.X,n.Y+n.Height/2);default:return new Point(n.X+n.Width,n.Y+n.Height/2);}}
        private void DrawConnectionExport(DrawingContext dc,ConnectionModel c)
        {
            var a=model.Nodes.FirstOrDefault(n=>n.Id==c.FromNodeId);var b=model.Nodes.FirstOrDefault(n=>n.Id==c.ToNodeId);if(a==null||b==null)return;
            Point p1=GetAnchor(a,c.FromAnchor),p2=GetAnchor(b,c.ToAnchor);var points=new System.Collections.Generic.List<Point>{p1};
            if(model.Direction==DiagramDirection.LeftToRight){double mid=(p1.X+p2.X)/2;points.Add(new Point(mid,p1.Y));points.Add(new Point(mid,p2.Y));}else{double mid=(p1.Y+p2.Y)/2;points.Add(new Point(p1.X,mid));points.Add(new Point(p2.X,mid));}points.Add(p2);
            var geo=new StreamGeometry();using(var g=geo.Open()){g.BeginFigure(points[0],false,false);g.PolyLineTo(points.GetRange(1,points.Count-1),true,false);}
            var pen=new Pen(Brushes.SlateGray,2);if(c.IsDashed)pen.DashStyle=DashStyles.Dash;dc.DrawGeometry(null,pen,geo);
            DrawExportArrow(dc,points[points.Count-2],p2,pen.Brush,c.ShowArrow);if(c.Bidirectional)DrawExportArrow(dc,points[1],p1,pen.Brush,true);
            if(!string.IsNullOrWhiteSpace(c.TagText)){var ft=new FormattedText(c.TagText,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,Brushes.DarkSlateGray);dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(239,247,242)),new Pen(Brushes.DarkSeaGreen,1),new Rect((p1.X+p2.X)/2-ft.Width/2-5,(p1.Y+p2.Y)/2-10,ft.Width+10,ft.Height+4),6,6);dc.DrawText(ft,new Point((p1.X+p2.X)/2-ft.Width/2,(p1.Y+p2.Y)/2-8));}
        }
        private void DrawExportArrow(DrawingContext dc,Point from,Point to,Brush brush,bool show){if(!show)return;Vector v=to-from;if(v.Length<.1)return;v.Normalize();Vector n=new Vector(-v.Y,v.X);var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(to,true,true);c.LineTo(to-v*12+n*5,true,false);c.LineTo(to-v*12-n*5,true,false);}dc.DrawGeometry(brush,null,g);}
        private void DrawNodeExport(DrawingContext dc,NodeModel n)
        {
            Rect r=new Rect(n.X,n.Y,n.Width,n.Height);Brush fill=ToBrush(n.Fill);var pen=new Pen(ToBrush(n.Stroke),2);
            switch(n.Shape)
            {
                case NodeShape.Ellipse:dc.DrawEllipse(fill,pen,new Point(r.Left+r.Width/2,r.Top+r.Height/2),r.Width/2,r.Height/2);break;
                case NodeShape.Diamond:var d=new StreamGeometry();using(var g=d.Open()){g.BeginFigure(new Point(r.Left+r.Width/2,r.Top),true,true);g.LineTo(new Point(r.Right,r.Top+r.Height/2),true,false);g.LineTo(new Point(r.Left+r.Width/2,r.Bottom),true,false);g.LineTo(new Point(r.Left,r.Top+r.Height/2),true,false);}dc.DrawGeometry(fill,pen,d);break;
                case NodeShape.RoundedRectangle:dc.DrawRoundedRectangle(fill,pen,r,12,12);break;
                default:dc.DrawRectangle(fill,pen,r);break;
            }
            var ft=new FormattedText(n.Text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),13,Brushes.DarkSlateGray);dc.DrawText(ft,new Point(r.Left+(r.Width-ft.Width)/2,r.Top+(r.Height-ft.Height)/2));
            double tx=r.Left+5,ty=r.Bottom+4;foreach(var t in n.Tags){var tf=new FormattedText(t.Text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),9,Brushes.DarkSlateGray);dc.DrawRoundedRectangle(ToBrush(t.Color),null,new Rect(tx,ty,tf.Width+10,tf.Height+4),5,5);dc.DrawText(tf,new Point(tx+5,ty+2));tx+=tf.Width+15;}
        }
        private Brush ToBrush(string s){try{return (Brush)new BrushConverter().ConvertFromString(s);}catch{return Brushes.LightBlue;}}
        private void UpdateZoom(){ZoomText.Text=Math.Round(Surface.Zoom*100)+"%";}
        private void Window_PreviewKeyDown(object sender,KeyEventArgs e)
        {
            if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.Z){Undo_Click(null,null);e.Handled=true;}
            else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.Y){Redo_Click(null,null);e.Handled=true;}
            else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.A){Surface.SelectAll();e.Handled=true;}
            else if(e.Key==Key.Delete){Surface.DeleteSelection();e.Handled=true;}
        }
        private void Minimize_Click(object s,RoutedEventArgs e){WindowState=WindowState.Minimized;}
        private void Maximize_Click(object s,RoutedEventArgs e){WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;MaxButton.Content=WindowState==WindowState.Maximized?"❐":"□";}
        private void Close_Click(object s,RoutedEventArgs e){Close();}
    }
}