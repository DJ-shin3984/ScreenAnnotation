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
        data.SetImage(flat);                                   // CF_BITMAP
        data.SetData(WinForms.DataFormats.Dib, false, ToDib(flat)); // CF_DIB: 브라우저·메신저·그림판이 가장 널리 받는 형식
        png.Position = 0;
        data.SetData("PNG", false, png);                       // 투명도·무손실을 아는 앱용

        for (var attempt = 1; ; attempt++)
        {
            try { WinForms.Clipboard.SetDataObject(data, copy: true); return; }
            catch (System.Runtime.InteropServices.ExternalException) when (attempt < 3) { Thread.Sleep(50); }
        }
    }

    /// <summary>24bpp 비트맵을 CF_DIB 바이트(BITMAPINFOHEADER + 아래→위 행, 4바이트 정렬)로.</summary>
    private static MemoryStream ToDib(System.Drawing.Bitmap bmp)
    {
        var w = bmp.Width; var h = bmp.Height;
        var stride = (w * 3 + 3) & ~3;
        var ms = new MemoryStream(40 + stride * h);
        var bw = new BinaryWriter(ms);
        bw.Write(40); bw.Write(w); bw.Write(h); bw.Write((short)1); bw.Write((short)24);
        bw.Write(0); bw.Write(stride * h); bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0); // BI_RGB, 72dpi
        var rect = new System.Drawing.Rectangle(0, 0, w, h);
        var bits = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[stride];
            for (var y = h - 1; y >= 0; y--)
            {
                System.Runtime.InteropServices.Marshal.Copy(bits.Scan0 + y * bits.Stride, row, 0, w * 3);
                ms.Write(row, 0, stride);
            }
        }
        finally { bmp.UnlockBits(bits); }
        ms.Position = 0;
        return ms;
    }
}
