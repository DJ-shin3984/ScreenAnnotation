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

    public void ShowAll(IReadOnlyList<CapturedFrame> frames)
    {
        if (_stale) Recreate();
        foreach (var frame in frames)
        {
            if (!_windows.TryGetValue(frame.Monitor.Key, out var w))
                _windows[frame.Monitor.Key] = w = new OverlayWindow(frame.Monitor);
            w.Show(frame);
        }
        IsVisible = true;
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
