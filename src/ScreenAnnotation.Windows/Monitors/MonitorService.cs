using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Monitors;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;
using static global::Windows.Win32.PInvoke;

namespace ScreenAnnotation.Windows.Monitors;

/// <summary>
/// 모니터 목록 열거 + 변경 감지. 생성자는 메시지 펌프가 있는 스레드(WPF UI 스레드)에서 호출해야 한다:
/// WM_DISPLAYCHANGE·WM_DPICHANGED를 받는 메시지 전용 창을 그 스레드에 만들기 때문이다.
/// </summary>
public sealed unsafe class MonitorService : IMonitorService, IDisposable
{
    private const string ClassName = "ScreenAnnotation.MonitorWatch";
    private static readonly WNDPROC WndProcKeepAlive = WndProc; // GC가 델리게이트를 수거하지 않도록 고정
    private static MonitorService? s_instance;                   // ponytail: 창 하나 = 인스턴스 하나, 싱글톤으로 쓴다

    private readonly ILogger<MonitorService> _log;
    private HWND _hwnd;

    public MonitorService(ILogger<MonitorService> log)
    {
        _log = log;
        s_instance = this;
        Monitors = Enumerate();
        CreateMessageWindow();
    }

    public IReadOnlyList<MonitorInfo> Monitors { get; private set; }

    public event EventHandler? MonitorsChanged;

    private void Refresh()
    {
        Monitors = Enumerate();
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<MonitorInfo> Enumerate()
    {
        var paths = DevicePathsByGdiName();
        var list = new List<MonitorInfo>();
        var handles = new List<HMONITOR>();
        EnumDisplayMonitors(default, (RECT?)null, (h, _, _, _) => { handles.Add(h); return true; }, default);

        foreach (var h in handles)
        {
            var info = new MONITORINFOEXW();
            info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
            if (!GetMonitorInfo(h, (MONITORINFO*)&info)) continue;

            var gdiName = info.szDevice.ToString();
            if (!paths.TryGetValue(gdiName, out var key))
            {
                key = gdiName;
                _log.LogWarning("QueryDisplayConfig에서 {Gdi}의 monitorDevicePath를 찾지 못해 GDI 이름을 Key로 사용", gdiName);
            }

            uint dpiX = 96, dpiY = 96;
            if (GetDpiForMonitor(h, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dx, out var dy).Succeeded)
            {
                dpiX = dx;
                dpiY = dy;
            }

            list.Add(new MonitorInfo(
                key, gdiName,
                ToRect(info.monitorInfo.rcMonitor),
                ToRect(info.monitorInfo.rcWork),
                dpiX, dpiY,
                (info.monitorInfo.dwFlags & MONITORINFOF_PRIMARY) != 0));
        }

        foreach (var m in list)
        {
            _log.LogInformation("모니터 {Key} bounds={Bounds} work={Work} dpi={DpiX}x{DpiY} primary={Primary}",
                m.Key, m.Bounds, m.WorkArea, m.DpiX, m.DpiY, m.IsPrimary);
        }

        return list;
    }

    /// <summary>GDI 장치 이름(\\.\DISPLAY1) → monitorDevicePath.</summary>
    private Dictionary<string, string> DevicePathsByGdiName()
    {
        var map = new Dictionary<string, string>();
        const QUERY_DISPLAY_CONFIG_FLAGS flags = QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS;
        if (GetDisplayConfigBufferSizes(flags, out var pathCount, out var modeCount) != WIN32_ERROR.ERROR_SUCCESS) return map;

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        fixed (DISPLAYCONFIG_PATH_INFO* pp = paths)
        fixed (DISPLAYCONFIG_MODE_INFO* pm = modes)
        {
            if (QueryDisplayConfig(flags, ref pathCount, pp, ref modeCount, pm, null) != WIN32_ERROR.ERROR_SUCCESS) return map;
        }

        for (var i = 0; i < pathCount; i++)
        {
            var src = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
            src.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            src.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
            src.header.adapterId = paths[i].sourceInfo.adapterId;
            src.header.id = paths[i].sourceInfo.id;
            if (DisplayConfigGetDeviceInfo(&src.header) != 0) continue;

            var tgt = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
            tgt.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
            tgt.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
            tgt.header.adapterId = paths[i].targetInfo.adapterId;
            tgt.header.id = paths[i].targetInfo.id;
            if (DisplayConfigGetDeviceInfo(&tgt.header) != 0) continue;

            map[src.viewGdiDeviceName.ToString()] = tgt.monitorDevicePath.ToString();
        }

        return map;
    }

    private void CreateMessageWindow()
    {
        fixed (char* cls = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = WndProcKeepAlive,
                hInstance = new HINSTANCE(GetModuleHandle((PCWSTR)null).Value),
                lpszClassName = cls,
            };
            RegisterClassEx(in wc); // 이미 등록돼 있으면 실패해도 무방
            _hwnd = CreateWindowEx(0, cls, cls, 0, 0, 0, 0, 0, new HWND(-3) /* HWND_MESSAGE */, HMENU.Null, wc.hInstance, null);
        }
    }

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg is WM_DISPLAYCHANGE or WM_DPICHANGED) s_instance?.Refresh();
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private static RectPx ToRect(RECT r) => new(r.left, r.top, r.right - r.left, r.bottom - r.top);

    public void Dispose()
    {
        if (!_hwnd.IsNull) DestroyWindow(_hwnd);
        _hwnd = HWND.Null;
        if (s_instance == this) s_instance = null;
    }
}
