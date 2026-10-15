using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Recognition;

namespace ScreenAnnotation.Core.Tests.Recognition;

public class ElementSelectionTests
{
    [Fact]
    public void NextCyclesThroughLevels()
    {
        ElementSelection.Next(3, 0).Should().Be(1);
        ElementSelection.Next(3, 2).Should().Be(0, "끝이면 처음으로");
        ElementSelection.Next(0, 5).Should().Be(0);
    }

    [Fact]
    public void SamePointWithinTolerance()
    {
        ElementSelection.SamePoint(100, 100, 103, 97).Should().BeTrue();
        ElementSelection.SamePoint(100, 100, 105, 100).Should().BeFalse();
    }

    [Fact]
    public void MedianRingIgnoresInsideAndPicksBackground()
    {
        // 20x20 흰 배경, 가운데 10x10 빨강
        const int w = 20, h = 20, stride = w * 4;
        var bgra = new byte[stride * h];
        for (var i = 0; i < bgra.Length; i += 4) { bgra[i] = 255; bgra[i + 1] = 255; bgra[i + 2] = 255; bgra[i + 3] = 255; }
        for (var y = 5; y < 15; y++)
            for (var x = 5; x < 15; x++) { var i = y * stride + x * 4; bgra[i] = 0; bgra[i + 1] = 0; bgra[i + 2] = 255; }

        PatchColor.MedianRing(bgra, w, h, stride, new RectPx(5, 5, 10, 10)).Should().Be("#FFFFFF");
        PatchColor.MedianRing(bgra, w, h, stride, new RectPx(7, 7, 2, 2)).Should().Be("#FF0000", "띠가 빨강 안쪽에 있음");
    }
}
