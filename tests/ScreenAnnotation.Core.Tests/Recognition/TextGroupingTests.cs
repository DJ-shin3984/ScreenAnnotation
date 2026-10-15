using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Recognition;

namespace ScreenAnnotation.Core.Tests.Recognition;

public class TextGroupingTests
{
    // 줄 높이 20, 줄 간격 5 → 3줄 문단, 60px 빈 틈, 2줄 문단
    private static readonly OcrResult Sample = new(
    [
        Line("one two", 10, 0),
        Line("three", 10, 25),
        Line("four", 10, 50),
        Line("five", 10, 130),
        Line("six", 10, 155),
    ]);

    private static OcrLine Line(string text, int x, int y)
    {
        var words = new List<OcrWord>();
        var cx = x;
        foreach (var w in text.Split(' '))
        {
            words.Add(new OcrWord(w, new RectPx(cx, y, w.Length * 10, 20)));
            cx += w.Length * 10 + 10;
        }
        return new OcrLine(text, new RectPx(x, y, cx - x - 10, 20), words);
    }

    [Fact]
    public void ParagraphGrowsAcrossSmallGapsOnly()
    {
        var p1 = TextGrouping.ParagraphAt(Sample, 15, 30);
        p1!.Value.Text.Should().Be("one two\nthree\nfour");
        p1.Value.Bounds.Should().Be(new RectPx(10, 0, 70, 70));

        var p2 = TextGrouping.ParagraphAt(Sample, 15, 160);
        p2!.Value.Text.Should().Be("five\nsix");

        TextGrouping.ParagraphAt(Sample, 15, 100).Should().BeNull("빈 틈");
    }

    [Fact]
    public void WordAndLineHit()
    {
        TextGrouping.WordAt(Sample, 55, 5)!.Text.Should().Be("two");
        TextGrouping.LineAt(Sample, 55, 5)!.Text.Should().Be("one two");
        TextGrouping.WordAt(Sample, 500, 5).Should().BeNull();
    }

    [Fact]
    public void ResultTextJoinsLines() => Sample.Text.Should().Be("one two\nthree\nfour\nfive\nsix");
}
