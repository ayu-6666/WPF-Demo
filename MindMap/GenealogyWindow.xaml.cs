using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Serialization;
using Microsoft.Win32;
using MindMapWpf48.Models;

namespace MindMapWpf48
{
    /// <summary>族谱世系图编辑器：竖排姓名矩形框 + 直角世系连线（从右到左，长子在右）+ 右键添加妻子/丈夫/儿子/女儿</summary>
    public partial class GenealogyWindow : Window
    {
        // ===== 布局常量（中式世系图风格，从右到左） =====
        private const double NodeW = 46;      // 单个成员框宽
        private const double SpouseGap = 22;  // 夫妻间距（容纳连线中点⊖圆圈）
        private const double FamGap = 30;     // 兄弟分支间距
        private const double RowH = 150;      // 世代行高
        private const double TopY = 66;       // 画布顶部留白
        private const double LeftX = 90;      // 画布左侧留白
        private const double RightX = 90;     // 画布右侧留白（世代标签）
        private const string LineColor = "#9E2B22";      // 世系线深红
        private const string SelStroke = "#1565C0";      // 选中蓝
        // ===== 四色区分：血亲 / 姻亲（图1配色） =====
        private const string SonFill = "#A9D3F5";        // 血亲男（儿子）：浅蓝
        private const string SonStroke = "#2E6DA4";
        private const string DaughterFill = "#FCE4C0";   // 血亲女（女儿）：浅橙
        private const string DaughterStroke = "#C0762A";
        private const string WifeFill = "#F5AFB8";       // 妻子（姻亲女）：粉红
        private const string WifeStroke = "#C2455A";
        private const string HusbandFill = "#C8E6BE";    // 女婿（姻亲男）：浅绿
        private const string HusbandStroke = "#4E8C46";
        private const string ChronInk = "#1A1A1A";       // 世系谱墨色
        private const string ChronSub = "#555555";       // 世系谱小字
        // ===== 世系谱模式常量 =====
        private const double ChronNodeW = 50;  // 节点宽（生平小字列 + 姓名列 + 排行列）
        private const double ChronGap = 34;    // 兄弟分支间距
        private const double ChronRowGap = 30; // 世代行间距

        private GenealogyModel _model = new GenealogyModel();
        private Guid _selectedId;
        private readonly Dictionary<Guid, Border> _borderMap = new Dictionary<Guid, Border>();
        private readonly Dictionary<Guid, Brush> _normalBorder = new Dictionary<Guid, Brush>();  // 未选中时的边框色（恢复用）
        private double _zoom = 1.0;
        private bool _chronicle;               // false=树形图模式，true=世系谱模式
        private readonly Dictionary<int, double> _chronRowH = new Dictionary<int, double>();     // 世系谱每代行高

        public GenealogyWindow()
        {
            InitializeComponent();
            EnsureDemoTree();
            RenderAll();
            // 世系从右到左：打开时视图定位到最右侧（世代标签 + 长子一侧）
            Loaded += (s, e) => GeneScroll.ScrollToRightEnd();
        }

        // ================= 窗口控制按钮 =================

        private void MinBtn_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void MaxBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        private void CloseBtn_Click(object sender, RoutedEventArgs e) { Close(); }

        // ================= 缩放（滚轮以鼠标位置为缩放原点） =================

