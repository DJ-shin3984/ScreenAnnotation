using ScreenAnnotation.Core.Session;

namespace ScreenAnnotation.Core.Tests.Session;

public class ViewTransformTests
{
    [Fact]
    public void ZoomKeepsCursorPointFixedAndRoundTrips()
    {
        var v = new ViewTransform(1920, 1080);
        var (cx, cy) = v.ToContent(300, 200);
        for (var i = 0; i < 10; i++) v.ZoomAt(300, 200, zoomIn: true);
        v.Scale.Should().Be(8, "1.25^10 > 8이므로 상한");

        v.ToContent(300, 200).X.Should().BeApproximately(cx, 1);
        v.ToContent(300, 200).Y.Should().BeApproximately(cy, 1);

        // 8×에서 화면 (700,500)에 찍은 점 → 저장 좌표 → 같은 화면 위치
        var (sx, sy) = v.ToContent(700, 500);
        var back = v.ToScreen(sx, sy);
        back.X.Should().BeApproximately(700, 1);
        back.Y.Should().BeApproximately(500, 1);
    }

    [Fact]
    public void ContentDrawnAt8xStaysWithin1pxAfterReset()
    {
        var v = new ViewTransform(1920, 1080);
        for (var i = 0; i < 10; i++) v.ZoomAt(960, 540, true);
        var content = v.ToContent(1000, 600);            // 8×에서 그린 점의 저장 좌표
        var expected = v.ToScreen(content.X, content.Y); // 8×에서 보이던 위치
        expected.X.Should().BeApproximately(1000, 1);
        v.Reset();
        v.Scale.Should().Be(1);
        v.ToScreen(content.X, content.Y).Should().Be((content.X, content.Y), "1×에서는 저장 좌표가 곧 화면 좌표");
    }

    [Fact]
    public void PanClampsAtEdges()
    {
        var v = new ViewTransform(1000, 500);
        v.Pan(-100, -100);
        v.OffsetX.Should().Be(0, "1×에서는 이동 불가");
        v.ZoomAt(0, 0, true); // 1.25×, offset 0
        v.Pan(-10000, -10000);
        v.OffsetX.Should().BeApproximately(1000 - 1250, 1e-9);
        v.OffsetY.Should().BeApproximately(500 - 625, 1e-9);
        v.Pan(10000, 10000);
        v.OffsetX.Should().Be(0);
        v.ZoomAt(0, 0, false);
        v.IsIdentity.Should().BeTrue();
    }
}
