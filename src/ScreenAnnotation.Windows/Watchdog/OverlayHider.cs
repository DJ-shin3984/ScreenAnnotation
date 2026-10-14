using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ScreenAnnotation.Windows.Watchdog;

/// <summary>워치독이 다른 프로세스(App)의 오버레이 창을 강제로 숨길 때 쓴다.</summary>
public static class OverlayHider
{
    public static bool Hide(long hwnd) => global::Windows.Win32.PInvoke.ShowWindow(new HWND((nint)hwnd), SHOW_WINDOW_CMD.SW_HIDE);
}
