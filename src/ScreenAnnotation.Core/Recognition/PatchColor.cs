using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Recognition;

/// <summary>구멍 메움 색: 요소 테두리 바깥 2px 띠의 채널별 중앙값. 단색 배경용 (ponytail: 사진·그라데이션은 어색, 복원 알고리즘은 Advanced).</summary>
public static class PatchColor
{
    public static string MedianRing(ReadOnlySpan<byte> bgra, int width, int height, int stride, RectPx r, int ring = 2)
    {
        var bs = new List<byte>(); var gs = new List<byte>(); var rs = new List<byte>();
        for (var y = r.Y - ring; y < r.Y + r.Height + ring; y++)
        {
            if (y < 0 || y >= height) continue;
            var rowInside = y >= r.Y && y < r.Y + r.Height;
            for (var x = r.X - ring; x < r.X + r.Width + ring; x++)
            {
                if (x < 0 || x >= width) continue;
                if (rowInside && x >= r.X && x < r.X + r.Width) continue; // 안쪽은 제외
                var i = y * stride + x * 4;
                bs.Add(bgra[i]); gs.Add(bgra[i + 1]); rs.Add(bgra[i + 2]);
            }
        }
        if (bs.Count == 0) return "#FFFFFF";
        return $"#{Median(rs):X2}{Median(gs):X2}{Median(bs):X2}";
    }

    private static byte Median(List<byte> v)
    {
        v.Sort();
        return v[v.Count / 2];
    }
}
