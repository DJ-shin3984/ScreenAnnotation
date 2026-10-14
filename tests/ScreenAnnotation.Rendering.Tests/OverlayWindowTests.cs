using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Rendering;

namespace ScreenAnnotation.Rendering.Tests;

public class OverlayWindowTests
{
    [Fact]
    public void Constructs_on_sta_thread_with_valid_handle()
    {
        nint handle = 0;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                var monitor = new MonitorInfo("test", @"\\.\DISPLAY1", new RectPx(0, 0, 800, 600), new RectPx(0, 0, 800, 560), 96, 96, true);
                var w = new OverlayWindow(monitor);
                handle = w.Handle;
                w.Close();
            }
            catch (Exception e) { error = e; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();

        error.Should().BeNull();
        handle.Should().NotBe(0);
    }
}
