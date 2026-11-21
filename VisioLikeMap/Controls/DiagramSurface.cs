using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VisioLikeWpf48.Models;
using VisioLikeWpf48.Services;

namespace VisioLikeWpf48.Controls
{
    public class DiagramSurface : Canvas
    {
        public DiagramModel Model { get; set; }
        public HashSet<Guid> SelectedNodes { get; private set; } = new HashSet<Guid>();
        public Guid? SelectedConnection { get; private set; }
        public double Zoom { get; private set; } = 1;
        public bool ConnectMode { get; set; }
        public event Action Changed;
        public event Action SelectionChanged;
        public event Action<string> Hint;

        private readonly ScaleTransform scale=new ScaleTransform(1,1);
        private readonly TranslateTransform translate=new TranslateTransform();
        private readonly TransformGroup transform=new TransformGroup();
        private Rectangle selectionBox;
        private bool boxSelecting, draggingNodes, panning, draggingEndpoint;
        private bool endpointFrom;
        private Point downWorld, lastScreen;
        private Dictionary<Guid,Point> moveOrigins=new Dictionary<Guid,Point>();
        private Guid? pendingFrom;
        private Guid? activeConnection;
        private Point endpointDragWorld;
        private NodeModel snapNode;
        private AnchorSide snapSide;

