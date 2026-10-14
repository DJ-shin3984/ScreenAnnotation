using System.Windows;
using ScreenAnnotation.Rendering.Strokes;

namespace ScreenAnnotation.Rendering.Tests.Strokes;

public class EphemeralStrokesTests
{
    private static TimeSpan S(double s) => TimeSpan.FromSeconds(s);

    [Fact]
    public void AllStrokesExpireTogetherFromLastEnd()
    {
        var e = new EphemeralStrokes();
        e.Begin("#FF3B30", 3).Points.Add(new Point(0, 0));
        e.End(S(0));
        e.Begin("#FF3B30", 3).Points.Add(new Point(5, 5)); // 두 번째 획 시작 → 타이머 리셋
        e.Tick(S(10)).Should().BeFalse("그리는 중에는 사라지지 않음");
        e.Strokes.Should().HaveCount(2);

        e.End(S(10));
        e.Tick(S(12.4)).Should().BeFalse();
        e.Strokes.Should().OnlyContain(s => s.Opacity == 1);

        e.Tick(S(12.75)).Should().BeTrue();
        e.Strokes.Should().OnlyContain(s => s.Opacity > 0.4 && s.Opacity < 0.6, "페이드 중간");

        e.Tick(S(13.1)).Should().BeTrue();
        e.Strokes.Should().BeEmpty();
    }

    [Fact]
    public void HoldIsConfigurable()
    {
        var e = new EphemeralStrokes { Hold = S(1) };
        e.Begin("#000000", 1);
        e.End(S(0));
        e.Tick(S(1.6)).Should().BeTrue();
        e.Strokes.Should().BeEmpty();
    }

    [Fact]
    public void LaserTrailKeepsOnlyRecentSamples()
    {
        var l = new LaserTrail();
        l.Add(new Point(0, 0), S(0));
        l.Add(new Point(1, 1), S(0.2));
        l.Add(new Point(2, 2), S(0.4));
        l.Samples.Should().HaveCount(2);
        l.Head.Should().Be(new Point(2, 2));
        l.Prune(S(1)).Should().BeFalse();
        l.Head.Should().BeNull();
    }
}
