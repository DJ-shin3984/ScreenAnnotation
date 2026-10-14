using System.Windows;
using System.Windows.Media;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Rendering.Tests;

public class RendererTests
{
    private const string Key = "test";

    private static void Sta(Action body)
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try { body(); } catch (Exception e) { error = e; }
            finally { System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        error.Should().BeNull();
    }

    private static OverlayWindow Window() =>
        new(new MonitorInfo(Key, @"\\.\DISPLAY1", new RectPx(0, 0, 800, 600), new RectPx(0, 0, 800, 560), 96, 96, true));

    private static PenStroke Stroke(Random r, string color = "#FF3B30")
    {
        var pts = Enumerable.Range(0, 20).Select(_ => new PointF2(r.Next(800), r.Next(600))).ToList();
        return new PenStroke
        {
            Id = Guid.NewGuid(), MonitorKey = Key, Order = AnnotationIds.Next(), Bounds = new RectPx(0, 0, 800, 600),
            Kind = StrokeKind.Pen, Points = pts, Color = color, Width = 3,
        };
    }

    private static double AvgLiveMs(WpfOverlayRenderer r)
    {
        var live = new LiveStroke { Kind = StrokeKind.Pen, Color = "#007AFF", Width = 3 };
        var total = 0.0;
        for (var i = 0; i < 50; i++)
        {
            live.Points.Add(new Point(i * 5, 100 + (i % 7) * 10));
            r.UpdateLiveStroke(live);
            total += r.LastL3FrameMs;
        }
        return total / 50;
    }

    [Fact]
    public void L3FrameTimeIndependentOfAnnotationCount() => Sta(() =>
    {
        var rnd = new Random(1);
        var w1 = Window();
        var empty = new WpfOverlayRenderer(w1, new AnnotationStore());
        AvgLiveMs(empty); // JIT 워밍업
        var baseline = AvgLiveMs(empty);

        var store = new AnnotationStore();
        var history = new HistoryManager(store);
        for (var i = 0; i < 1000; i++) history.Execute(new AddCommand(Stroke(rnd)));
        var w2 = Window();
        var r = new WpfOverlayRenderer(w2, store);
        r.InvalidateAll();
        var loaded = AvgLiveMs(r);
        w1.Close();
        w2.Close();

        loaded.Should().BeLessThan(4);
        (loaded / Math.Max(baseline, 0.05)).Should().BeLessThan(3);
    });

    [Fact]
    public void L2RendersStrokeAndRect() => Sta(() =>
    {
        var store = new AnnotationStore();
        var history = new HistoryManager(store);
        history.Execute(new AddCommand(new PenStroke
        {
            Id = Guid.NewGuid(), MonitorKey = Key, Order = AnnotationIds.Next(), Bounds = new RectPx(100, 100, 200, 1),
            Kind = StrokeKind.Pen, Points = [new(100, 100), new(300, 100)], Color = "#FF0000", Width = 6,
        }));
        history.Execute(new AddCommand(new ShapeAnnotation
        {
            Id = Guid.NewGuid(), MonitorKey = Key, Order = AnnotationIds.Next(), Bounds = new RectPx(400, 300, 100, 100),
            Kind = ShapeKind.Rect, Start = new PointPx(400, 300), End = new PointPx(500, 400), Color = "#0000FF", Width = 4,
        }));
        var w = Window();
        var r = new WpfOverlayRenderer(w, store);
        r.Start();

        var l2 = (System.Windows.Controls.Image)w.Root.Children[1];
        var bmp = (System.Windows.Media.Imaging.RenderTargetBitmap)l2.Source;
        var px = new byte[800 * 600 * 4];
        bmp.CopyPixels(px, 800 * 4, 0);
        byte[] At(int x, int y) => px[((y * 800 + x) * 4)..((y * 800 + x) * 4 + 4)]; // B G R A
        w.Close();

        At(200, 100)[2].Should().BeGreaterThan(200, "획 위는 빨강");
        At(200, 100)[3].Should().BeGreaterThan(200);
        At(400, 350)[0].Should().BeGreaterThan(200, "사각형 왼쪽 변은 파랑");
        At(50, 500)[3].Should().Be(0, "빈 곳은 투명");
    });
}
