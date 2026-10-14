using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Rendering;

/// <summary>모니터별 OverlayWindow 묶음. UI 스레드에서만 쓴다.</summary>
public sealed class OverlayHost
{
    private readonly Dictionary<string, OverlayWindow> _windows = new();
    private bool _stale;

    public OverlayHost(IMonitorService monitors)
    {
        monitors.MonitorsChanged += (_, _) => _stale = true;
    }

    public bool IsVisible { get; private set; }

    public IReadOnlyList<nint> Handles => _windows.Values.Select(w => w.Handle).ToList();

    public IReadOnlyCollection<OverlayWindow> Windows => _windows.Values;

    /// <summary>시작 시 미리 창을 만들어 첫 켜기 지연(창 생성·첫 렌더)을 줄인다.</summary>
    public void Prepare(IReadOnlyList<MonitorInfo> monitors)
    {
        if (_stale) Recreate();
        foreach (var m in monitors) Get(m);
    }

    public void ShowAll(IReadOnlyList<CapturedFrame> frames)
    {
        if (_stale) Recreate();
        foreach (var frame in frames) Get(frame.Monitor).Show(frame);
        IsVisible = true;
    }

    private OverlayWindow Get(MonitorInfo monitor)
    {
        if (!_windows.TryGetValue(monitor.Key, out var w))
            _windows[monitor.Key] = w = new OverlayWindow(monitor);
        return w;
    }

    public void HideAll()
    {
        foreach (var w in _windows.Values) w.Hide();
        IsVisible = false;
    }

    private void Recreate()
    {
        foreach (var w in _windows.Values) w.Close();
        _windows.Clear();
        _stale = false;
    }
}