        private void GeneScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ApplyZoom(_zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15), e.GetPosition(GeneScroll));
            e.Handled = true;
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e) { ApplyZoom(_zoom * 1.2, null); }
        private void ZoomOut_Click(object sender, RoutedEventArgs e) { ApplyZoom(_zoom / 1.2, null); }
        private void ZoomReset_Click(object sender, RoutedEventArgs e) { ApplyZoom(1.0, null); }

        /// <summary>缩放画布；anchor 为视口中保持不动的锚点（null = 视口中心）</summary>
        private void ApplyZoom(double newZoom, Point? anchor)
        {
            newZoom = Math.Max(0.35, Math.Min(3.0, newZoom));
            if (Math.Abs(newZoom - _zoom) < 0.0001) return;

            var sv = GeneScroll;
            Point m = anchor ?? new Point(sv.ViewportWidth / 2, sv.ViewportHeight / 2);

            // 锚点当前对应的内容坐标（缩放前），缩放后平移偏移使该内容点回到锚点处
            double contentX = (sv.HorizontalOffset + m.X) / _zoom;
            double contentY = (sv.VerticalOffset + m.Y) / _zoom;

            _zoom = newZoom;
            GeneCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
            sv.UpdateLayout();
            sv.ScrollToHorizontalOffset(contentX * _zoom - m.X);
            sv.ScrollToVerticalOffset(contentY * _zoom - m.Y);
            ZoomText.Text = Math.Round(_zoom * 100) + "%";
        }

        // ================= 模式切换（树形图 / 世系谱） =================

        /// <summary>RadioButton Checked 事件：真实点击与 UIA Select 均可靠触发</summary>
        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (TreeModeBtn == null || ChronicleModeBtn == null) return;   // XAML 初始化期
            bool chronicle = sender == ChronicleModeBtn;
            if (chronicle == _chronicle) return;
            _chronicle = chronicle;
            _selectedId = Guid.Empty;
            RenderAll();
            // 世系从右到左：切换后视图定位到最右（世代标签 + 长子一侧）
            GeneScroll.UpdateLayout();
            GeneScroll.ScrollToRightEnd();
        }

        // ================= 数据 =================

        private void EnsureDemoTree()
        {
            if (_model.Roots.Count > 0) return;
            // 示例树：演示 从右到左排行 / 妻子虚线 / 女儿加丈夫 / 过继 / 无后
            var root = new Person { Name = "李祖缘", Birth = "1880" };
            var g2 = new Person { Name = "李兴全", Birth = "1902" };
            g2.Wives.Add(new Person { Name = "陈氏", Gender = "女", Birth = "1905" });
            var g3 = new Person { Name = "李才凤", Birth = "1926" };
            var c1 = new Person { Name = "李廷爵", Birth = "1948" };
            c1.Wives.Add(new Person { Name = "王氏", Gender = "女", Birth = "1950" });
            var c2 = new Person { Name = "李廷禄", Birth = "1951", Adopted = true };
            var dau = new Person { Name = "李婉君", Gender = "女", Birth = "1953" };
            dau.Husbands.Add(new Person { Name = "张君瑞", Gender = "男", Birth = "1951" });
            var s1 = new Person { Name = "李沂宗", Birth = "1970" };
            var s2 = new Person { Name = "李秋宗", Birth = "1973", LineEnded = true };
            var s3 = new Person { Name = "李海宗", Birth = "1975" };
            var s4 = new Person { Name = "李春学", Birth = "1976" };
            c1.Children.Add(s1);
            c1.Children.Add(s2);
            c2.Children.Add(s3);
            dau.Children.Add(s4);
            g3.Children.Add(c1);
            g3.Children.Add(c2);
            g3.Children.Add(dau);
            g2.Children.Add(g3);
            root.Children.Add(g2);
            _model.Roots.Add(root);
        }

        /// <summary>成员的所有配偶（男→妻子，女→丈夫）</summary>
        private static List<Person> Spouses(Person p)
        {
            return p.Gender == "女" ? p.Husbands : p.Wives;
        }

        /// <summary>排行序号 → 中文（1→长子/长女，2→次子/次女，3→三子/三女…）</summary>
        private static string RankName(int n, string gender)
        {
            string[] d = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            if (n <= 0) return "";
            string unit = gender == "女" ? "女" : "子";
            if (n == 1) return "长" + unit;
            if (n == 2) return "次" + unit;
            if (n <= 10) return d[n] + unit;
            return n + unit;
        }

        // ================= 布局与渲染 =================

        /// <summary>计算成员单元占用宽（含配偶与子孙块），防重叠</summary>
        private double Occupy(Person p)
        {
            double unitW = UnitWidth(p);
            if (p.Children.Count == 0) return unitW;
            double total = 0;
            for (int i = 0; i < p.Children.Count; i++)
            {
                total += Occupy(p.Children[i]);
                if (i < p.Children.Count - 1) total += FamGap;
            }
            return Math.Max(unitW, total);
        }

        private static double UnitWidth(Person p)
        {
            return NodeW + Spouses(p).Count * (NodeW + SpouseGap);
        }

        /// <summary>
        /// 布局并绘制成员单元，返回成员框中心 x（供父级连线）。
        /// 从右到左排行：子女按添加顺序反向放置（第一个添加的占最右块）。
        /// </summary>
        private double Layout(Person p, double left, int gen)
        {
            double y = TopY + gen * RowH;

            // ---- 成员自己（在单元最左），配偶依次向右 ----
            double unitW = UnitWidth(p);
            double occupy = Occupy(p);
            double unitLeft = left + (occupy - unitW) / 2;

            double h;
            var hb = MakeNode(p, out h);
            Canvas.SetLeft(hb, unitLeft);
            Canvas.SetTop(hb, y);
            GeneCanvas.Children.Add(hb);

            // 配偶（右侧并排 + 高边中点连线 + ⊖圆圈节点 + 标签）
            double sx = unitLeft + NodeW + SpouseGap;
            var sps = Spouses(p);
            for (int k = 0; k < sps.Count; k++)
            {
                var sp = sps[k];
                double sh;
                var sb = MakeNode(sp, out sh, true);
                Canvas.SetLeft(sb, sx);
                Canvas.SetTop(sb, y);
                GeneCanvas.Children.Add(sb);

                // 夫妻连线：成员框右宽边（高边）中点 → 配偶框左宽边中点，两框不等高时中间折线过渡
                double xEdge1 = unitLeft + NodeW, xEdge2 = sx;
                double y1c = y + h / 2, y2c = y + sh / 2;
                double midX = (xEdge1 + xEdge2) / 2;
                if (Math.Abs(y1c - y2c) < 0.5)
                {
                    AddLine(xEdge1, y1c, xEdge2, y2c, 1.0);
                }
                else
                {
                    var pl = new Polyline
                    {
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(LineColor)),
                        StrokeThickness = 1.0
                    };
                    pl.Points = new PointCollection
                    {
                        new Point(xEdge1, y1c), new Point(midX, y1c),
                        new Point(midX, y2c), new Point(xEdge2, y2c)
                    };
                    GeneCanvas.Children.Add(pl);
                }
                // 连线中点 ⊖ 圆圈节点（图1样式）
                AddSpouseJoint(midX, (y1c + y2c) / 2);
                AddTag(midX - 9, Math.Min(y1c, y2c) - 22, p.Gender == "女" ? "丈夫" : "妻子");

                sx += NodeW + SpouseGap;
            }

            double centerX = unitLeft + NodeW / 2;   // 世系线起点 = 成员框宽中点
            double bottom = y + h;

            // ---- 子女：从右到左排行（长子占最右块） ----
            if (p.Children.Count > 0)
            {
                int n = p.Children.Count;
                var widths = new double[n];
                double total = 0;
                for (int i = 0; i < n; i++)
                {
                    widths[i] = Occupy(p.Children[i]);
                    total += widths[i];
                    if (i < n - 1) total += FamGap;
                }

                double hLine = bottom + 22;
                double childTop = TopY + (gen + 1) * RowH;

                // 父垂直下行线（从框底宽中点出发）
                AddLine(centerX, bottom, centerX, hLine);

                // 反向放置：最后添加的放最左，长子（第一个）放最右
                double cursor = left + (occupy - total) / 2;
                var centers = new List<double>();
                for (int i = n - 1; i >= 0; i--)
                {
                    double ccx = Layout(p.Children[i], cursor, gen + 1);
                    centers.Add(ccx);
                    AddLine(ccx, hLine, ccx, childTop);
                    // 排行/过继标签（竖线中点旁）
                    var child = p.Children[i];
                    string tag = child.Adopted ? "过继" : (n > 1 ? RankName(i + 1, child.Gender) : "");
                    if (tag != "")
                        AddTag(ccx + 3, (hLine + childTop) / 2 - 6, tag);
                    cursor += widths[i] + FamGap;
                }

                // 兄弟横向总线
                if (centers.Count > 1)
                    AddLine(centers.Min(), hLine, centers.Max(), hLine);
            }

            return centerX;
        }

        /// <summary>
        /// 创建成员矩形框：竖排姓名 + 顶部圆圈（男空心/女实心）+ 止/入标记 + 悬浮生平提示。
        /// 配色区分：血亲男=浅蓝、血亲女=浅橙、妻子（姻亲女）=粉红、女婿（姻亲男）=浅绿。
        /// </summary>
        private Border MakeNode(Person p, out double height, bool spouse = false)
        {
            string name = string.IsNullOrWhiteSpace(p.Name) ? "?" : p.Name.Trim();

            bool female = p.Gender == "女";
            string fill, stroke;
            if (spouse)
            {
                fill = female ? WifeFill : HusbandFill;      // 妻子=粉红，女婿=浅绿
                stroke = female ? WifeStroke : HusbandStroke;
            }
            else
            {
                fill = female ? DaughterFill : SonFill;      // 血亲女=浅橙，血亲男=浅蓝
                stroke = female ? DaughterStroke : SonStroke;
            }

            double lineH = 20;
            double markerH = (p.LineEnded || p.Adopted) ? 15 : 0;
            height = 6 + 16 + 4 + name.Length * lineH + markerH + 8;

            var circle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(LineColor)),
                StrokeThickness = 1.2,
                Fill = female ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(LineColor)) : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var nameTxt = new TextBlock
            {
                Text = Vertical(name),
                FontSize = 15,
                LineHeight = lineH,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                FontFamily = new FontFamily("KaiTi, SimSun")
            };

            var stack = new StackPanel { Margin = new Thickness(3, 6, 3, 4), HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(circle);
            stack.Children.Add(nameTxt);
            if (p.LineEnded || p.Adopted)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = p.LineEnded ? "止" : "入",
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(LineColor)),
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeights.Bold
                });
            }

            bool selected = _selectedId == p.Id;
            var normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stroke));
            var border = new Border
            {
                Child = stack,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fill)),
                BorderBrush = selected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelStroke)) : normalBrush,
                BorderThickness = new Thickness(selected ? 2.5 : 1.4),
                CornerRadius = new CornerRadius(2),
                Cursor = Cursors.Hand,
                Tag = p,
                ToolTip = BuildTooltip(p)
            };
            _normalBorder[p.Id] = normalBrush;
            border.MouseLeftButtonDown += Node_MouseLeftButtonDown;
            border.ContextMenu = BuildMenu(p);
            _borderMap[p.Id] = border;
            return border;
        }

        /// <summary>竖排文本：逐字换行</summary>
        private static string Vertical(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        /// <summary>夫妻连线中点的 ⊖ 圆圈节点（白底红圈中间短横线，图1样式）</summary>
        private void AddSpouseJoint(double cx, double cy)
        {
            var lineBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(LineColor));
            var grid = new Grid { Width = 13, Height = 13 };
            grid.Children.Add(new Ellipse { Fill = Brushes.White, Stroke = lineBrush, StrokeThickness = 1.2 });
            grid.Children.Add(new Rectangle
            {
                Width = 7,
                Height = 1.5,
                Fill = lineBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            Canvas.SetLeft(grid, cx - 6.5);
            Canvas.SetTop(grid, cy - 6.5);
            GeneCanvas.Children.Add(grid);
        }

        /// <summary>悬浮生平提示</summary>
        private static ToolTip BuildTooltip(Person p)
        {
            var info = new StringBuilder();
            info.AppendLine(p.Name + "（" + p.Gender + "）");
            if (!string.IsNullOrWhiteSpace(p.Courtesy)) info.AppendLine("字/号：" + p.Courtesy);
            if (!string.IsNullOrWhiteSpace(p.Birth) || !string.IsNullOrWhiteSpace(p.Death))
                info.AppendLine("生卒：" + (p.Birth ?? "") + " ~ " + (p.Death ?? "今"));
            if (p.Adopted) info.AppendLine("入继/过继");
            if (p.LineEnded) info.AppendLine("无后（止）");
            var sps = Spouses(p);
            if (sps.Count > 0)
                info.AppendLine("配偶：" + string.Join("、", sps.Select(s => s.Name)));
            if (p.Children.Count > 0)
                info.AppendLine("子女：" + string.Join("、", p.Children.Select(c => c.Name)));
            if (!string.IsNullOrWhiteSpace(p.Note)) info.AppendLine("备注：" + p.Note);

            var tipText = new TextBlock
            {
                Text = info.ToString().TrimEnd(),
                FontSize = 12,
                MaxWidth = 260,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
            };
            return new ToolTip { Content = tipText, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        }

        private void AddLine(double x1, double y1, double x2, double y2, double thickness = 1.2, bool dashed = false, string color = LineColor)
        {
            var line = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                StrokeThickness = thickness
            };
            if (dashed) line.StrokeDashArray = new DoubleCollection { 3, 2 };
            GeneCanvas.Children.Add(line);
        }

        /// <summary>连线上小标签（妻子/丈夫/排行/过继）</summary>
        private void AddTag(double x, double y, string text, string color = LineColor)
        {
            var txt = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                FontFamily = new FontFamily("KaiTi, SimSun"),
                Background = Brushes.White,   // 白底避免与连线重叠看不清
                Padding = new Thickness(1, 0, 1, 0)
            };
            Canvas.SetLeft(txt, x);
            Canvas.SetTop(txt, y);
            GeneCanvas.Children.Add(txt);
        }

        private void DrawGenLabel(int gen)
        {
            var label = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(LineColor)),
                CornerRadius = new CornerRadius(2),
                Width = 34,
                Padding = new Thickness(0, 6, 0, 6),
                Child = new TextBlock
                {
                    Text = GenName(gen),
                    Foreground = Brushes.White,
                    FontSize = 14,
                    TextAlignment = TextAlignment.Center,
                    FontFamily = new FontFamily("KaiTi, SimSun")
                }
            };
            Canvas.SetLeft(label, GeneCanvas.Width - 56);   // 世代标签显示在最右侧
            Canvas.SetTop(label, TopY + gen * RowH + 12);
            GeneCanvas.Children.Add(label);
        }

        /// <summary>世代序号 → 中文（1→一世，12→十二世）</summary>
        private static string GenName(int gen)
        {
            int n = gen + 1;
            string[] d = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            if (n <= 10) return d[n] + "世";
            if (n < 20) return "十" + (n % 10 == 0 ? "" : d[n % 10]) + "世";
            return n + "世";
        }

        /// <summary>全量重绘（按当前模式分发：树形图 / 世系谱）</summary>
        private void RenderAll()
        {
            GeneCanvas.Children.Clear();
            _borderMap.Clear();
            _normalBorder.Clear();

            int maxGen = MaxDepth();
            if (_chronicle) RenderChronicle(maxGen); else RenderTree(maxGen);
        }

        /// <summary>树形图模式（矩形框 + 直角连线，图1样式）</summary>
        private void RenderTree(int maxGen)
        {
            double cursor = LeftX;
            foreach (var root in _model.Roots)
            {
                Layout(root, cursor, 0);
                cursor += Occupy(root) + 90;
            }
            cursor -= 90;

            GeneCanvas.Width = Math.Max(1000, cursor + 110);   // 内容右界 + 世代标签空间
            GeneCanvas.Height = Math.Max(900, (maxGen + 1) * RowH + 90);
            for (int g = 0; g <= maxGen; g++) DrawGenLabel(g);
        }

        // ================= 世系谱模式（传统竖排世系信息，图2/3/4样式） =================

        /// <summary>世系谱生平小字：配偶 + 生卒 + 入继/止 + 字号 + 备注</summary>
        private static string BuildBio(Person p)
        {
            var sb = new StringBuilder();
            var sps = Spouses(p);
            if (sps.Count > 0) sb.Append("配" + string.Join("、", sps.Select(s => s.Name)) + "。");
            bool hasBirth = !string.IsNullOrWhiteSpace(p.Birth), hasDeath = !string.IsNullOrWhiteSpace(p.Death);
            if (hasBirth || hasDeath)
            {
                if (hasBirth) sb.Append(p.Birth + "生");
                if (hasDeath) sb.Append(hasBirth ? "，" + p.Death + "卒" : p.Death + "卒");
                sb.Append("。");
            }
            if (p.Adopted) sb.Append("入继。");
            if (p.LineEnded) sb.Append("无后，止。");
            if (!string.IsNullOrWhiteSpace(p.Courtesy)) sb.Append("字" + p.Courtesy + "。");
            if (!string.IsNullOrWhiteSpace(p.Note)) sb.Append(p.Note);
            return sb.ToString();
        }

        /// <summary>世系谱节点占用宽</summary>
        private double ChronOccupy(Person p)
        {
            if (p.Children.Count == 0) return ChronNodeW;
            double total = 0;
            for (int i = 0; i < p.Children.Count; i++)
            {
                total += ChronOccupy(p.Children[i]);
                if (i < p.Children.Count - 1) total += ChronGap;
            }
            return Math.Max(ChronNodeW, total);
        }

        /// <summary>世系谱节点估算高（姓名列 / 生平小字列取大者）</summary>
        private static double ChronEstHeight(Person p)
        {
            string name = string.IsNullOrWhiteSpace(p.Name) ? "?" : p.Name.Trim();
            double nameH = name.Length * 19 + 10;
            double bioH = BuildBio(p).Length * 11.5 + 10;
            return Math.Max(48, Math.Max(nameH, bioH));
        }

        /// <summary>第一遍扫描：统计每代最大行高</summary>
        private void MeasureChronRows(Person p, int gen)
        {
            double h = ChronEstHeight(p);
            double cur;
            if (!_chronRowH.TryGetValue(gen, out cur) || cur < h) _chronRowH[gen] = h;
            foreach (var c in p.Children) MeasureChronRows(c, gen + 1);
        }

        /// <summary>某代行顶部 y（由各行高累计）</summary>
        private double ChronRowTop(int gen)
        {
            double y = TopY;
            for (int g = 0; g < gen; g++) y += (_chronRowH.ContainsKey(g) ? _chronRowH[g] : 90) + ChronRowGap;
            return y;
        }

        /// <summary>世系谱布局并绘制，返回成员中心 x（子女从右到左排行）</summary>
        private double ChronLayout(Person p, double left, int gen, int rank, int siblingCount)
        {
            double y = ChronRowTop(gen);
            double occupy = ChronOccupy(p);
            double x = left + (occupy - ChronNodeW) / 2;

            var node = MakeChronNode(p, rank, siblingCount);
            Canvas.SetLeft(node, x);
            Canvas.SetTop(node, y);
            GeneCanvas.Children.Add(node);

            double cx = x + ChronNodeW / 2;
            if (p.Children.Count > 0)
            {
                int n = p.Children.Count;
                var widths = new double[n];
                double total = 0;
                for (int i = 0; i < n; i++)
                {
                    widths[i] = ChronOccupy(p.Children[i]);
                    total += widths[i];
                    if (i < n - 1) total += ChronGap;
                }

                double bottom = y + ChronEstHeight(p);
                double childTop = ChronRowTop(gen + 1);
                double hLine = childTop - 14;           // 兄弟总横线位于子女行顶上方

                AddLine(cx, bottom, cx, hLine, 1.0, false, ChronInk);

                double cursor = left + (occupy - total) / 2;
                var centers = new List<double>();
                for (int i = n - 1; i >= 0; i--)
                {
                    double ccx = ChronLayout(p.Children[i], cursor, gen + 1, i + 1, n);
                    centers.Add(ccx);
                    AddLine(ccx, hLine, ccx, childTop, 1.0, false, ChronInk);
                    cursor += widths[i] + ChronGap;
                }
                if (centers.Count > 1)
                    AddLine(centers.Min(), hLine, centers.Max(), hLine, 1.0, false, ChronInk);
            }
            return cx;
        }

        /// <summary>世系谱节点：竖排姓名（大字居右）+ 排行小字 + 生平小字列（居左），细黑线风格</summary>
        private Border MakeChronNode(Person p, int rank, int siblingCount)
        {
            string name = string.IsNullOrWhiteSpace(p.Name) ? "?" : p.Name.Trim();
            var ink = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ChronInk));
            var sub = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ChronSub));
            var kai = new FontFamily("KaiTi, SimSun");

            var hp = new StackPanel { Orientation = Orientation.Horizontal };
            string bio = BuildBio(p);
            if (bio != "")
                hp.Children.Add(new TextBlock
                {
                    Text = Vertical(bio),
                    FontSize = 9.5,
                    LineHeight = 11.5,
                    TextAlignment = TextAlignment.Left,
                    Foreground = sub,
                    FontFamily = kai,
                    Margin = new Thickness(0, 2, 3, 0)
                });
            hp.Children.Add(new TextBlock
            {
                Text = Vertical(name),
                FontSize = 16,
                LineHeight = 19,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                Foreground = ink,
                FontFamily = kai
            });
            if (siblingCount > 1)
                hp.Children.Add(new TextBlock
                {
                    Text = Vertical(RankName(rank, p.Gender)),
                    FontSize = 9.5,
                    LineHeight = 11.5,
                    Foreground = sub,
                    FontFamily = kai,
                    Margin = new Thickness(3, 2, 0, 0)
                });

            bool selected = _selectedId == p.Id;
            var border = new Border
            {
                Child = hp,
                Background = Brushes.Transparent,
                BorderBrush = selected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelStroke)) : Brushes.Transparent,
                BorderThickness = new Thickness(selected ? 2 : 1),
                Padding = new Thickness(3, 2, 3, 2),
                Cursor = Cursors.Hand,
                Tag = p,
                ToolTip = BuildTooltip(p)
            };
            _normalBorder[p.Id] = Brushes.Transparent;
            border.MouseLeftButtonDown += Node_MouseLeftButtonDown;
            border.ContextMenu = BuildMenu(p);
            _borderMap[p.Id] = border;
            return border;
        }

        private void RenderChronicle(int maxGen)
        {
            _chronRowH.Clear();
            foreach (var r in _model.Roots) MeasureChronRows(r, 0);

            double cursor = LeftX;
            foreach (var root in _model.Roots)
            {
                ChronLayout(root, cursor, 0, 1, 1);
                cursor += ChronOccupy(root) + 90;
            }
            cursor -= 90;

            GeneCanvas.Width = Math.Max(1000, cursor + 150);
            double bottom = ChronRowTop(maxGen) + (_chronRowH.ContainsKey(maxGen) ? _chronRowH[maxGen] : 90);
            GeneCanvas.Height = Math.Max(900, bottom + 70);

            for (int g = 0; g <= maxGen; g++) DrawChronGenLabel(g);
            DrawChronTitle(maxGen);
        }

        /// <summary>世系谱世代标签：黑底白字竖排小方块（图3样式），位于画布最右</summary>
        private void DrawChronGenLabel(int gen)
        {
            double rowH = _chronRowH.ContainsKey(gen) ? _chronRowH[gen] : 90;
            var label = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ChronInk)),
                Width = 30,
                Padding = new Thickness(0, 8, 0, 8),
                Child = new TextBlock
                {
                    Text = GenName(gen),
                    Foreground = Brushes.White,
                    FontSize = 14,
                    TextAlignment = TextAlignment.Center,
                    FontFamily = new FontFamily("KaiTi, SimSun")
                }
            };
            Canvas.SetLeft(label, GeneCanvas.Width - 66);
            Canvas.SetTop(label, ChronRowTop(gen) + rowH / 2 - 30);
            GeneCanvas.Children.Add(label);
        }

        /// <summary>右缘竖排总标题（图3"第一世至第N世世系表"）</summary>
        private void DrawChronTitle(int maxGen)
        {
            var title = new TextBlock
            {
                Text = Vertical("第一世至第" + GenName(maxGen) + "世系表"),
                FontSize = 13,
                LineHeight = 17,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888")),
                FontFamily = new FontFamily("KaiTi, SimSun")
            };
            Canvas.SetLeft(title, GeneCanvas.Width - 26);
            Canvas.SetTop(title, TopY + 4);
            GeneCanvas.Children.Add(title);
        }

        private int MaxDepth()
        {
            int Depth(Person p, int g)
            {
                int m = g;
                foreach (var c in p.Children) m = Math.Max(m, Depth(c, g + 1));
                return m;
            }
            int max = 0;
            foreach (var r in _model.Roots) max = Math.Max(max, Depth(r, 0));
            return max;
        }

        // ================= 交互 =================

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = (Border)sender;
            Select(border);
            e.Handled = true;
        }

        private void Select(Border border)
        {
            if (_selectedId != Guid.Empty && _borderMap.ContainsKey(_selectedId))
            {
                var old = _borderMap[_selectedId];
                Brush nb;
                if (!_normalBorder.TryGetValue(_selectedId, out nb)) nb = Brushes.Transparent;
                old.BorderBrush = nb;
                old.BorderThickness = new Thickness(1.4);
            }
            _selectedId = ((Person)border.Tag).Id;
            border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelStroke));
            border.BorderThickness = new Thickness(2.5);
        }

        private ContextMenu BuildMenu(Person p)
        {
            var menu = new ContextMenu();
            // 男性→添加妻子；女性→添加丈夫
            AddItem(menu, p.Gender == "女" ? "添加丈夫" : "添加妻子",
                () => AddSpouse(p, p.Gender == "女" ? "男" : "女", p.Gender == "女" ? "丈夫" : "妻子"));
            AddItem(menu, "添加儿子", () => AddChild(p, "男"));
            AddItem(menu, "添加女儿", () => AddChild(p, "女"));
            menu.Items.Add(new Separator());
            AddItem(menu, "编辑信息", () => EditPerson(p));
            AddItem(menu, p.LineEnded ? "取消无后标记（止）" : "标记无后（止）", () =>
            {
                p.LineEnded = !p.LineEnded;
                RenderAll();
            });
            AddItem(menu, "删除该成员（含后代）", () => DeletePerson(p));
            return menu;
        }

        private void AddItem(ContextMenu menu, string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }

        private bool ShowEdit(string title, string lockedGender, Person person, bool isNew)
        {
            var dlg = new PersonEditWindow(title, lockedGender, person, isNew) { Owner = this };
            return dlg.ShowDialog() == true;
        }

        private void AddSpouse(Person p, string gender, string label)
        {
            var sp = new Person { Gender = gender };
            if (!ShowEdit(label + "（" + p.Name + "）", gender, sp, true)) return;
            if (gender == "女") p.Wives.Add(sp); else p.Husbands.Add(sp);
            _selectedId = sp.Id;
            RenderAll();
        }

        private void AddChild(Person p, string gender)
        {
            var child = new Person { Gender = gender };
            string title = gender == "男" ? "添加儿子（" + p.Name + "）" : "添加女儿（" + p.Name + "）";
            if (!ShowEdit(title, gender, child, true)) return;
            p.Children.Add(child);
            _selectedId = child.Id;
            RenderAll();
        }

        private void EditPerson(Person p)
        {
            if (!ShowEdit("编辑信息（" + p.Name + "）", null, p, false)) return;
            RenderAll();
        }

        private void DeletePerson(Person p)
        {
            if (MessageBox.Show("确定删除【" + p.Name + "】及其全部后代？", "删除确认",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            if (!RemovePerson(p))
            {
                MessageBox.Show("未找到该成员。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _selectedId = Guid.Empty;
            RenderAll();
        }

        /// <summary>从树中移除成员（递归查找父容器），返回是否成功</summary>
        private bool RemovePerson(Person target)
        {
            if (_model.Roots.Remove(target)) return true;
            return RemoveFrom(_model.Roots, target);
        }

        private bool RemoveFrom(List<Person> list, Person target)
        {
            foreach (var parent in list)
            {
                if (parent.Children.Remove(target) || parent.Wives.Remove(target) || parent.Husbands.Remove(target))
                    return true;
                if (RemoveFrom(parent.Children, target) || RemoveFrom(parent.Wives, target)
                    || RemoveFrom(parent.Husbands, target)) return true;
            }
            return false;
        }

        private void AddRoot_Click(object sender, RoutedEventArgs e)
        {
            var root = new Person { Gender = "男" };
            if (!ShowEdit("添加始祖", "男", root, true)) return;
            _model.Roots.Add(root);
            _selectedId = root.Id;
            RenderAll();
        }

        private void Relayout_Click(object sender, RoutedEventArgs e)
        {
            RenderAll();
        }

        // ================= 文件 =================

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "保存族谱",
                Filter = "族谱文件 (*.genealogy.xml)|*.genealogy.xml|所有文件 (*.*)|*.*",
                FileName = "族谱.genealogy.xml"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var serializer = new XmlSerializer(typeof(GenealogyModel));
                using (var sw = new StreamWriter(dlg.FileName))
                    serializer.Serialize(sw, _model);
                MessageBox.Show("已保存：" + dlg.FileName, "保存族谱", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "打开族谱",
                Filter = "族谱文件 (*.genealogy.xml)|*.genealogy.xml|所有文件 (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var serializer = new XmlSerializer(typeof(GenealogyModel));
                using (var sr = new StreamReader(dlg.FileName))
                    _model = (GenealogyModel)serializer.Deserialize(sr);
                _selectedId = Guid.Empty;
                RenderAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportPng_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Title = "导出 PNG", Filter = "PNG 图片 (*.png)|*.png", FileName = "族谱世系图.png" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                int w = (int)Math.Ceiling(GeneCanvas.Width);
                int h = (int)Math.Ceiling(GeneCanvas.Height);
                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(GeneCanvas);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(dlg.FileName))
                    encoder.Save(fs);
                MessageBox.Show("已导出：" + dlg.FileName, "导出PNG", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
