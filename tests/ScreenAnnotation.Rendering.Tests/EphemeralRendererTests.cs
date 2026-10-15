using System.Windows;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Rendering.Strokes;

namespace ScreenAnnotation.Rendering.Tests;

/// <summary>회귀: 입력과 렌더러가 같은 시계를 써야 사라지는 펜이 실제로 사라진다.</summary>
public class EphemeralRendererTests
{
    [Fact]
    public void EphemeralStrokeExpiresOnFrameTick()
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                var w = new OverlayWindow(new MonitorInfo("k", @"\.\DISPLAY9", new RectPx(0, 0, 320, 200), new RectPx(0, 0, 320, 200), 96, 96, true));
                var r = new WpfOverlayRenderer(w, new AnnotationStore());
                var s = r.Ephemeral.Begin("#FF3B30", 3);
                s.Points.Add(new Point(10, 10));
                s.Points.Add(new Point(50, 40));
                r.NotifyEphemeralChanged();
                r.Ephemeral.End(MonotonicClock.Now - TimeSpan.FromSeconds(10)); // 이미 만료된 시각
                r.OnFrame(null, EventArgs.Empty);
                r.Ephemeral.Strokes.Should().BeEmpty("렌더러 틱이 입력과 같은 시계로 만료를 계산해야 한다");
                w.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        error.Should().BeNull();
    }
}
