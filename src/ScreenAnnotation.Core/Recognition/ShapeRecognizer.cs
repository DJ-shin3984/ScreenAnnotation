using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Recognition;

/// <summary>펜으로 대충 그린 획을 직선·사각형·타원으로 판정한다. 기준은 계획서 "도형 자동 보정".</summary>
public static class ShapeRecognizer
{
    public static ShapeAnnotation? Recognize(PenStroke stroke)
    {
        var pts = stroke.Points;
        if (pts.Count < 3) return null;
        var length = PathLength(pts);
        if (length <= 20) return null;

        ShapeAnnotation Make(ShapeKind kind, PointPx a, PointPx b) => new()
        {
            Id = Guid.NewGuid(),
            MonitorKey = stroke.MonitorKey,
            Order = stroke.Order,
            Bounds = ShapeAnnotation.BoundsOf(a, b),
            Kind = kind,
            Start = a,
            End = b,
            Color = stroke.Color,
            Width = stroke.Width,
        };

        // 1. 직선: 시작–끝 현과 모든 점의 최대 거리 < 길이의 4%
        if (MaxChordDistance(pts) < length * 0.04f)
            return Make(ShapeKind.Line, ToPx(pts[0]), ToPx(pts[^1]));

        if (!IsClosed(pts)) return null;

        // 2. 사각형: 단순화 후 꼭짓점 4~5개 + 모서리 각 70°~110°
        var simplified = Simplify(pts, length * 0.03f);
        if (simplified.Count == 5 && Dist(simplified[0], simplified[^1]) < length * 0.15f)
            simplified.RemoveAt(simplified.Count - 1);
        if (simplified.Count == 4 && AllCornersRight(simplified))
        {
            var (min, max) = BoundingBox(simplified);
            return Make(ShapeKind.Rect, min, max);
        }

        // 3. 타원: 중심 거리 변동계수 < 0.15
        if (RadiusVariation(pts) < 0.15f)
        {
            var (min, max) = BoundingBox(pts);
            return Make(ShapeKind.Ellipse, min, max);
        }
        return null;
    }

    /// <summary>처음과 끝이 길이의 15% 또는 20px 이내.</summary>
    public static bool IsClosed(IReadOnlyList<PointF2> pts)
    {
        if (pts.Count < 3) return false;
        var gap = Dist(pts[0], pts[^1]);
        return gap < 20 || gap < PathLength(pts) * 0.15f;
    }

    /// <summary>Ramer–Douglas–Peucker.</summary>
    public static List<PointF2> Simplify(IReadOnlyList<PointF2> pts, float tolerance)
    {
        if (pts.Count < 3) return [.. pts];
        var keep = new bool[pts.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int, int)>();
        stack.Push((0, pts.Count - 1));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            var maxD = 0f;
            var idx = -1;
            for (var i = a + 1; i < b; i++)
            {
                var d = SegmentDistance(pts[i], pts[a], pts[b]);
                if (d > maxD) { maxD = d; idx = i; }
            }
            if (idx >= 0 && maxD > tolerance)
            {
                keep[idx] = true;
                stack.Push((a, idx));
                stack.Push((idx, b));
            }
        }
        var result = new List<PointF2>();
        for (var i = 0; i < pts.Count; i++) if (keep[i]) result.Add(pts[i]);
        return result;
    }

    private static bool AllCornersRight(List<PointF2> v)
    {
        for (var i = 0; i < v.Count; i++)
        {
            var p = v[(i + v.Count - 1) % v.Count];
            var c = v[i];
            var n = v[(i + 1) % v.Count];
            var ax = p.X - c.X; var ay = p.Y - c.Y;
            var bx = n.X - c.X; var by = n.Y - c.Y;
            var cos = (ax * bx + ay * by) / (MathF.Sqrt(ax * ax + ay * ay) * MathF.Sqrt(bx * bx + by * by));
            var deg = MathF.Acos(Math.Clamp(cos, -1, 1)) * 180 / MathF.PI;
            if (deg < 70 || deg > 110) return false;
        }
        return true;
    }

    private static float RadiusVariation(IReadOnlyList<PointF2> pts)
    {
        float cx = 0, cy = 0;
        foreach (var p in pts) { cx += p.X; cy += p.Y; }
        cx /= pts.Count; cy /= pts.Count;
        var radii = pts.Select(p => MathF.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy))).ToArray();
        var mean = radii.Average();
        if (mean <= 0) return float.MaxValue;
        var std = MathF.Sqrt(radii.Sum(r => (r - mean) * (r - mean)) / radii.Length);
        return std / mean;
    }

    private static float MaxChordDistance(IReadOnlyList<PointF2> pts)
    {
        var max = 0f;
        for (var i = 1; i < pts.Count - 1; i++) max = Math.Max(max, SegmentDistance(pts[i], pts[0], pts[^1]));
        return max;
    }

    private static float PathLength(IReadOnlyList<PointF2> pts)
    {
        var len = 0f;
        for (var i = 1; i < pts.Count; i++) len += Dist(pts[i - 1], pts[i]);
        return len;
    }

    private static float SegmentDistance(PointF2 p, PointF2 a, PointF2 b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        if (len2 == 0) return Dist(p, a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
        return Dist(p, new PointF2(a.X + t * dx, a.Y + t * dy));
    }

    private static float Dist(PointF2 a, PointF2 b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static PointPx ToPx(PointF2 p) => new((int)MathF.Round(p.X), (int)MathF.Round(p.Y));

    private static (PointPx Min, PointPx Max) BoundingBox(IReadOnlyList<PointF2> pts)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in pts)
        {
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
        }
        return (ToPx(new PointF2(minX, minY)), ToPx(new PointF2(maxX, maxY)));
    }
}
