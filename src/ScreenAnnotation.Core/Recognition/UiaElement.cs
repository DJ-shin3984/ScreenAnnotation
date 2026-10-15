using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Windows;

namespace ScreenAnnotation.Core.Recognition;

/// <summary>UIA가 알려 준 화면 요소. Bounds는 전역 물리 픽셀.</summary>
public sealed record UiaElement(string ControlType, string Name, RectPx Bounds, string Method, double ElapsedMs);

public interface IUiaElementLocator
{
    /// <summary>스냅샷에서 대상 창을 찾아 UIA 트리를 내려가며 점을 포함하는 가장 깊은 요소를 찾는다. 150ms 초과 시 null.</summary>
    Task<UiaElement?> FindAsync(int x, int y, WindowSnapshot snapshot, CancellationToken ct);

    /// <summary>대안: ElementFromPoint. 호출자가 질의 순간 오버레이를 투과 상태로 만들어야 한다.</summary>
    Task<UiaElement?> FindFromPointAsync(int x, int y, CancellationToken ct);

    /// <summary>FindAsync + 검증 + 텍스트 범위(단어→줄→문단) 또는 상위 요소 목록. 150ms 초과·검증 실패 시 null.</summary>
    Task<DetectedElement?> FindDetailedAsync(int x, int y, WindowSnapshot snapshot, CancellationToken ct);
}