        public DiagramSurface()
        {
            Background=Brushes.White; ClipToBounds=true; Focusable=true;
            transform.Children.Add(scale); transform.Children.Add(translate); RenderTransform=transform;
            selectionBox=new Rectangle{Stroke=Brushes.DodgerBlue,StrokeThickness=1,StrokeDashArray=new DoubleCollection{4,2},Fill=new SolidColorBrush(Color.FromArgb(28,30,120,230)),Visibility=Visibility.Collapsed,IsHitTestVisible=false};
            Children.Add(selectionBox);
            MouseLeftButtonDown+=CanvasDown; MouseMove+=CanvasMove; MouseLeftButtonUp+=CanvasUp;
            MouseWheel+=CanvasWheel; PreviewKeyDown+=CanvasKeyDown;
        }
        public Point ToWorld(Point p){return transform.Inverse.Transform(p);}
        public void SetZoom(double z,Point? at=null)
        {
            z=Math.Max(.2,Math.Min(3,z));Point p=at??new Point(ActualWidth/2,ActualHeight/2);Point w=ToWorld(p);
            Zoom=z;scale.ScaleX=scale.ScaleY=z;Point q=transform.Transform(w);translate.X+=p.X-q.X;translate.Y+=p.Y-q.Y;RenderDiagram();
        }
        public void Fit()
        {
            if(Model==null||Model.Nodes.Count==0){Zoom=1;scale.ScaleX=scale.ScaleY=1;translate.X=translate.Y=20;RenderDiagram();return;}
            double minX=Model.Nodes.Min(n=>n.X),minY=Model.Nodes.Min(n=>n.Y),maxX=Model.Nodes.Max(n=>n.X+n.Width),maxY=Model.Nodes.Max(n=>n.Y+n.Height);
            double z=Math.Min((ActualWidth-70)/Math.Max(1,maxX-minX),(ActualHeight-70)/Math.Max(1,maxY-minY));z=Math.Max(.2,Math.Min(2,z));
            Zoom=z;scale.ScaleX=scale.ScaleY=z;translate.X=35-minX*z;translate.Y=35-minY*z;RenderDiagram();
        }
        public void RenderDiagram()
        {
            Children.Clear();Children.Add(selectionBox);
            if(Model==null)return;
            // 每次重绘前统一重算自动描点：节点移动/尺寸变化后连线立即跟随，手动指定的端保持不变。
            foreach(var c in Model.Connections.ToList())
            {
                var a=Model.Nodes.FirstOrDefault(n=>n.Id==c.FromNodeId);
                var b=Model.Nodes.FirstOrDefault(n=>n.Id==c.ToNodeId);
                ConnectionGeometry.Resolve(c,a,b);
            }
            foreach(var c in Model.Connections)DrawConnection(c);
            foreach(var n in Model.Nodes)DrawNode(n);
            if(draggingEndpoint&&snapNode!=null)
            {
                Point p=Anchor(snapNode,snapSide);
                var ring=new Ellipse{Width=18,Height=18,Stroke=Brushes.ForestGreen,StrokeThickness=2.5,Fill=new SolidColorBrush(Color.FromArgb(60,35,139,87)),IsHitTestVisible=false};
                Canvas.SetLeft(ring,p.X-9);Canvas.SetTop(ring,p.Y-9);Children.Add(ring);
            }
            Panel.SetZIndex(selectionBox,99999);
        }
        private Brush B(string s){try{return (Brush)new BrushConverter().ConvertFromString(s);}catch{return Brushes.LightBlue;}}
        private Point Anchor(NodeModel n,AnchorSide s){return ConnectionGeometry.AnchorPoint(n,s);}
        private List<Point> Route(Point a,Point b,AnchorSide from,AnchorSide to){return ConnectionGeometry.BuildRoute(a,b,from,to);}
        private void DrawConnection(ConnectionModel c)
        {
            var a=Model.Nodes.FirstOrDefault(n=>n.Id==c.FromNodeId);var b=Model.Nodes.FirstOrDefault(n=>n.Id==c.ToNodeId);if(a==null||b==null)return;
            Point p1=Anchor(a,c.FromAnchor),p2=Anchor(b,c.ToAnchor);
            if(draggingEndpoint && activeConnection==c.Id)
            {
                if(endpointFrom) p1=endpointDragWorld; else p2=endpointDragWorld;
            }
            var pts=Route(p1,p2,c.FromAnchor,c.ToAnchor);
            var geo=new StreamGeometry();using(var g=geo.Open()){g.BeginFigure(pts[0],false,false);g.PolyLineTo(pts.Skip(1).ToList(),true,false);}
            bool selected=SelectedConnection==c.Id;
            var pen=new Pen(selected?new SolidColorBrush(Color.FromRgb(34,155,85)):new SolidColorBrush(Color.FromRgb(83,105,132)),selected?3.5:2.2);
            if(c.IsDashed)pen.DashStyle=DashStyles.Dash;
            var path=new Path{Data=geo,Stroke=pen.Brush,StrokeThickness=pen.Thickness,StrokeDashArray=c.IsDashed?new DoubleCollection{6,4}:null,Tag=c.Id,Cursor=Cursors.Hand};
            path.MouseLeftButtonDown+=(s,e)=>{SelectConnection(c.Id);e.Handled=true;};
            path.MouseRightButtonDown+=(s,e)=>{SelectConnection(c.Id);ShowConnectionMenu(c);e.Handled=true;};
            Children.Add(path);
            DrawArrowHead(pts[pts.Count-2],p2,pen.Brush,c.ShowArrow);
            if(c.Bidirectional)DrawArrowHead(pts[1],p1,pen.Brush,true);
            if(!string.IsNullOrWhiteSpace(c.TagText))
            {
                Point m=ConnectionGeometry.MidPoint(pts);var tag=new Border{Background=selected?new SolidColorBrush(Color.FromRgb(220,250,228)):Brushes.White,BorderBrush=selected?Brushes.ForestGreen:new SolidColorBrush(Color.FromRgb(210,218,228)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(7,2,7,2),Child=new TextBlock{Text=c.TagText,FontSize=11,Foreground=Brushes.DarkSlateGray},Cursor=Cursors.Hand,Tag=c.Id};
                Canvas.SetLeft(tag,m.X-tag.ActualWidth/2);Canvas.SetTop(tag,m.Y-22);tag.MouseLeftButtonDown+=(s,e)=>{SelectConnection(c.Id);e.Handled=true;};tag.MouseRightButtonDown+=(s,e)=>{SelectConnection(c.Id);ShowConnectionMenu(c);e.Handled=true;};Children.Add(tag);
                tag.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));Canvas.SetLeft(tag,m.X-tag.DesiredSize.Width/2);Canvas.SetTop(tag,m.Y-22);
            }
            if(selected)
            {
                DrawEndpointHandle(p1,true,c.FromAnchorManual);DrawEndpointHandle(p2,false,c.ToAnchorManual);
            }
        }
        private void DrawArrowHead(Point prev,Point tip,Brush brush,bool show)
        {
            if(!show)return;Vector v=tip-prev;if(v.Length<.1)return;v.Normalize();Vector n=new Vector(-v.Y,v.X);
            Children.Add(new Polygon{Points=new PointCollection{tip,tip-v*12+n*5,tip-v*12-n*5},Fill=brush,IsHitTestVisible=false});
        }
        private void DrawEndpointHandle(Point p,bool from,bool manual)
        {
            Brush stroke=manual?new SolidColorBrush(Color.FromRgb(226,146,34)):Brushes.ForestGreen;
            var h=new Ellipse{Width=12,Height=12,Fill=Brushes.White,Stroke=stroke,StrokeThickness=2,Cursor=Cursors.SizeAll,Tag=from?"FROM":"TO",ToolTip=manual?"手动描点 · 拖动可重新指定":"自动描点 · 拖动可手动指定"};
            Canvas.SetLeft(h,p.X-6);Canvas.SetTop(h,p.Y-6);
            h.MouseLeftButtonDown+=(s,e)=>{draggingEndpoint=true;endpointFrom=from;activeConnection=SelectedConnection;endpointDragWorld=ToWorld(e.GetPosition(this));snapNode=null;CaptureMouse();e.Handled=true;};
            Children.Add(h);
        }
        private void DrawNode(NodeModel n)
        {
            var panel=new Grid{Width=n.Width,Height=n.Height,Tag=n.Id,Background=Brushes.Transparent,Cursor=Cursors.SizeAll};
            Shape sh;
            switch(n.Shape)
            {
                case NodeShape.Ellipse:sh=new Ellipse();break;
                case NodeShape.Diamond:sh=new Polygon{Points=new PointCollection{new Point(n.Width/2,0),new Point(n.Width,n.Height/2),new Point(n.Width/2,n.Height),new Point(0,n.Height/2)}};break;
                case NodeShape.RoundedRectangle:sh=new Rectangle{RadiusX=12,RadiusY=12};break;
                case NodeShape.Parallelogram:sh=new Polygon{Points=new PointCollection{new Point(20,0),new Point(n.Width,0),new Point(n.Width-20,n.Height),new Point(0,n.Height)}};break;
                case NodeShape.Document:sh=new Path{Data=Geometry.Parse("M0,0 L150,0 L150,60 Q115,78 75,60 Q35,42 0,60 Z")};break;
                default:sh=new Rectangle();break;
            }
            bool selected=SelectedNodes.Contains(n.Id);
            sh.Fill=B(n.Fill);sh.Stroke=selected?Brushes.ForestGreen:B(n.Stroke);sh.StrokeThickness=selected?3.5:2;
            panel.Children.Add(sh);
            var stack=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(8),IsHitTestVisible=false};
            stack.Children.Add(new TextBlock{Text=n.Text,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(35,48,66))});
            if(!string.IsNullOrWhiteSpace(n.Status))stack.Children.Add(new TextBlock{Text=n.Status,FontSize=10,Foreground=Brushes.DimGray,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,3,0,0)});
            if(n.Tags!=null&&n.Tags.Count>0)
            {
                var wp=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,4,0,0)};
                foreach(var t in n.Tags)wp.Children.Add(new Border{Background=B(t.Color),BorderBrush=selected?Brushes.ForestGreen:Brushes.Transparent,BorderThickness=selected?new Thickness(1.5):new Thickness(0),CornerRadius=new CornerRadius(7),Padding=new Thickness(5,1,5,1),Margin=new Thickness(2,1,2,1),Child=new TextBlock{Text=t.Text,FontSize=9,Foreground=Brushes.DarkSlateGray}});
                stack.Children.Add(wp);
            }
            panel.Children.Add(stack);Canvas.SetLeft(panel,n.X);Canvas.SetTop(panel,n.Y);Children.Add(panel);
            panel.MouseLeftButtonDown+=(s,e)=>NodeDown(n,e);
            panel.MouseRightButtonDown+=(s,e)=>{SelectedNodes.Clear();SelectedNodes.Add(n.Id);RenderDiagram();ShowNodeMenu(n);e.Handled=true;};
            foreach(AnchorSide side in Enum.GetValues(typeof(AnchorSide)))
            {
                Point p=Anchor(n,side);var port=new Ellipse{Width=9,Height=9,Fill=Brushes.White,Stroke=selected?Brushes.ForestGreen:Brushes.DodgerBlue,StrokeThickness=1.5,IsHitTestVisible=false};
                Canvas.SetLeft(port,p.X-4.5);Canvas.SetTop(port,p.Y-4.5);Children.Add(port);
            }
        }
        private void NodeDown(NodeModel n,MouseButtonEventArgs e)
        {
            Focus();
            if(ConnectMode)
            {
                if(!pendingFrom.HasValue){pendingFrom=n.Id;Hint?.Invoke("已选择起点，请点击目标节点。");}
                else if(pendingFrom.Value!=n.Id){CreateConnection(pendingFrom.Value,n.Id);pendingFrom=null;ConnectMode=false;Hint?.Invoke("连接创建完成。");}
                e.Handled=true;return;
            }
            if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){if(!SelectedNodes.Add(n.Id))SelectedNodes.Remove(n.Id);}
            else if(!SelectedNodes.Contains(n.Id)){SelectedNodes.Clear();SelectedNodes.Add(n.Id);}
            SelectedConnection=null;moveOrigins.Clear();
            foreach(var id in SelectedNodes){var x=Model.Nodes.FirstOrDefault(q=>q.Id==id);if(x!=null)moveOrigins[id]=new Point(x.X,x.Y);}
            downWorld=ToWorld(e.GetPosition(this));draggingNodes=true;CaptureMouse();SelectionChanged?.Invoke();RenderDiagram();e.Handled=true;
        }
        private void CanvasDown(object sender,MouseButtonEventArgs e)
        {
            Focus();Point screen=e.GetPosition(this);lastScreen=screen;downWorld=ToWorld(screen);
            if(Keyboard.IsKeyDown(Key.Space)||e.MiddleButton==MouseButtonState.Pressed){panning=true;CaptureMouse();e.Handled=true;return;}
            if(e.OriginalSource==this||e.OriginalSource==selectionBox)
            {
                SelectedConnection=null;if(!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))SelectedNodes.Clear();
                boxSelecting=true;Canvas.SetLeft(selectionBox,downWorld.X);Canvas.SetTop(selectionBox,downWorld.Y);selectionBox.Width=0;selectionBox.Height=0;selectionBox.Visibility=Visibility.Visible;CaptureMouse();RenderDiagram();e.Handled=true;
            }
        }
        private void CanvasMove(object sender,MouseEventArgs e)
        {
            Point screen=e.GetPosition(this),world=ToWorld(screen);
            if(draggingEndpoint&&activeConnection.HasValue&&e.LeftButton==MouseButtonState.Pressed)
            {
                endpointDragWorld=world;
                var active=Model.Connections.FirstOrDefault(x=>x.Id==activeConnection.Value);
                if(active!=null)
                {
                    Guid excluded=endpointFrom?active.ToNodeId:active.FromNodeId;
                    FindNearestAnchor(world,excluded,24,out snapNode,out snapSide);
                }
                RenderDiagram();
                return;
            }
            if(panning&&e.LeftButton==MouseButtonState.Pressed){translate.X+=screen.X-lastScreen.X;translate.Y+=screen.Y-lastScreen.Y;lastScreen=screen;return;}
            if(draggingNodes&&e.LeftButton==MouseButtonState.Pressed)
            {
                double dx=world.X-downWorld.X,dy=world.Y-downWorld.Y;
                foreach(var kv in moveOrigins){var n=Model.Nodes.FirstOrDefault(q=>q.Id==kv.Key);if(n!=null){n.X=kv.Value.X+dx;n.Y=kv.Value.Y+dy;}}
                RenderDiagram();return;
            }
            if(boxSelecting&&e.LeftButton==MouseButtonState.Pressed)
            {
                Canvas.SetLeft(selectionBox,Math.Min(downWorld.X,world.X));Canvas.SetTop(selectionBox,Math.Min(downWorld.Y,world.Y));
                selectionBox.Width=Math.Abs(world.X-downWorld.X);selectionBox.Height=Math.Abs(world.Y-downWorld.Y);
                Rect r=new Rect(downWorld,world);
                foreach(var n in Model.Nodes)if(r.IntersectsWith(new Rect(n.X,n.Y,n.Width,n.Height)))SelectedNodes.Add(n.Id);
                RenderDiagram();
            }
        }
        private void CanvasUp(object sender,MouseButtonEventArgs e)
        {
            if(draggingEndpoint&&activeConnection.HasValue)
            {
                var c=Model.Connections.FirstOrDefault(x=>x.Id==activeConnection.Value);
                NodeModel target; AnchorSide targetSide;
                Guid excluded=c==null?Guid.Empty:(endpointFrom?c.ToNodeId:c.FromNodeId);
                bool snapped=FindNearestAnchor(endpointDragWorld,excluded,24,out target,out targetSide);
                if(c!=null&&snapped&&target!=null)
                {
                    if(endpointFrom){c.FromNodeId=target.Id;c.FromAnchor=targetSide;c.FromAnchorManual=true;}
                    else {c.ToNodeId=target.Id;c.ToAnchor=targetSide;c.ToAnchorManual=true;}
                    Hint?.Invoke("已手动指定描点，移动图形后保持不变。");
                    Changed?.Invoke();
                }
                draggingEndpoint=false;activeConnection=null;snapNode=null;
            }
            bool changed=draggingNodes;draggingNodes=false;boxSelecting=false;panning=false;selectionBox.Visibility=Visibility.Collapsed;ReleaseMouseCapture();RenderDiagram();
            if(changed)Changed?.Invoke();SelectionChanged?.Invoke();
        }
        private NodeModel NearestNode(Point p,double tol)
        {
            return Model.Nodes.Where(n=>p.X>=n.X-tol&&p.X<=n.X+n.Width+tol&&p.Y>=n.Y-tol&&p.Y<=n.Y+n.Height+tol)
                .OrderBy(n=>RectDistance(p,n)).FirstOrDefault();
        }
        private bool FindNearestAnchor(Point p, Guid excludedNodeId, double tolerance, out NodeModel node, out AnchorSide side)
        {
            node=null; side=AnchorSide.Left; double best=tolerance;
            foreach(var n in Model.Nodes)
            {
                if(n.Id==excludedNodeId) continue;
                foreach(AnchorSide candidate in Enum.GetValues(typeof(AnchorSide)))
                {
                    Point q=Anchor(n,candidate); double dx=q.X-p.X,dy=q.Y-p.Y;
                    double d=Math.Sqrt(dx*dx+dy*dy);
                    if(d<=best){best=d;node=n;side=candidate;}
                }
            }
            return node!=null;
        }
        private double RectDistance(Point p,NodeModel n){double dx=Math.Max(Math.Max(n.X-p.X,0),p.X-n.X-n.Width),dy=Math.Max(Math.Max(n.Y-p.Y,0),p.Y-n.Y-n.Height);return Math.Sqrt(dx*dx+dy*dy);}
        private void CanvasWheel(object s,MouseWheelEventArgs e){SetZoom(Zoom*(e.Delta>0?1.1:1/1.1),e.GetPosition(this));e.Handled=true;}
        private void CanvasKeyDown(object s,KeyEventArgs e){if(e.Key==Key.Escape){ConnectMode=false;pendingFrom=null;SelectedNodes.Clear();SelectedConnection=null;RenderDiagram();}}
        public void SetZoom(double z,Point at){z=Math.Max(.2,Math.Min(3,z));Point w=ToWorld(at);Zoom=z;scale.ScaleX=scale.ScaleY=z;Point q=transform.Transform(w);translate.X+=at.X-q.X;translate.Y+=at.Y-q.Y;RenderDiagram();}
        public void SelectAll(){SelectedNodes.Clear();foreach(var n in Model.Nodes)SelectedNodes.Add(n.Id);RenderDiagram();SelectionChanged?.Invoke();}
        public void DeleteSelection()
        {
            if(SelectedConnection.HasValue){Model.Connections.RemoveAll(c=>c.Id==SelectedConnection.Value);SelectedConnection=null;}
            else if(SelectedNodes.Count>0){Model.Nodes.RemoveAll(n=>SelectedNodes.Contains(n.Id));Model.Connections.RemoveAll(c=>SelectedNodes.Contains(c.FromNodeId)||SelectedNodes.Contains(c.ToNodeId));SelectedNodes.Clear();}
            RenderDiagram();Changed?.Invoke();
        }
        public void CreateConnection(Guid from,Guid to)
        {
            if(from==to)return;var a=Model.Nodes.FirstOrDefault(n=>n.Id==from);var b=Model.Nodes.FirstOrDefault(n=>n.Id==to);if(a==null||b==null)return;
            var c=new ConnectionModel{FromNodeId=from,ToNodeId=to};ConnectionGeometry.Resolve(c,a,b);Model.Connections.Add(c);SelectedConnection=c.Id;RenderDiagram();Changed?.Invoke();
        }
        private void SelectConnection(Guid id){SelectedConnection=id;SelectedNodes.Clear();RenderDiagram();SelectionChanged?.Invoke();}

        private sealed class MenuIcons
        {
            public const string Edit="M2,12 L11,3 L13,5 L4,14 L2,14 Z";
            public const string Tag="M2,8 L8,2 L13.5,2 L13.5,7.5 L8,13.5 Z";
            public const string Trash="M3,4 L13,4 M5,4 L5,13 L11,13 L11,4 M6.5,4 L6.5,2 L9.5,2 L9.5,4";
            public const string Swap="M2,5 L12,5 M9,2 L12,5 L9,8 M12,11 L2,11 M5,8 L2,11 L5,14";
            public const string Dash="M2,8 L14,8";
            public const string DoubleArrow="M2,8 L14,8 M5,5 L2,8 L5,11 M11,5 L14,8 L11,11";
            public const string Refresh="M13,8 A5,5 0 1 1 9.2,3.2 M9.2,3.2 L12,1.4 M9.2,3.2 L12,6";
            public const string Anchor="M8,3.2 A2.2,2.2 0 1 0 8,7.6 A2.2,2.2 0 1 0 8,3.2 M8,7.6 L8,13.2 M4.4,11 A3.8,3.8 0 0 0 11.6,11";
        }

        private static ContextMenu NewMenu(){return new ContextMenu{PlacementTarget=null,Placement=System.Windows.Controls.Primitives.PlacementMode.MousePoint};}
        private static System.Windows.Shapes.Path MenuIcon(string data)
        {
            return new System.Windows.Shapes.Path{Data=Geometry.Parse(data),Width=13,Height=13,Stretch=Stretch.Uniform,Stroke=new SolidColorBrush(Color.FromRgb(102,122,148)),StrokeThickness=1.4,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};
        }
        private static Separator MenuSeparator()
        {
            var style=Application.Current!=null?Application.Current.TryFindResource("MenuSeparatorStyle") as Style:null;
            return new Separator{Style=style};
        }
        private static MenuItem MenuAction(string header,string iconData,Action action,bool enabled=true)
        {
            var item=new MenuItem{Header=header,IsEnabled=enabled};
            if(iconData!=null)item.Icon=MenuIcon(iconData);
            if(action!=null)item.Click+=(s,e)=>action();
            return item;
        }
        private void ShowConnectionMenu(ConnectionModel c)
        {
            var m=NewMenu();
            m.Items.Add(BuildAnchorMenu("起点描点",c,true));
            m.Items.Add(BuildAnchorMenu("终点描点",c,false));
            m.Items.Add(MenuSeparator());
            m.Items.Add(MenuAction("恢复自动描点（最短）",MenuIcons.Refresh,()=>RestoreAutoAnchors(c),c.FromAnchorManual||c.ToAnchorManual));
            m.Items.Add(MenuSeparator());
            m.Items.Add(MenuAction("反转箭头方向",MenuIcons.Swap,()=>{
                var id=c.FromNodeId;c.FromNodeId=c.ToNodeId;c.ToNodeId=id;
                var side=c.FromAnchor;c.FromAnchor=c.ToAnchor;c.ToAnchor=side;
                var manual=c.FromAnchorManual;c.FromAnchorManual=c.ToAnchorManual;c.ToAnchorManual=manual;
                RenderDiagram();Changed?.Invoke();}));
            m.Items.Add(MenuAction(c.IsDashed?"改为实线":"改为虚线",MenuIcons.Dash,()=>{c.IsDashed=!c.IsDashed;RenderDiagram();Changed?.Invoke();},true));
            m.Items.Add(MenuAction(c.Bidirectional?"改为单向箭头":"改为双向箭头",MenuIcons.DoubleArrow,()=>{c.Bidirectional=!c.Bidirectional;RenderDiagram();Changed?.Invoke();}));
            m.Items.Add(MenuAction("编辑连接 Tag…",MenuIcons.Tag,()=>{var s=PromptText("编辑连接标签",c.TagText);if(s!=null){c.TagText=s;RenderDiagram();Changed?.Invoke();}}));
            m.Items.Add(MenuSeparator());
            m.Items.Add(MenuAction("删除连接",MenuIcons.Trash,()=>{Model.Connections.Remove(c);SelectedConnection=null;RenderDiagram();Changed?.Invoke();}));
            m.PlacementTarget=this;m.IsOpen=true;
        }
        private MenuItem BuildAnchorMenu(string header,ConnectionModel c,bool from)
        {
            var parent=new MenuItem{Header=header,Icon=MenuIcon(MenuIcons.Anchor)};
            bool manual=from?c.FromAnchorManual:c.ToAnchorManual;
            AnchorSide current=from?c.FromAnchor:c.ToAnchor;
            string[] labels={"自动描点（最短）","上边描点","右边描点","下边描点","左边描点"};
            AnchorSide?[] sides={null,AnchorSide.Top,AnchorSide.Right,AnchorSide.Bottom,AnchorSide.Left};
            for(int i=0;i<labels.Length;i++)
            {
                AnchorSide? side=sides[i];
                var item=new MenuItem{Header=labels[i],IsCheckable=true,IsChecked=side.HasValue?(manual&&current==side.Value):!manual};
                item.Click+=(s,e)=>
                {
                    if(from){c.FromAnchorManual=side.HasValue;if(side.HasValue)c.FromAnchor=side.Value;}
                    else{c.ToAnchorManual=side.HasValue;if(side.HasValue)c.ToAnchor=side.Value;}
                    RenderDiagram();Changed?.Invoke();
                    Hint?.Invoke(side.HasValue?"已手动指定描点，移动图形后保持不变。":"该端已恢复自动描点。");
                };
                parent.Items.Add(item);
            }
            return parent;
        }
        private void RestoreAutoAnchors(ConnectionModel c)
        {
            c.FromAnchorManual=false;c.ToAnchorManual=false;
            RenderDiagram();Changed?.Invoke();
            Hint?.Invoke("已恢复为自动描点（最短）。");
        }
        private void ShowNodeMenu(NodeModel n)
        {
            var m=NewMenu();
            m.Items.Add(MenuAction("编辑节点文字…",MenuIcons.Edit,()=>{var s=PromptText("编辑节点",n.Text);if(s!=null){n.Text=s;RenderDiagram();Changed?.Invoke();}}));
            m.Items.Add(MenuAction("添加 Tag…",MenuIcons.Tag,()=>{var s=PromptText("添加 Tag","新标签");if(!string.IsNullOrWhiteSpace(s)){n.Tags.Add(new TagModel{Text=s});RenderDiagram();Changed?.Invoke();}}));
            m.Items.Add(MenuSeparator());
            m.Items.Add(MenuAction("删除节点",MenuIcons.Trash,()=>{SelectedNodes.Clear();SelectedNodes.Add(n.Id);DeleteSelection();}));
            m.PlacementTarget=this;m.IsOpen=true;
        }
        private string PromptText(string title,string initial)
        {
            var w=new Window{Title=title,Width=330,Height=135,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};
            var sp=new StackPanel{Margin=new Thickness(12)};var tb=new TextBox{Text=initial,Padding=new Thickness(5),Margin=new Thickness(0,0,0,8)};
            var ok=new Button{Content="确定",Width=70,HorizontalAlignment=HorizontalAlignment.Right,IsDefault=true};ok.Click+=(s,e)=>{w.DialogResult=true;w.Close();};
            sp.Children.Add(tb);sp.Children.Add(ok);w.Content=sp;return w.ShowDialog()==true?tb.Text:null;
        }
        public void Arrange(DiagramDirection direction)
        {
            Model.Direction=direction;double x=80,y=70;
            for(int i=0;i<Model.Nodes.Count;i++)
            {
                var n=Model.Nodes[i];
                if(direction==DiagramDirection.LeftToRight){n.X=x;n.Y=y+(i%3)*145;x+=230;if(i%3==2){x+=0;}}
                else {n.X=x+(i%3)*230;n.Y=y+i/3*155;}
            }
            RenderDiagram();Changed?.Invoke();
        }
    }
}