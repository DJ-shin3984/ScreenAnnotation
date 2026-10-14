using ScreenAnnotation.Core.Watchdog;

namespace ScreenAnnotation.Core.Tests.Watchdog;

public class HeartbeatTests
{
    [Fact]
    public void ActiveWithHandlesRoundTrips()
    {
        var hb = new Heartbeat(true, [0x1A2B, 0x3C4D]);
        hb.Format().Should().Be("1 0x1A2B 0x3C4D");
        Heartbeat.TryParse(hb.Format(), out var parsed).Should().BeTrue();
        parsed.Active.Should().BeTrue();
        parsed.OverlayHandles.Should().Equal(0x1A2B, 0x3C4D);
    }

    [Fact]
    public void InactiveRoundTrips()
    {
        Heartbeat.Inactive.Format().Should().Be("0");
        Heartbeat.TryParse("0", out var parsed).Should().BeTrue();
        parsed.Active.Should().BeFalse();
        parsed.OverlayHandles.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("2")]
    [InlineData("1 1A2B")]
    [InlineData("1 0xZZ")]
    public void GarbageIsRejected(string? line) => Heartbeat.TryParse(line, out _).Should().BeFalse();
}
