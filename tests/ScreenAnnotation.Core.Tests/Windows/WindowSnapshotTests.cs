using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Windows;

namespace ScreenAnnotation.Core.Tests.Windows;

public class WindowSnapshotTests
{
    private static readonly WindowSnapshot Snapshot = new(
    [
        new WindowInfo(1, "top", new RectPx(100, 100, 200, 200), 10, false),
        new WindowInfo(2, "bottom", new RectPx(0, 0, 1000, 1000), 20, false),
    ], DateTimeOffset.UnixEpoch);

    [Fact]
    public void HitTest_ReturnsTopMostContainingWindow() => Snapshot.HitTest(150, 150)!.Handle.Should().Be(1);

    [Fact]
    public void HitTest_FallsThroughToLowerWindow() => Snapshot.HitTest(50, 50)!.Handle.Should().Be(2);

    [Fact]
    public void HitTest_ReturnsNullOutside() => Snapshot.HitTest(2000, 2000).Should().BeNull();
}
