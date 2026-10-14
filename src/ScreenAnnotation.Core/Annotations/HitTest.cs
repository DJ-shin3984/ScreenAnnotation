namespace ScreenAnnotation.Core.Annotations;

/// <summary>지우개 2차 판정용 순수 기하.</summary>
public static class HitTest
{
    /// <summary>점에서 폴리라인까지 최소 거리. 점이 하나면 그 점까지의 거리.</summary>
    public static double DistanceToPolyline(IReadOnlyList<PointF2> pts, float x, float y)
    {
        if (pts.Count == 0) return double.PositiveInfinity;
        var best = double.PositiveInfinity;
        for (var i = 0; i < Math.Max(1, pts.Count - 1); i++)
        {
            var a = pts[i];
            var b = pts[Math.Min(i + 1, pts.Count - 1)];
            best = Math.Min(best, DistanceToSegment(a, b, x, y));
        }
        return best;
    }

    private static double DistanceToSegment(PointF2 a, PointF2 b, float x, float y)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        var t = len2 == 0 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / len2, 0, 1);
        double px = a.X + t * dx - x, py = a.Y + t * dy - y;
        return Math.Sqrt(px * px + py * py);
    }
}
