using System;
using System.Collections.Generic;
using System.Windows;
using VisioLikeWpf48.Models;

namespace VisioLikeWpf48.Services
{
    /// <summary>两个图形之间的相对排列方式。</summary>
    public enum AnchorArrangement { Horizontal, Vertical, Diagonal }

    /// <summary>
    /// 连接线描点解析与正交路由。画布绘制与 PNG 导出共用这一套规则，保证端点始终贴合描点。
    /// </summary>
    public static class ConnectionGeometry
    {
        private const double StubLength = 16;

        public static readonly AnchorSide[] AllSides =
            { AnchorSide.Top, AnchorSide.Right, AnchorSide.Bottom, AnchorSide.Left };

        public static Point AnchorPoint(NodeModel n, AnchorSide side)
        {
            switch (side)
            {
                case AnchorSide.Top: return new Point(n.X + n.Width / 2, n.Y);
                case AnchorSide.Bottom: return new Point(n.X + n.Width / 2, n.Y + n.Height);
                case AnchorSide.Left: return new Point(n.X, n.Y + n.Height / 2);
                default: return new Point(n.X + n.Width, n.Y + n.Height / 2);
            }
        }

        /// <summary>描点朝图形外侧的法线方向。</summary>
        public static Vector OutwardNormal(AnchorSide side)
        {
            switch (side)
            {
                case AnchorSide.Top: return new Vector(0, -1);
                case AnchorSide.Bottom: return new Vector(0, 1);
                case AnchorSide.Left: return new Vector(-1, 0);
                default: return new Vector(1, 0);
            }
        }

        /// <summary>
        /// 用两图形包围盒的间隙判定排列关系：仅横向分离为左右排列，仅纵向分离为上下排列，
        /// 两轴都分离（或图形重叠）视为斜向，交给最短距离选择。
        /// </summary>
        public static AnchorArrangement Classify(NodeModel a, NodeModel b)
        {
            double hGap = Math.Max(b.X - (a.X + a.Width), a.X - (b.X + b.Width));
            double vGap = Math.Max(b.Y - (a.Y + a.Height), a.Y - (b.Y + b.Height));
            if (hGap > 0 && vGap <= 0) return AnchorArrangement.Horizontal;
            if (vGap > 0 && hGap <= 0) return AnchorArrangement.Vertical;
            return AnchorArrangement.Diagonal;
        }

        /// <summary>
        /// 按当前图形位置重算连接线两端的描点。手动指定的端保持不变，自动端按排列语义或最短距离重算。
        /// </summary>
        public static void Resolve(ConnectionModel c, NodeModel a, NodeModel b)
        {
            if (c == null || a == null || b == null) return;
            if (c.FromAnchorManual && c.ToAnchorManual) return;

            AnchorSide from = c.FromAnchor;
            AnchorSide to = c.ToAnchor;
            switch (Classify(a, b))
            {
                case AnchorArrangement.Horizontal:
                {
                    bool aIsLeft = a.X + a.Width / 2 <= b.X + b.Width / 2;
                    from = aIsLeft ? AnchorSide.Right : AnchorSide.Left;
                    to = aIsLeft ? AnchorSide.Left : AnchorSide.Right;
                    break;
                }
                case AnchorArrangement.Vertical:
                {
                    bool aIsTop = a.Y + a.Height / 2 <= b.Y + b.Height / 2;
                    from = aIsTop ? AnchorSide.Bottom : AnchorSide.Top;
                    to = aIsTop ? AnchorSide.Top : AnchorSide.Bottom;
                    break;
                }
                default:
                {
                    // 斜向排列：在所有可用组合里取最短，已经手动指定的一端固定不动。
                    double best = double.MaxValue;
                    foreach (var sa in AllSides)
                    {
                        if (c.FromAnchorManual && sa != c.FromAnchor) continue;
                        foreach (var sb in AllSides)
                        {
                            if (c.ToAnchorManual && sb != c.ToAnchor) continue;
                            double d = (AnchorPoint(a, sa) - AnchorPoint(b, sb)).LengthSquared;
                            if (d < best) { best = d; from = sa; to = sb; }
                        }
                    }
                    break;
                }
            }

            if (!c.FromAnchorManual) c.FromAnchor = from;
            if (!c.ToAnchorManual) c.ToAnchor = to;
        }

