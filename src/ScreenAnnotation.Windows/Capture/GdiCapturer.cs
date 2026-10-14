using System.Runtime.InteropServices;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using static global::Windows.Win32.PInvoke;

namespace ScreenAnnotation.Windows.Capture;

/// <summary>최후 수단: 화면 DC에서 BitBlt. 느리지만 어디서나 된다.</summary>
public static unsafe class GdiCapturer
{
    public static CapturedFrame Capture(MonitorInfo monitor)
    {
        var b = monitor.Bounds;
        var stride = b.Width * 4;
        var bgra = new byte[stride * b.Height];

        var screenDc = GetDC(HWND.Null);
        if (screenDc == default) throw new InvalidOperationException("GetDC 실패");
        try
        {
            var memDc = CreateCompatibleDC(screenDc);
            if (memDc == default) throw new InvalidOperationException("CreateCompatibleDC 실패");
            try
            {
                Blit(screenDc, memDc, b, bgra);
            }
            finally
            {
                _ = DeleteDC(memDc);
            }
        }
        finally
        {
            _ = ReleaseDC(HWND.Null, screenDc);
        }

        return new CapturedFrame(monitor, b.Width, b.Height, stride, bgra, "GDI");
    }

    private static void Blit(HDC screenDc, HDC memDc, RectPx b, byte[] bgra)
    {
        var bmi = new BITMAPINFO();
        bmi.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        bmi.bmiHeader.biWidth = b.Width;
        bmi.bmiHeader.biHeight = -b.Height; // top-down
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = (uint)BI_COMPRESSION.BI_RGB;

        using var dib = CreateDIBSection(memDc, &bmi, DIB_USAGE.DIB_RGB_COLORS, out var bits, null, 0);
        if (dib.IsInvalid || bits == null) throw new InvalidOperationException("CreateDIBSection 실패");

        var old = SelectObject(memDc, (HGDIOBJ)dib.DangerousGetHandle());
        try
        {
            if (!BitBlt(memDc, 0, 0, b.Width, b.Height, screenDc, b.X, b.Y, ROP_CODE.SRCCOPY | ROP_CODE.CAPTUREBLT))
                throw new InvalidOperationException($"BitBlt 실패 (0x{Marshal.GetLastWin32Error():X})");
            new ReadOnlySpan<byte>(bits, bgra.Length).CopyTo(bgra);
        }
        finally
        {
            SelectObject(memDc, old);
        }
    }
}
