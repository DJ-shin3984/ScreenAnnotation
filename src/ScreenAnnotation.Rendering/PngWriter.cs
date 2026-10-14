using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenAnnotation.Core.Capture;

namespace ScreenAnnotation.Rendering;

public static class PngWriter
{
    public static void Save(CapturedFrame frame, string path)
    {
        var source = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Bgra, frame.Stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}
