using Microsoft.Win32;
using MindMapWpf48.Models;
using MindMapWpf48.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Input;

namespace MindMapWpf48
{
    public partial class MainWindow : Window
    {
        private DiagramModel _diagram = new DiagramModel();
        private Guid? _selectedNode;
        private Guid? _connectFrom;
        private bool _connectMode;
        private Point _dragStart;
        private double _nodeStartX, _nodeStartY;
        private bool _dragging;

        public MainWindow()
        {
            InitializeComponent();
            AddSample();
            RenderDiagram();
        }

        private void AddSample()
        {
            var a = NewNode(NodeShape.Ellipse, 100, 150, "开始");
            var b = NewNode(NodeShape.Rectangle, 390, 150, "需求分析");
            var c = NewNode(NodeShape.Diamond, 700, 150, "需求是否明确？");
            var d = NewNode(NodeShape.RoundedRectangle, 1010, 65, "开发实现");
            var e = NewNode(NodeShape.Square, 1010, 260, "补充需求");
            _diagram.Nodes.AddRange(new[] { a, b, c, d, e });
            _diagram.Connections.Add(new ConnectionModel { FromNodeId = a.Id, ToNodeId = b.Id });
            _diagram.Connections.Add(new ConnectionModel { FromNodeId = b.Id, ToNodeId = c.Id });
            _diagram.Connections.Add(new ConnectionModel { FromNodeId = c.Id, ToNodeId = d.Id, Label = "是" });
            _diagram.Connections.Add(new ConnectionModel { FromNodeId = c.Id, ToNodeId = e.Id, Label = "否" });
            b.Tags.Add(new TagModel { Text = "分析", Color = "#FFFFE4A8" });
            d.Tags.Add(new TagModel { Text = "进行中", Color = "#FFCDEFD8" });
        }

        private NodeModel NewNode(NodeShape shape, double x, double y, string text)
        {
            var n = new NodeModel { Shape = shape, X = x, Y = y, Text = text };
            if (shape == NodeShape.Square) { n.Width = 110; n.Height = 110; }
            if (shape == NodeShape.Diamond) { n.Width = 170; n.Height = 110; }
            if (shape == NodeShape.Ellipse) { n.Width = 130; n.Height = 75; }
            return n;
        }

        private void AddNode_Click(object sender, RoutedEventArgs e)
        {
            var shape = (NodeShape)Enum.Parse(typeof(NodeShape), (string)((Button)sender).Tag);
            var n = NewNode(shape, 180 + (_diagram.Nodes.Count % 5) * 35, 150 + (_diagram.Nodes.Count % 6) * 35, shape.ToString());
            _diagram.Nodes.Add(n); _selectedNode = n.Id; RenderDiagram();
        }

        private void ConnectMode_Click(object sender, RoutedEventArgs e)
        {
            _connectMode = !_connectMode; _connectFrom = null;
            StatusText.Text = _connectMode ? "连接模式：依次点击起点节点和终点节点。" : "已退出连接模式。";
        }

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            var n = GetSelected();
            if (n == null) { StatusText.Text = "请先选择一个节点。"; return; }
            var value = Prompt("请输入Tag标签：", "新标签");
            if (!string.IsNullOrWhiteSpace(value)) { n.Tags.Add(new TagModel { Text = value }); RenderDiagram(); }
        }

        private void EditText_Click(object sender, RoutedEventArgs e)
        {
            var n = GetSelected();
            if (n == null) { StatusText.Text = "请先选择一个节点。"; return; }
            var value = Prompt("请输入节点文字：", n.Text);
            if (value != null) { n.Text = value; RenderDiagram(); }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var n = GetSelected(); if (n == null) return;
            _diagram.Nodes.Remove(n);
            _diagram.Connections.RemoveAll(c => c.FromNodeId == n.Id || c.ToNodeId == n.Id);
            _selectedNode = null; RenderDiagram();
        }

        private NodeModel GetSelected() { return _selectedNode.HasValue ? _diagram.Nodes.FirstOrDefault(n => n.Id == _selectedNode.Value) : null; }

