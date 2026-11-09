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
    /// <summary>族谱世系图编辑器：统一尺寸矩形框（横排姓名两行换行+省略号，独子竖直直连）+ 规则世系连线（夫妻按嫁入/嫁出排布：妻左夫右、女左婿右；子女自连线中点箭头下行，长子在右）+ 身份底色图例 + 右键添加妻子/丈夫/儿子/女儿</summary>
    public partial class GenealogyWindow : Window
    {
        // ===== 布局常量（中式世系图风格，从右到左） =====
        private const double NodeW = 46;      // 单个成员框宽
        private const double NodeH = 80;      // 统一成员框高（容纳两行姓名 + 止/入标记，所有节点一致）
        private const double SpouseGap = 22;  // 夫妻间距（容纳连线中点⊖圆圈）
        private const double FamGap = 30;     // 兄弟分支间距
        private const double RowH = 150;      // 世代行高
        private const double TopY = 66;       // 画布顶部留白
        private const double LeftX = 90;      // 画布左侧留白
        private const double RightX = 90;     // 画布右侧留白（世代标签）
        private const string LineColor = "#9E2B22";      // 世系线深红
        private const string SelStroke = "#1565C0";      // 选中蓝
        // ===== 身份配色映射（集中定义，便于统一换色；[0]=底色，[1]=边框色） =====
        private static readonly Dictionary<string, string[]> IdentityPalette = new Dictionary<string, string[]>
        {
            { "本家男",   new[] { "#A9C7F0", "#2E6DA4" } },   // 本家男性：蓝色系
            { "本家女",   new[] { "#F5C4D8", "#C24E7E" } },   // 本家女性（未嫁）：粉色系
            { "嫁入妻子", new[] { "#F7E3B5", "#B08A2E" } },   // 嫁入的妻子：浅琥珀
            { "外嫁女儿", new[] { "#DCC8F0", "#7A4EA3" } },   // 外嫁的女儿：紫色系
            { "女婿",     new[] { "#C5D6E8", "#4A708C" } }    // 女婿：灰蓝色系
        };
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
        /// 夫妻左右顺序（按嫁入/嫁出）：男成员之妻在左、本家成员在右（娶入）；女成员在左、女婿在右（嫁出）。
        /// 所有节点统一 NodeW×NodeH 固定尺寸；相邻框相对边中点水平直连（⊖ 圆圈节点）；
        /// 子女连线从首段夫妻连线中点（无配偶时为成员框底边中点）出发，箭头指向子女框上边中点；
        /// 独子（alignCenterX 给出且对齐成功）时为单条竖直直线箭头，不绕横向总线。
        /// </summary>
        private double Layout(Person p, double left, int gen, double? alignCenterX = null)
        {
            double y = TopY + gen * RowH;

            // ---- 成员单元（成员 + 配偶框，统一固定尺寸） ----
            double unitW = UnitWidth(p);
            double occupy = Occupy(p);
            double unitLeft = left + (occupy - unitW) / 2;

            var sps = Spouses(p);
            bool wifeSide = p.Gender != "女";   // 男成员配偶取自 Wives（嫁入妻子），女成员配偶取自 Husbands（女婿）
            bool memberLeft = !wifeSide;        // 左右顺序：女成员在左、配偶向右（嫁出）；妻子在左、男成员在最右（娶入）

            // 独子竖直连线：在本单元占用跨度内平移，使成员框中心对齐父级指定的下行线起点
            //（不与相邻兄弟分支重叠）；平移后仍无法精确对齐时保持居中，由父级退化为直角连线
            if (alignCenterX.HasValue)
            {
                double curCenter = (memberLeft ? unitLeft : unitLeft + sps.Count * (NodeW + SpouseGap)) + NodeW / 2;
                double dx = alignCenterX.Value - curCenter;
                unitLeft = Math.Max(left, Math.Min(left + occupy - unitW, unitLeft + dx));
            }

            var member = MakeNode(p, false, false);
            var spouseBorders = new List<Border>();
            foreach (var sp in sps)
                spouseBorders.Add(MakeNode(sp, true, wifeSide));

            var ordered = new List<Border>();
            if (memberLeft) { ordered.Add(member); ordered.AddRange(spouseBorders); }
            else { ordered.AddRange(spouseBorders); ordered.Add(member); }

            for (int k = 0; k < ordered.Count; k++)
            {
                Canvas.SetLeft(ordered[k], unitLeft + k * (NodeW + SpouseGap));
                Canvas.SetTop(ordered[k], y);
                GeneCanvas.Children.Add(ordered[k]);
            }

            double boxBottom = y + NodeH;
            double yMid = y + NodeH / 2;      // 夫妻连线所在高度（统一框高中点）
            double memberX = memberLeft ? unitLeft : unitLeft + spouseBorders.Count * (NodeW + SpouseGap);
            double centerX = memberX + NodeW / 2;   // 成员框宽中点（父级竖线落点）

            // 相邻框相对边中点水平直连 + ⊖圆圈节点；首段连线挂 妻子/丈夫 标签
            double? firstJointX = null;
            for (int k = 0; k + 1 < ordered.Count; k++)
            {
                double x1 = unitLeft + k * (NodeW + SpouseGap) + NodeW;
                double midX = x1 + SpouseGap / 2;
                AddLine(x1, yMid, x1 + SpouseGap, yMid);
                if (k == 0) firstJointX = midX;
                AddSpouseJoint(midX, yMid);
                if (k == 0)
                    AddTagCentered(midX, yMid - 26, p.Gender == "女" ? "丈夫" : "妻子");
            }

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

                double childTop = TopY + (gen + 1) * RowH;

                // 子女连线起点：首段夫妻连线中点（⊖圆圈下缘出发）；无配偶时为成员框底边中点
                double originX = firstJointX ?? centerX;
                double originY = firstJointX.HasValue ? yMid + 8 : boxBottom;

                // 排行方向：子女从右到左依次排列（长子最右、次子在其左），游标自左向右推进
                double cursor = left + (occupy - total) / 2;
                double hLine = Math.Min(boxBottom + Math.Max(12, (childTop - boxBottom) * 0.4), childTop - 8);

                if (n == 1)
                {
                    // ---- 独子：单条竖直直线箭头（无拐角、不绕横向总线） ----
                    double ccx = Layout(p.Children[0], cursor, gen + 1, originX);
                    if (Math.Abs(ccx - originX) < 0.5)
                    {
                        AddLine(originX, originY, originX, childTop);
                        AddArrowDown(originX, childTop);
                        if (p.Children[0].Adopted)
                            AddTagCentered(originX, (originY + childTop) / 2 - 6, "过继");
                    }
                    else
                    {
                        // 子女占用跨度钳制导致无法对齐：退化为直角连线（竖-横-竖）
                        AddLine(originX, originY, originX, hLine);
                        AddLine(Math.Min(originX, ccx), hLine, Math.Max(originX, ccx), hLine);
                        AddLine(ccx, hLine, ccx, childTop);
                        AddArrowDown(ccx, childTop);
                        if (p.Children[0].Adopted)
                            AddTagCentered(ccx, (hLine + childTop) / 2 - 6, "过继");
                    }
                }
                else
                {
                    // ---- 多子女：长子占最右块、后续依次向左 ----
                    AddLine(originX, originY, originX, hLine);   // 父垂直下行线
                    var centers = new List<double>();
                    for (int i = n - 1; i >= 0; i--)
                    {
                        double ccx = Layout(p.Children[i], cursor, gen + 1);
                        centers.Add(ccx);
                        AddLine(ccx, hLine, ccx, childTop);      // 竖线到子女框上边中点
                        AddArrowDown(ccx, childTop);             // 单向箭头指向子女框上边中点
                        // 排行/过继标签（竖线中点，白底居中贴合连线）
                        var child = p.Children[i];
                        string tag = child.Adopted ? "过继" : RankName(i + 1, child.Gender);
                        if (tag != "")
                            AddTagCentered(ccx, (hLine + childTop) / 2 - 6, tag);
                        cursor += widths[i] + FamGap;
                    }

                    // 兄弟横向总线：覆盖下行线起点与全部子女竖线，保证整体连通不断线
                    double busL = Math.Min(originX, centers.Min());
                    double busR = Math.Max(originX, centers.Max());
                    if (busR - busL > 0.5)
                        AddLine(busL, hLine, busR, hLine);
                }
            }

            return centerX;
        }

        /// <summary>
        /// 依据数据模型关系路径判定身份（映射见 IdentityPalette）：
        /// Wives 列表=嫁入妻子；Husbands 列表=女婿；
        /// 血缘路径（Roots/Children）成员：男=本家男；女=有丈夫（嫁出）为外嫁女儿，否则本家女。
        /// </summary>
        private static string IdentityOf(Person p, bool spouse, bool wifeSide)
        {
            if (spouse) return wifeSide ? "嫁入妻子" : "女婿";
            if (p.Gender == "女") return p.Husbands.Count > 0 ? "外嫁女儿" : "本家女";
            return "本家男";
        }

        /// <summary>
        /// 创建成员矩形框：所有节点统一 NodeW×NodeH 固定尺寸（宽高完全一致，不随姓名长度变化）；
        /// 姓名横排、最多两行自动换行，仍放不下时末尾省略号（悬浮 Tooltip 显示全名）
        /// + 顶部圆圈（男空心/女实心）+ 止/入标记 + 悬浮生平提示。
        /// 底色按身份区分（集中映射见 IdentityPalette）：本家男=蓝、本家女=粉、嫁入妻子=琥珀、外嫁女儿=紫、女婿=灰蓝。
        /// </summary>
        private Border MakeNode(Person p, bool spouse = false, bool wifeSide = false)
        {
            string name = string.IsNullOrWhiteSpace(p.Name) ? "?" : p.Name.Trim();

            string[] colors = IdentityPalette[IdentityOf(p, spouse, wifeSide)];
            string fill = colors[0], stroke = colors[1];
            bool female = p.Gender == "女";   // 顶部圆圈：男空心/女实心

            var circle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(LineColor)),
                StrokeThickness = 1.2,
                Fill = female ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(LineColor)) : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 姓名横排：框内最多两行换行，仍放不下时省略号截断（全名见 Tooltip），不溢出、不撑大框
            var nameTxt = new TextBlock
            {
                Text = name,
                FontSize = 15,
                LineHeight = 19,
                MaxHeight = 38,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                FontFamily = new FontFamily("KaiTi, SimSun")
            };

            var stack = new StackPanel { Margin = new Thickness(3, 6, 3, 4), VerticalAlignment = VerticalAlignment.Center };
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

            // 统一固定尺寸：所有节点（含配偶）宽高完全一致，连线端点按常量计算
            border.Width = NodeW;
            border.Height = NodeH;
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

        /// <summary>子女连线末端的单向箭头（实心三角，指向子女框上边中点）</summary>
        private void AddArrowDown(double cx, double tipY)
        {
            const double w = 7, h = 6;
            var poly = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(cx - w / 2, tipY - h),
                    new Point(cx + w / 2, tipY - h),
                    new Point(cx, tipY)
                },
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(LineColor))
            };
            GeneCanvas.Children.Add(poly);
        }

        /// <summary>连线上小标签（妻子/丈夫/排行/过继）：白底，水平居中贴合连线，不被裁剪</summary>
        private void AddTagCentered(double cx, double y, string text, string color = LineColor)
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
            txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(txt, cx - txt.DesiredSize.Width / 2);
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

        /// <summary>身份颜色图例（画布顶部左侧，树形图模式）</summary>
        private void DrawLegend()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var kv in IdentityPalette)
            {
                panel.Children.Add(new Border
                {
                    Width = 14,
                    Height = 14,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kv.Value[0])),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kv.Value[1])),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                panel.Children.Add(new TextBlock
                {
                    Text = kv.Key,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    FontFamily = new FontFamily("KaiTi, SimSun"),
                    Margin = new Thickness(0, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            Canvas.SetLeft(panel, LeftX);
            Canvas.SetTop(panel, 16);
            GeneCanvas.Children.Add(panel);
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
            DrawLegend();   // 画布顶部身份颜色图例
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
