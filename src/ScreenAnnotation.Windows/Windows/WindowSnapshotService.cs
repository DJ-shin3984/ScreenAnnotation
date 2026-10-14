using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Windows;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.WindowsAndMessaging;
using static global::Windows.Win32.PInvoke;

namespace ScreenAnnotation.Windows.Windows;

public sealed class WindowSnapshotService(ILogger<WindowSnapshotService> logger) : IWindowSnapshotService
{
    private static readonly uint OwnPid = (uint)Environment.ProcessId;

    public unsafe WindowSnapshot Take()
    {
        var list = new List<WindowInfo>();
        // EnumWindows는 Z-order(맨 위부터) 순으로 호출한다.
        EnumWindows((hwnd, _) =>
        {
            var info = Describe(hwnd);
            if (info is not null) list.Add(info);
            return true;
        }, 0);

        var snapshot = new WindowSnapshot(list, DateTimeOffset.Now);
        if (logger.IsEnabled(LogLevel.Debug))
            foreach (var w in list)
                logger.LogDebug("0x{Handle:X} \"{Title}\" {Bounds} pid={Pid} elevated={Elevated}",
                    w.Handle, w.Title, w.Bounds, w.ProcessId, w.IsElevated);
        return snapshot;
    }

    private static unsafe WindowInfo? Describe(HWND hwnd)
    {
        if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return null;
        if ((GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) & (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0) return null;

        uint pid;
        GetWindowThreadProcessId(hwnd, &pid);
        if (pid == OwnPid) return null;

        uint cloaked;
        if (DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).Succeeded && cloaked != 0)
            return null;

        RECT rect;
        if (DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, (uint)sizeof(RECT)).Failed
            && !GetWindowRect(hwnd, &rect))
            return null;
        var bounds = new RectPx(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;

        return new WindowInfo(hwnd.Value, Title(hwnd), bounds, pid, IsElevated(pid));
    }

    private static unsafe string Title(HWND hwnd)
    {
        int len = GetWindowTextLength(hwnd);
        if (len == 0) return "";
        Span<char> buf = stackalloc char[len + 1];
        fixed (char* p = buf) len = GetWindowText(hwnd, p, buf.Length);
        return new string(buf[..len]);
    }

    private static unsafe bool IsElevated(uint pid)
    {
        using var process = OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process.IsInvalid) return true; // 접근 거부 = 상위 권한으로 간주
        if (!OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token)) return true;
        using (token)
        {
            TOKEN_ELEVATION elevation;
            return !GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenElevation, &elevation, (uint)sizeof(TOKEN_ELEVATION), out _)
                || elevation.TokenIsElevated != 0;
        }
    }
}
