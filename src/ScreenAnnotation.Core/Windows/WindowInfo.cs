using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Windows;

public sealed record WindowInfo(nint Handle, string Title, RectPx Bounds, uint ProcessId, bool IsElevated);

/// <summary>켜지는 순간의 최상위 창 목록. Z-order 순(맨 위가 0번).</summary>
public sealed record WindowSnapshot(IReadOnlyList<WindowInfo> Windows, DateTimeOffset TakenAt)
{
    public WindowInfo? HitTest(int x, int y) => Windows.FirstOrDefault(w => w.Bounds.Contains(x, y));
}

public interface IWindowSnapshotService
{
    WindowSnapshot Take();
}
