using System.Windows;
using ScreenAnnotation.Rendering.Strokes;

namespace ScreenAnnotation.Rendering.Tests.Strokes;

public class EphemeralStrokesTests
{
    private static TimeSpan S(double s) => TimeSpan.FromSeconds(s);

    [Fact]
    public void EachStrokeExpiresOnItsOwnTimer()
    {
        var e = new EphemeralStrokes(); // Hold 1.0s, Fade 0.7s
        var a = e.Begin("#FF3B30", 3); a.Points.Add(new Point(0, 0));
        e.End(a, S(0));
        var b = e.Begin("#FF3B30", 3); b.Points.Add(new Point(5, 5));
        e.End(b, S(2));

        e.Tick(S(0.9)).Should().BeFalse();
        e.Strokes.Should().HaveCount(2).And.OnlyContain(s => s.Opacity == 1);

        e.Tick(S(1.35)).Should().BeTrue();
        a.Opacity.Should().BeInRange(0.4, 0.6, "첫 획은 페이드 중간");
        b.Opacity.Should().Be(1, "둘째 획은 아직 유지 시간");

        e.Tick(S(1.8)).Should().BeTrue();
        e.Strokes.Should().ContainSingle().Which.Should().BeSameAs(b);

        e.Tick(S(3.8)).Should().BeTrue();
        e.Strokes.Should().BeEmpty();
    }

    [Fact]
    public void StrokeBeingDrawnNeverFades()
    {
        var e = new EphemeralStrokes();
        var a = e.Begin("#000000", 1);
        e.Tick(S(100)).Should().BeFalse();
        e.Strokes.Should().ContainSingle();
        e.End(S(100)); // End() without a stroke = 가장 최근 미완료 획
        e.Tick(S(101.8)).Should().BeTrue();
        e.Strokes.Should().BeEmpty();
    }

    [Fact]
    public void HoldIsConfigurable()
    {
        var e = new EphemeralStrokes { Hold = S(3) };
        e.Begin("#000000", 1);
        e.End(S(0));
        e.Tick(S(2.9)).Should().BeFalse();
        e.Tick(S(3.8)).Should().BeTrue();
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
