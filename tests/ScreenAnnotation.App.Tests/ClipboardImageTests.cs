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
                // CF_DIB/DIBV5는 다른 프로세스가 읽을 때 OLE가 변환해 준다 — 같은 프로세스의 GetFormats()에는 안 보인다 (수동: 그림판 붙여넣기)
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
