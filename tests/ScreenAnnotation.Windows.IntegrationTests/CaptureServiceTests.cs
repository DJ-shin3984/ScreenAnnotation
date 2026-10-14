using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenAnnotation.Windows.Capture;
using ScreenAnnotation.Windows.Monitors;
using Xunit.Abstractions;

namespace ScreenAnnotation.Windows.IntegrationTests;

public class CaptureServiceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task CaptureAll_ReturnsOneFramePerMonitorWithPixels()
    {
        DpiAwareness.EnablePerMonitorV2(); // 테스트 러너는 매니페스트가 없어 가상화된 좌표를 받는다
        using var monitors = new MonitorService(NullLogger<MonitorService>.Instance);
        using var capture = new CaptureService(new XunitLogger<CaptureService>(output));
        capture.Warmup();

        var sw = Stopwatch.StartNew();
        var frames = await capture.CaptureAllAsync(monitors.Monitors);
        output.WriteLine($"CaptureAll {sw.Elapsed.TotalMilliseconds:F1}ms: " +
                         string.Join(", ", frames.Select(f => $"{f.Monitor.DeviceName} {f.Width}x{f.Height} {f.Method}")));

        sw.Restart();
        var again = await capture.CaptureAllAsync(monitors.Monitors);
        output.WriteLine($"CaptureAll(warm) {sw.Elapsed.TotalMilliseconds:F1}ms: " + string.Join(", ", again.Select(f => f.Method)));

        frames.Should().HaveCount(monitors.Monitors.Count);
        foreach (var f in frames)
        {
            f.Width.Should().Be(f.Monitor.Bounds.Width);
            f.Height.Should().Be(f.Monitor.Bounds.Height);
            f.Bgra.Length.Should().Be(f.Stride * f.Height);
            f.Bgra.Should().Contain(b => b != 0);
        }
    }
}
