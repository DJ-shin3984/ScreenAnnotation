using System.Windows;
using System.Windows.Media;

namespace ScreenAnnotation.Rendering.Strokes;

/// <summary>펜 획의 점 처리: 1px 미만 점 버림, Catmull-Rom → 베지어, RDP 단순화. 좌표는 물리 픽셀.</summary>
public static class StrokeBuilder
{
    /// <summary>직전 점과 1px 미만 차이면 false(버림).</summary>
    public static bool ShouldAppend(IReadOnlyList<Point> points, Point next)
    {
        if (points.Count == 0) return true;
        var last = points[^1];
        return Math.Abs(next.X - last.X) >= 1 || Math.Abs(next.Y - last.Y) >= 1;
    }

    /// <summary>Catmull-Rom 스플라인을 3차 베지어로 바꿔 부드러운 경로를 만든다.</summary>
    public static StreamGeometry ToGeometry(IReadOnlyList<Point> pts)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            if (pts.Count == 0) return Freeze(g);
            ctx.BeginFigure(pts[0], isFilled: false, isClosed: false);
            if (pts.Count == 1)
                ctx.LineTo(pts[0], true, false); // 점 하나 = 둥근 캡으로 점 찍기
            else if (pts.Count == 2)
                ctx.LineTo(pts[1], true, false);
            else
            {
                for (var i = 0; i < pts.Count - 1; i++)
                {
                    var p0 = pts[Math.Max(i - 1, 0)];
                    var p1 = pts[i];
                    var p2 = pts[i + 1];
                    var p3 = pts[Math.Min(i + 2, pts.Count - 1)];
                    var c1 = new Point(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6);
                    var c2 = new Point(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6);
                    ctx.BezierTo(c1, c2, p2, true, false);
                }
            }
        }
        return Freeze(g);
    }

    /// <summary>Ramer–Douglas–Peucker. 허용오차 기본 0.5px.</summary>
    public static List<Point> Simplify(IReadOnlyList<Point> pts, double tolerance = 0.5)
    {
        if (pts.Count < 3) return [.. pts];
        var keep = new bool[pts.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int, int)>();
        stack.Push((0, pts.Count - 1));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            var maxD = 0.0;
            var idx = -1;
            for (var i = a + 1; i < b; i++)
            {
                var d = Distance(pts[i], pts[a], pts[b]);
                if (d > maxD) { maxD = d; idx = i; }
            }
            if (idx >= 0 && maxD > tolerance)
            {
                keep[idx] = true;
                stack.Push((a, idx));
                stack.Push((idx, b));
            }
        }
        var result = new List<Point>();
        for (var i = 0; i < pts.Count; i++) if (keep[i]) result.Add(pts[i]);
        return result;
    }

    private static double Distance(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        if (len2 == 0) return (p - a).Length;
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
        return (p - new Point(a.X + t * dx, a.Y + t * dy)).Length;
    }

    private static StreamGeometry Freeze(StreamGeometry g)
    {
        g.Freeze();
        return g;
    }
}
