using global::Windows.Win32.Foundation;
using global::Windows.Win32.UI.WindowsAndMessaging;
using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Windows.Overlay;

/// <summary>오버레이 창을 물리 픽셀 경계에 맞추고 도구 창(Alt+Tab 제외)으로 만든다.</summary>
public static class OverlayNative
{
    public static void FitToMonitor(nint hwnd, RectPx bounds)
    {
        global::Windows.Win32.PInvoke.SetWindowPos(
            new HWND(hwnd), HWND.HWND_TOPMOST,
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    public static void MakeToolWindow(nint hwnd)
    {
        var h = new HWND(hwnd);
        var ex = global::Windows.Win32.PInvoke.GetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        global::Windows.Win32.PInvoke.SetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, ex | (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);
    }

    /// <summary>UIA ElementFromPoint 질의 순간만 마우스 투과(WS_EX_TRANSPARENT|WS_EX_LAYERED)로 바꾼다.</summary>
    public static void SetClickThrough(nint hwnd, bool enabled)
    {
        var h = new HWND(hwnd);
        const int flags = (int)(WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_LAYERED);
        var ex = global::Windows.Win32.PInvoke.GetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        global::Windows.Win32.PInvoke.SetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, enabled ? ex | flags : ex & ~flags);
    }
}
