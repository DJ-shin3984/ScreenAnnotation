using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Recognition;

/// <summary>OCR 결과를 단어 → 줄 → 문단으로 묶는다 (Ctrl+클릭 반복 확장용). 순수 로직.</summary>
public static class TextGrouping
{
    public static OcrWord? WordAt(OcrResult r, int x, int y) =>
        r.Lines.SelectMany(l => l.Words).FirstOrDefault(w => w.Bounds.Contains(x, y));

    public static OcrLine? LineAt(OcrResult r, int x, int y) =>
        r.Lines.FirstOrDefault(l => l.Bounds.Contains(x, y));

    /// <summary>클릭한 줄에서 위아래로, 세로 간격이 줄 높이의 1.5배 미만이고 가로 겹침이 30%를 넘는 줄들을 묶는다.</summary>
    public static (string Text, RectPx Bounds)? ParagraphAt(OcrResult r, int x, int y)
    {
        var lines = r.Lines.OrderBy(l => l.Bounds.Y).ToList();
        var hit = lines.FindIndex(l => l.Bounds.Contains(x, y));
        if (hit < 0) return null;

        var start = hit;
        while (start > 0 && Adjacent(lines[start - 1], lines[start])) start--;
        var end = hit;
        while (end < lines.Count - 1 && Adjacent(lines[end], lines[end + 1])) end++;

        var group = lines.GetRange(start, end - start + 1);
        var bounds = group.Select(l => l.Bounds).Aggregate(Union);
        return (string.Join("\n", group.Select(l => l.Text)), bounds);
    }

    private static bool Adjacent(OcrLine upper, OcrLine lower)
    {
        var height = Math.Max(1, Math.Min(upper.Bounds.Height, lower.Bounds.Height));
        var gap = lower.Bounds.Y - upper.Bounds.Bottom;
        if (gap >= height * 1.5) return false;

        var overlap = Math.Min(upper.Bounds.Right, lower.Bounds.Right) - Math.Max(upper.Bounds.X, lower.Bounds.X);
        var narrower = Math.Max(1, Math.Min(upper.Bounds.Width, lower.Bounds.Width));
        return overlap > narrower * 0.3;
    }

    private static RectPx Union(RectPx a, RectPx b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new RectPx(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }
}
