using System.IO;
using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;

namespace ScreenAnnotation.App;

/// <summary>
/// 클립보드에 이미지를 여러 형식으로 넣는다. WPF Clipboard.SetImage는 CF_BITMAP 하나만 넣어서
/// Chrome·카카오톡·그림판 등이 붙여넣기를 못 받는다. WinForms 경로는 CF_BITMAP + CF_DIB + DIBV5를 넣고, 여기에 PNG 스트림을 더한다.
/// </summary>
public static class ClipboardImage
{
    public static void Set(BitmapSource image)
    {
        var png = new MemoryStream();
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(image));
        enc.Save(png);
        png.Position = 0;

        using var bmp = new System.Drawing.Bitmap(png);                 // 24/32bpp GDI 비트맵 (DIB 변환용)
        using var flat = new System.Drawing.Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = System.Drawing.Graphics.FromImage(flat))
        {
            g.Clear(System.Drawing.Color.White);
            g.DrawImageUnscaled(bmp, 0, 0);
        }

        var data = new WinForms.DataObject();
        data.SetImage(flat);             // CF_BITMAP + CF_DIB + DIBV5
        png.Position = 0;
        data.SetData("PNG", false, png); // 투명도·무손실을 아는 앱용

        for (var attempt = 1; ; attempt++)
        {
            try { WinForms.Clipboard.SetDataObject(data, copy: true); return; }
            catch (System.Runtime.InteropServices.ExternalException) when (attempt < 3) { Thread.Sleep(50); }
        }
    }
}
