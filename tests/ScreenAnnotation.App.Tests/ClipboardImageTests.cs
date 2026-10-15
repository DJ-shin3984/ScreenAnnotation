using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenAnnotation.App;

namespace ScreenAnnotation.App.Tests;

public class ClipboardImageTests
{
    [Fact]
    public void PutsPngAndDibFormatsOnClipboard()
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                var bmp = new RenderTargetBitmap(40, 30, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen()) dc.DrawRectangle(System.Windows.Media.Brushes.Red, null, new System.Windows.Rect(0, 0, 40, 30));
                bmp.Render(dv);

                ClipboardImage.Set(bmp);

                var formats = System.Windows.Forms.Clipboard.GetDataObject()!.GetFormats();
                formats.Should().Contain("PNG");
                formats.Should().Contain(System.Windows.Forms.DataFormats.Dib, "그림판·브라우저가 받는 CF_DIB");
                System.Windows.Forms.Clipboard.ContainsImage().Should().BeTrue();
            }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        error.Should().BeNull();
    }
}
