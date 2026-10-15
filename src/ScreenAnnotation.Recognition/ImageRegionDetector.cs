using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Recognition;

/// <summary>
/// UIA가 실패한 화면에서 클릭 지점의 이미지 요소 경계를 추정한다 (2차 경로).
/// 바깥 링 최빈색 = 배경 → 배경과 다른 픽셀 마스크 → 2px 팽창 → 클릭 지점 포함 연결 영역의 경계 사각형.
/// </summary>
public static class ImageRegionDetector
{
    private const int ColorTolerance = 24;   // 배경과의 채널 최대 차이
    private const int DilateRadius = 2;
    private const int SnapRadius = 6;        // 클릭이 마스크 밖이면 이 거리 안의 마스크 픽셀로
    private const double MaxFraction = 0.8;  // 이보다 크면 "요소"가 아니라 화면 전체로 본다

    public static RectPx? Detect(CapturedFrame frame, int localX, int localY) =>
        Detect(frame.Bgra, frame.Width, frame.Height, frame.Stride, localX, localY);

    public static RectPx? Detect(ReadOnlySpan<byte> bgra, int width, int height, int stride, int clickX, int clickY, int searchRadius = 400)
    {
        if (clickX < 0 || clickY < 0 || clickX >= width || clickY >= height) return null;

        // 1. 탐색 창
        var x0 = Math.Max(0, clickX - searchRadius);
        var y0 = Math.Max(0, clickY - searchRadius);
        var x1 = Math.Min(width, clickX + searchRadius + 1);
        var y1 = Math.Min(height, clickY + searchRadius + 1);
        var w = x1 - x0;
        var h = y1 - y0;
        if (w < 8 || h < 8) return null;

        // 2. 배경색: 바깥 4px 링의 최빈색 (5비트 양자화)
        var (bb, bg, br) = EstimateBackground(bgra, stride, x0, y0, w, h);

        // 3. 마스크
        var mask = new bool[w * h];
        for (var y = 0; y < h; y++)
        {
            var row = (y0 + y) * stride + x0 * 4;
            for (var x = 0; x < w; x++)
            {
                var i = row + x * 4;
                var d = Math.Max(Math.Abs(bgra[i] - bb), Math.Max(Math.Abs(bgra[i + 1] - bg), Math.Abs(bgra[i + 2] - br)));
                mask[y * w + x] = d > ColorTolerance;
            }
        }

        // 4. 팽창
        mask = Dilate(mask, w, h, DilateRadius);

        // 5. 시작점: 클릭 지점, 아니면 근처 마스크 픽셀
        var sx = clickX - x0;
        var sy = clickY - y0;
        if (!mask[sy * w + sx] && !FindNearest(mask, w, h, ref sx, ref sy)) return null;

        // 연결 영역 (BFS)
        var minX = sx; var maxX = sx; var minY = sy; var maxY = sy;
        var visited = new bool[w * h];
        var queue = new Queue<int>();
        queue.Enqueue(sy * w + sx);
        visited[sy * w + sx] = true;
        while (queue.Count > 0)
        {
            var idx = queue.Dequeue();
            var x = idx % w;
            var y = idx / w;
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
            Push(x - 1, y); Push(x + 1, y); Push(x, y - 1); Push(x, y + 1);
        }

        // 6. 경계 (팽창분 되돌림) → 전체 이미지 좌표
        var rx = Math.Max(0, minX + DilateRadius);
        var ry = Math.Max(0, minY + DilateRadius);
        var rw = Math.Max(1, maxX - minX + 1 - 2 * DilateRadius);
        var rh = Math.Max(1, maxY - minY + 1 - 2 * DilateRadius);
        if (rw < 4 || rh < 4) return null;
        if (rw > width * MaxFraction || rh > height * MaxFraction) return null;
        return new RectPx(x0 + rx, y0 + ry, rw, rh);

        void Push(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            var i = y * w + x;
            if (visited[i] || !mask[i]) return;
            visited[i] = true;
            queue.Enqueue(i);
        }
    }

    private static (byte B, byte G, byte R) EstimateBackground(ReadOnlySpan<byte> bgra, int stride, int x0, int y0, int w, int h)
    {
        var counts = new Dictionary<int, int>();
        var bestKey = 0; var best = 0;
        for (var y = 0; y < h; y++)
        {
            var ring = y < 4 || y >= h - 4;
            for (var x = 0; x < w; x++)
            {
                if (!ring && x >= 4 && x < w - 4) continue;
                var i = (y0 + y) * stride + (x0 + x) * 4;
                var key = (bgra[i] >> 3) | ((bgra[i + 1] >> 3) << 5) | ((bgra[i + 2] >> 3) << 10);
                var c = counts.GetValueOrDefault(key) + 1;
                counts[key] = c;
                if (c > best) { best = c; bestKey = key; }
            }
        }
        // 양자화 구간의 중앙값으로 복원
        return ((byte)(((bestKey & 31) << 3) + 4), (byte)((((bestKey >> 5) & 31) << 3) + 4), (byte)((((bestKey >> 10) & 31) << 3) + 4));
    }

    private static bool[] Dilate(bool[] src, int w, int h, int r)
    {
        // 가로 → 세로 두 번의 1차원 팽창 = (2r+1)² 상자 팽창
        var tmp = new bool[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (!src[y * w + x]) continue;
                for (var dx = -r; dx <= r; dx++)
                {
                    var xx = x + dx;
                    if (xx >= 0 && xx < w) tmp[y * w + xx] = true;
                }
            }
        var dst = new bool[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (!tmp[y * w + x]) continue;
                for (var dy = -r; dy <= r; dy++)
                {
                    var yy = y + dy;
                    if (yy >= 0 && yy < h) dst[yy * w + x] = true;
                }
            }
        return dst;
    }

    private static bool FindNearest(bool[] mask, int w, int h, ref int sx, ref int sy)
    {
        var bestD = int.MaxValue; var bx = sx; var by = sy;
        for (var dy = -SnapRadius; dy <= SnapRadius; dy++)
            for (var dx = -SnapRadius; dx <= SnapRadius; dx++)
            {
                var x = sx + dx; var y = sy + dy;
                if (x < 0 || y < 0 || x >= w || y >= h || !mask[y * w + x]) continue;
                var d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; bx = x; by = y; }
            }
        if (bestD == int.MaxValue) return false;
        sx = bx; sy = by;
        return true;
    }
}
