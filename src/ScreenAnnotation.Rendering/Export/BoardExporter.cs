using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Rendering.Strokes;

namespace ScreenAnnotation.Rendering.Export;

/// <summary>정지 화면 + 판서(+ 그 순간 보이는 사라지는 획)를 원래 배율(1×)로 합성한다. 확대 상태와 무관.</summary>
public static class BoardExporter
{
    public static BitmapSource Compose(CapturedFrame frame, IReadOnlyList<Annotation> annotations, IReadOnlyList<EphemeralStrokes.Stroke> ephemeral)
    {
        var background = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Bgra, frame.Stride);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(background, new Rect(0, 0, frame.Width, frame.Height));
            foreach (var a in annotations.OrderBy(a => a.Order)) WpfOverlayRenderer.Draw(dc, a);
            foreach (var s in ephemeral) WpfOverlayRenderer.DrawEphemeral(dc, s);
        }
        var rtb = new RenderTargetBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>yyyyMMdd_HHmmss_{monitorIndex}.png 로 저장하고 경로를 돌려준다.</summary>
    public static string SavePng(BitmapSource image, string directory, int monitorIndex, DateTime now)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{now:yyyyMMdd_HHmmss}_{monitorIndex}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var fs = File.Create(path);
        encoder.Save(fs);
        return path;
    }

    /// <summary>클립보드가 다른 프로세스에 잠겨 있으면 3번까지 재시도한다.</summary>
    public static void CopyToClipboard(BitmapSource image)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetImage(image);
                return;
            }
            catch (COMException) when (attempt < 3)
            {
                Thread.Sleep(50);
            }
        }
    }
}
