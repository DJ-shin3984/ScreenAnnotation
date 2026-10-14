using ScreenAnnotation.Core.Annotations;

namespace ScreenAnnotation.Core.Tests.Annotations;

public class HitTestTests
{
    private static readonly PointF2[] Line = [new(0, 0), new(100, 0), new(100, 100)];

    [Fact]
    public void NearSegmentMiddleIsPerpendicularDistance() =>
        HitTest.DistanceToPolyline(Line, 50, 7).Should().BeApproximately(7, 1e-6);

    [Fact]
    public void BeyondEndpointMeasuresToEndpoint() =>
        HitTest.DistanceToPolyline(Line, 103, 104).Should().BeApproximately(5, 1e-6);

    [Fact]
    public void CornerPicksClosestSegment() =>
        HitTest.DistanceToPolyline(Line, 97, 50).Should().BeApproximately(3, 1e-6);

    [Fact]
    public void SinglePointAndEmpty()
    {
        HitTest.DistanceToPolyline([new(3, 4)], 0, 0).Should().BeApproximately(5, 1e-6);
        HitTest.DistanceToPolyline([], 0, 0).Should().Be(double.PositiveInfinity);
    }
}