        /// <summary>按两端描点方向生成正交折线，首尾点就是描点实际坐标。</summary>
        public static List<Point> BuildRoute(Point from, Point to, AnchorSide fromSide, AnchorSide toSide)
        {
            var points = new List<Point> { from };
            Vector normalFrom = OutwardNormal(fromSide), normalTo = OutwardNormal(toSide);
            Point stubFrom = from + normalFrom * StubLength;
            Point stubTo = to + normalTo * StubLength;
            points.Add(stubFrom);

            bool fromHorizontal = fromSide == AnchorSide.Left || fromSide == AnchorSide.Right;
            bool toHorizontal = toSide == AnchorSide.Left || toSide == AnchorSide.Right;

            if (fromHorizontal && toHorizontal)
            {
                double busX;
                if (fromSide == toSide)
                    busX = fromSide == AnchorSide.Right
                        ? Math.Max(stubFrom.X, stubTo.X) + StubLength
                        : Math.Min(stubFrom.X, stubTo.X) - StubLength;
                else busX = (stubFrom.X + stubTo.X) / 2;
                points.Add(new Point(busX, stubFrom.Y));
                points.Add(new Point(busX, stubTo.Y));
            }
            else if (!fromHorizontal && !toHorizontal)
            {
                double busY;
                if (fromSide == toSide)
                    busY = fromSide == AnchorSide.Bottom
                        ? Math.Max(stubFrom.Y, stubTo.Y) + StubLength
                        : Math.Min(stubFrom.Y, stubTo.Y) - StubLength;
                else busY = (stubFrom.Y + stubTo.Y) / 2;
                points.Add(new Point(stubFrom.X, busY));
                points.Add(new Point(stubTo.X, busY));
            }
            else if (fromHorizontal)
            {
                points.Add(new Point(stubTo.X, stubFrom.Y));
            }
            else
            {
                points.Add(new Point(stubFrom.X, stubTo.Y));
            }

            points.Add(stubTo);
            points.Add(to);
            return Simplify(points);
        }

        /// <summary>折线按长度计的中间点，用于放置连接 Tag。</summary>
        public static Point MidPoint(List<Point> points)
        {
            if (points == null || points.Count == 0) return new Point();
            if (points.Count == 1) return points[0];

            double total = 0;
            for (int i = 1; i < points.Count; i++) total += (points[i] - points[i - 1]).Length;
            double half = total / 2, walked = 0;
            for (int i = 1; i < points.Count; i++)
            {
                double segment = (points[i] - points[i - 1]).Length;
                if (segment > 0 && walked + segment >= half)
                {
                    double t = (half - walked) / segment;
                    return points[i - 1] + (points[i] - points[i - 1]) * t;
                }
                walked += segment;
            }
            return points[points.Count - 1];
        }

        private static List<Point> Simplify(List<Point> points)
        {
            var result = new List<Point>();
            foreach (var p in points)
            {
                if (result.Count > 0 && (result[result.Count - 1] - p).LengthSquared < 0.01) continue;
                if (result.Count >= 2)
                {
                    Point a = result[result.Count - 2], b = result[result.Count - 1];
                    Vector v1 = b - a, v2 = p - b;
                    bool collinear = Math.Abs(v1.X * v2.Y - v1.Y * v2.X) < 0.01;
                    bool sameDirection = v1.X * v2.X + v1.Y * v2.Y >= 0;
                    if (collinear && sameDirection) { result[result.Count - 1] = p; continue; }
                }
                result.Add(p);
            }
            return result;
        }
    }
}
