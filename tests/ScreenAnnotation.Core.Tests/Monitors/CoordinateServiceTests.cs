using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Tests.Monitors;

public class CoordinateServiceTests
{
    private static readonly MonitorInfo Left = new("left", @"\\.\DISPLAY2",new RectPx(-1920, 0, 1920, 1080), new RectPx(-1920, 0, 1920, 1040), 96, 96, false);
    private static readonly MonitorInfo Uhd = new("uhd", @"\\.\DISPLAY1",new RectPx(0, 0, 3840, 2160), new RectPx(0, 0, 3840, 2100), 144, 144, true);

    [Fact]
    public void NegativeGlobalCoordinatesRoundTrip()
    {
        var global = new PointPx(-100, 50);
        var local = CoordinateService.GlobalToLocal(Left, global);
        local.Should().Be(new PointPx(1820, 50));
        CoordinateService.LocalToGlobal(Left, local).Should().Be(global);
    }

    [Fact]
    public void Dpi150PercentConvertsBothWays()
    {
        var dip = CoordinateService.LocalPxToDip(Uhd, new PointPx(384, 216));
        dip.Should().Be(new PointDip(256, 144));
        CoordinateService.DipToLocalPx(Uhd, dip).Should().Be(new PointPx(384, 216));
    }

    [Fact]
    public void FindMonitorPicksContainingOne()
    {
        var all = new[] { Left, Uhd };
        CoordinateService.FindMonitor(all, new PointPx(-1, 10)).Should().Be(Left);
        CoordinateService.FindMonitor(all, new PointPx(0, 10)).Should().Be(Uhd);
        CoordinateService.FindMonitor(all, new PointPx(5000, 10)).Should().BeNull();
    }
}
