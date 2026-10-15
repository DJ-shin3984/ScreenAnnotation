using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;
using Xunit.Abstractions;

namespace ScreenAnnotation.Recognition.Tests;

public class WindowsMediaOcrServiceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task RecognizesRenderedText()
    {
        var ocr = new WindowsMediaOcrService(NullLogger<WindowsMediaOcrService>.Instance);
        if (!ocr.IsAvailable)
        {
            output.WriteLine("OCR 없음: " + ocr.UnavailableReason);
            return;
        }
        output.WriteLine("OCR 언어: " + ocr.LanguageTag);

        var frame = RenderText("HELLO 123", 400, 100);
        var result = await RegionOcr.CopyTextAsync(ocr, frame, new RectPx(0, 0, 400, 100), CancellationToken.None);
        output.WriteLine("인식: " + result);
        result.Replace(" ", "").Should().Contain("HELLO");

        var full = await ocr.RecognizeAsync(frame, new RectPx(-50, -50, 1000, 1000), CancellationToken.None);
        full.Lines.Should().NotBeEmpty("영역이 프레임 밖으로 나가도 잘라서 인식");
        full.Lines[0].Bounds.X.Should().BeGreaterThanOrEqualTo(0);
    }

    private static CapturedFrame RenderText(string text, int w, int h)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Arial"), 40, Brushes.Black, 1.0);
            dc.DrawText(ft, new Point(20, 25));
        }
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var bgra = new byte[w * 4 * h];
        rtb.CopyPixels(bgra, w * 4, 0);
        var monitor = new MonitorInfo("test", @"\\.\TEST", new RectPx(0, 0, w, h), new RectPx(0, 0, w, h), 96, 96, true);
        return new CapturedFrame(monitor, w, h, w * 4, bgra, "test");
    }
}