        private void RenderDiagram()
        {
            DiagramCanvas.Children.Clear();
            foreach (var c in _diagram.Connections) DrawConnection(c);
            foreach (var n in _diagram.Nodes) DrawNode(n);
        }

        private void DrawNode(NodeModel n)
        {
            var panel = new Grid { Width = n.Width, Height = n.Height, Tag = n.Id, Cursor = Cursors.SizeAll };
            Shape shape;
            switch (n.Shape)
            {
                case NodeShape.Ellipse: shape = new Ellipse(); break;
                case NodeShape.Diamond: shape = new Polygon { Points = new PointCollection { new Point(n.Width/2,0), new Point(n.Width,n.Height/2), new Point(n.Width/2,n.Height), new Point(0,n.Height/2) } }; break;
                case NodeShape.RoundedRectangle: shape = new Rectangle { RadiusX = 14, RadiusY = 14 }; break;
                default: shape = new Rectangle(); break;
            }
            if (shape is Shape)
            {
                ((Shape)shape).Fill = Brush(n.Fill); ((Shape)shape).Stroke = Brush(n.Stroke); ((Shape)shape).StrokeThickness = 2;
            }
            panel.Children.Add(shape);
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(8) };
            content.Children.Add(new TextBlock { Text = n.Text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(35,48,66)) });
            if (!string.IsNullOrWhiteSpace(n.Status))
                content.Children.Add(new TextBlock { Text = n.Status, FontSize = 10, Foreground = Brushes.DimGray, TextAlignment = TextAlignment.Center, Margin = new Thickness(0,3,0,0) });
            if (n.Tags.Count > 0)
            {
                var tags = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,4,0,0) };
                foreach (var t in n.Tags)
                    tags.Children.Add(new Border { Background = Brush(t.Color), CornerRadius = new CornerRadius(7), Padding = new Thickness(5,1,5,1), Margin = new Thickness(2,1,2,1), Child = new TextBlock { Text = t.Text, FontSize = 9, Foreground = Brushes.DarkSlateGray } });
                content.Children.Add(tags);
            }
            panel.Children.Add(content);
            if (_selectedNode == n.Id) panel.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.DodgerBlue, BlurRadius = 13, ShadowDepth = 0, Opacity = .8 };
            Canvas.SetLeft(panel, n.X); Canvas.SetTop(panel, n.Y);
            panel.MouseLeftButtonDown += Node_MouseLeftButtonDown;
            panel.MouseMove += Node_MouseMove;
            panel.MouseLeftButtonUp += Node_MouseLeftButtonUp;
            DiagramCanvas.Children.Add(panel);
        }

        private void DrawConnection(ConnectionModel c)
        {
            var a = _diagram.Nodes.FirstOrDefault(n => n.Id == c.FromNodeId);
            var b = _diagram.Nodes.FirstOrDefault(n => n.Id == c.ToNodeId);
            if (a == null || b == null) return;
            Point p1 = Center(a), p2 = Center(b);
            Vector direction = p2 - p1; direction.Normalize();
            p1 += direction * Math.Min(a.Width, a.Height) * .38;
            p2 -= direction * Math.Min(b.Width, b.Height) * .38;
            var line = new Line { X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y, Stroke = new SolidColorBrush(Color.FromRgb(95,112,135)), StrokeThickness = 2 };
            DiagramCanvas.Children.Add(line);
            if (c.ShowArrow)
            {
                Vector v = p2 - p1; v.Normalize(); Vector normal = new Vector(-v.Y, v.X);
                Point q1 = p2 - v * 13 + normal * 6, q2 = p2 - v * 13 - normal * 6;
                var arrow = new Polygon { Points = new PointCollection { p2, q1, q2 }, Fill = line.Stroke };
                DiagramCanvas.Children.Add(arrow);
            }
            if (!string.IsNullOrWhiteSpace(c.Label))
            {
                var label = new TextBlock { Text = c.Label, Background = Brushes.White, Foreground = Brushes.DimGray, FontSize = 11, Padding = new Thickness(3) };
                Canvas.SetLeft(label, (p1.X+p2.X)/2); Canvas.SetTop(label, (p1.Y+p2.Y)/2 - 18); DiagramCanvas.Children.Add(label);
            }
        }

        private Point Center(NodeModel n) { return new Point(n.X + n.Width / 2, n.Y + n.Height / 2); }
        private Brush Brush(string value) { try { return (Brush)new BrushConverter().ConvertFromString(value); } catch { return Brushes.LightBlue; } }

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var panel = (Grid)sender; Guid id = (Guid)panel.Tag;
            if (_connectMode)
            {
                if (!_connectFrom.HasValue) { _connectFrom = id; StatusText.Text = "已选择起点，请点击终点节点。"; }
                else if (_connectFrom.Value != id)
                {
                    _diagram.Connections.Add(new ConnectionModel { FromNodeId = _connectFrom.Value, ToNodeId = id });
                    _connectFrom = null; _connectMode = false; RenderDiagram(); StatusText.Text = "连接完成。";
                }
                e.Handled = true; return;
            }
            _selectedNode = id;
            var n = GetSelected(); _dragStart = e.GetPosition(DiagramCanvas); _nodeStartX = n.X; _nodeStartY = n.Y; _dragging = true;
            panel.CaptureMouse(); RenderDiagram(); e.Handled = true;
        }
        private void Node_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
            var n = GetSelected(); if (n == null) return;
            Point p = e.GetPosition(DiagramCanvas); n.X = Math.Max(0, _nodeStartX + p.X - _dragStart.X); n.Y = Math.Max(0, _nodeStartY + p.Y - _dragStart.Y);
            RenderDiagram();
        }
        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false; ((Grid)sender).ReleaseMouseCapture();
        }

        private string Prompt(string title, string initial)
        {
            var w = new Window { Title = title, Width = 360, Height = 145, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize };
            var stack = new StackPanel { Margin = new Thickness(12) };
            var box = new TextBox { Text = initial, Margin = new Thickness(0,0,0,10), Padding = new Thickness(5) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "确定", Width = 70, IsDefault = true, Margin = new Thickness(4) };
            var cancel = new Button { Content = "取消", Width = 70, IsCancel = true, Margin = new Thickness(4) };
            ok.Click += (s,e) => { w.DialogResult = true; w.Close(); };
            buttons.Children.Add(ok); buttons.Children.Add(cancel); stack.Children.Add(box); stack.Children.Add(buttons); w.Content = stack;
            return w.ShowDialog() == true ? box.Text : null;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "流程图项目 (*.xml)|*.xml", FileName = "流程图.xml" };
            if (dlg.ShowDialog() == true) { DiagramFileService.Save(_diagram, dlg.FileName); StatusText.Text = "项目已保存：" + dlg.FileName; }
        }
        private void Load_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "流程图项目 (*.xml)|*.xml" };
            if (dlg.ShowDialog() == true) { _diagram = DiagramFileService.Load(dlg.FileName); _selectedNode = null; RenderDiagram(); StatusText.Text = "项目已打开。"; }
        }
        private void OpenGenealogy_Click(object sender, RoutedEventArgs e)
        {
            var win = new GenealogyWindow { Owner = this };
            win.Show();
        }
        private void ExportPng_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "PNG图片 (*.png)|*.png", FileName = "流程图.png" };
            if (dlg.ShowDialog() != true) return;
            DiagramCanvas.Measure(new Size(DiagramCanvas.Width, DiagramCanvas.Height));
            DiagramCanvas.Arrange(new Rect(new Size(DiagramCanvas.Width, DiagramCanvas.Height)));
            var bitmap = new RenderTargetBitmap((int)DiagramCanvas.Width, (int)DiagramCanvas.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(DiagramCanvas);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = System.IO.File.Create(dlg.FileName)) encoder.Save(stream);
            StatusText.Text = "PNG图片已导出：" + dlg.FileName;
        }
    }
}