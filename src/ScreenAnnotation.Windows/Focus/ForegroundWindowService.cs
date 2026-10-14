using global::Windows.Win32;
using global::Windows.Win32.Foundation;

namespace ScreenAnnotation.Windows.Focus;

/// <summary>켜기 직전의 포그라운드 창을 기억했다가 끌 때 되돌린다.</summary>
public sealed class ForegroundWindowService
{
    public nint Capture() => PInvoke.GetForegroundWindow();

    /// <summary>이전 창에 포커스를 돌려준다. 창이 사라졌으면 false.</summary>
    public bool Restore(nint previous)
    {
        if (previous == 0) return false;
        var hwnd = new HWND(previous);
        if (!PInvoke.IsWindow(hwnd)) return false;
        // 오버레이가 방금 포그라운드였으므로 같은 프로세스에서 SetForegroundWindow가 허용된다.
        return PInvoke.SetForegroundWindow(hwnd);
    }
}
