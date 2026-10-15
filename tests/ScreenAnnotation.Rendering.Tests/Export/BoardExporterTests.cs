using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Rendering.Export;
using ScreenAnnotation.Rendering.Strokes;
using Xunit.Abstractions;

namespace ScreenAnnotation.Rendering.Tests.Export;

public class BoardExporterTests(ITestOutputHelper output)
{
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

    private static CapturedFrame GrayFrame(int w, int h)
    {
        var bgra = new byte[w * h * 4];
        for (var i = 0; i < bgra.Length; i += 4) { bgra[i] = 128; bgra[i + 1] = 128; bgra[i + 2] = 128; bgra[i + 3] = 255; }
        var monitor = new MonitorInfo("m", @"\\.\DISPLAY1", new RectPx(0, 0, w, h), new RectPx(0, 0, w, h), 96, 96, true);
        return new CapturedFrame(monitor, w, h, w * 4, bgra, "test");
    }

    private static PenStroke Stroke(PointF2 a, PointF2 b, string color, float width, StrokeKind kind = StrokeKind.Pen) => new()
    {
        Id = Guid.NewGuid(), MonitorKey = "m", Order = AnnotationIds.Next(),
        Bounds = new RectPx((int)Math.Min(a.X, b.X), (int)Math.Min(a.Y, b.Y), (int)Math.Abs(b.X - a.X) + 1, (int)Math.Abs(b.Y - a.Y) + 1),
        Kind = kind, Points = [a, b], Color = color, Width = width,
    };

    private static (byte B, byte G, byte R, byte A) Pixel(BitmapSource img, int x, int y)
    {
        var px = new byte[4];
        img.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return (px[0], px[1], px[2], px[3]);
    }

    [Fact]
    public void ComposeDrawsStrokesOverFrame() => Sta(() =>
    {
        var frame = GrayFrame(400, 300);
        var red = Stroke(new(50, 150), new(350, 150), "#FF3B30", 8);
        var yellow = Stroke(new(200, 20), new(200, 280), "#FFCC00", 16, StrokeKind.Highlighter);
        var eph = new EphemeralStrokes().Begin("#007AFF", 6);
        eph.Points.AddRange([new Point(20, 20), new Point(120, 20)]);

        var img = BoardExporter.Compose(frame, [red, yellow], [eph]);

        img.PixelWidth.Should().Be(400);
        var onRed = Pixel(img, 100, 150);
        onRed.R.Should().BeGreaterThan(200); onRed.G.Should().BeLessThan(100);
        var onBlue = Pixel(img, 70, 20);
        onBlue.B.Should().BeGreaterThan(200); onBlue.R.Should().BeLessThan(100);
        var onYellow = Pixel(img, 200, 100); // 0.4 불투명 노랑이 회색 위에
        onYellow.R.Should().BeGreaterThan(150); onYellow.B.Should().BeLessThan(120);
        var gray = Pixel(img, 10, 290);
        gray.Should().Be(((byte)128, (byte)128, (byte)128, (byte)255));
    });

    [Fact]
    public void SavePngWritesNamedFile() => Sta(() =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "sa-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var img = BoardExporter.Compose(GrayFrame(40, 30), [], []);
            var path = BoardExporter.SavePng(img, dir, 2, new DateTime(2026, 10, 15, 9, 5, 7));
            Path.GetFileName(path).Should().Be("20261015_090507_2.png");
            File.Exists(path).Should().BeTrue();
            using var fs = File.OpenRead(path);
            new PngBitmapDecoder(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0].PixelWidth.Should().Be(40);
        }
        finally { Directory.Delete(dir, true); }
    });

    [Fact]
    public void Compose4KWith200StrokesIsFast() => Sta(() =>
    {
        var frame = GrayFrame(3840, 2160);
        var r = new Random(1);
        var strokes = Enumerable.Range(0, 200)
            .Select(_ => Stroke(new(r.Next(3840), r.Next(2160)), new(r.Next(3840), r.Next(2160)), "#FF3B30", 4))
            .ToList<Annotation>();
        BoardExporter.Compose(frame, strokes, []); // 워밍업(첫 호출은 WPF 초기화 포함)

        var sw = Stopwatch.StartNew();
        BoardExporter.Compose(frame, strokes, []);
        sw.Stop();
        output.WriteLine($"4K compose: {sw.Elapsed.TotalMilliseconds:F0} ms");
        sw.Elapsed.TotalMilliseconds.Should().BeLessThan(1500);
    });
}
