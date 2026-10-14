using System.Windows;
using ScreenAnnotation.Rendering.Strokes;

namespace ScreenAnnotation.Rendering.Tests.Strokes;

public class StrokeBuilderTests
{
    [Fact]
    public void DropsSubPixelMoves()
    {
        var pts = new List<Point> { new(10, 10) };
        StrokeBuilder.ShouldAppend(pts, new Point(10.4, 10.6)).Should().BeFalse();
        StrokeBuilder.ShouldAppend(pts, new Point(11, 10)).Should().BeTrue();
        StrokeBuilder.ShouldAppend([], new Point(0, 0)).Should().BeTrue();
    }

    [Fact]
    public void SimplifyKeepsCornersDropsCollinear()
    {
        var line = Enumerable.Range(0, 50).Select(i => new Point(i, i * 0.1)).ToList();
        StrokeBuilder.Simplify(line).Should().HaveCount(2);

        var corner = new List<Point> { new(0, 0), new(50, 0), new(50, 50) };
        StrokeBuilder.Simplify(corner).Should().Equal(corner);
    }

    [Fact]
    public void GeometryPassesThroughPoints()
    {
        var pts = new List<Point> { new(0, 0), new(10, 20), new(30, 5), new(40, 40) };
        var g = StrokeBuilder.ToGeometry(pts);
        g.IsFrozen.Should().BeTrue();
        g.Bounds.Width.Should().BeGreaterThan(39);
        g.Bounds.Height.Should().BeGreaterThan(39);
        StrokeBuilder.ToGeometry([]).IsEmpty().Should().BeTrue();
    }
}
