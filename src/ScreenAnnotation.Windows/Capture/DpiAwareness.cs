using Windows.Win32.UI.HiDpi;
using static global::Windows.Win32.PInvoke;

namespace ScreenAnnotation.Windows.Capture;

/// <summary>매니페스트가 없는 프로세스(테스트 러너 등)를 PerMonitorV2로 만든다. 앱은 매니페스트로 설정한다.</summary>
public static class DpiAwareness
{
    public static bool EnablePerMonitorV2() => SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
}
