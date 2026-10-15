using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Windows;

namespace ScreenAnnotation.Core.Recognition;

/// <summary>한 단계 큰 범위 (텍스트: 줄·문단 / 이미지: 감싼 요소). Bounds는 전역 물리 픽셀.</summary>
public sealed record ElementLevel(RectPx Bounds, string? Text);

/// <summary>Ctrl+클릭으로 인식한 화면 요소. Bounds는 전역 물리 픽셀. Ancestors는 작은 것부터.</summary>
public sealed record DetectedElement(ElementKind Kind, RectPx Bounds, string? Text, ElementSource Source, IReadOnlyList<ElementLevel> Ancestors)
{
    /// <summary>자기 자신(0) + 상위 범위들. 같은 자리 Ctrl+클릭 반복이 이 목록을 순환한다.</summary>
    public IReadOnlyList<ElementLevel> Levels => [new(Bounds, Text), .. Ancestors];
}

public interface IElementDetector
{
    /// <summary>1차 UIA(검증·텍스트 범위 확장) → 실패 시 2차 이미지 분석. 없으면 null. UI 스레드를 막지 않는다.</summary>
    Task<DetectedElement?> DetectAsync(int globalX, int globalY, WindowSnapshot snapshot, CapturedFrame? frame, CancellationToken ct);
}

public static class ElementSelection
{
    /// <summary>같은 지점 반복 클릭: 다음 범위로, 끝이면 처음으로.</summary>
    public static int Next(int levels, int currentIndex) => levels <= 0 ? 0 : (currentIndex + 1) % levels;

    /// <summary>두 클릭이 "같은 지점"인가 (4px 이내).</summary>
    public static bool SamePoint(int x1, int y1, int x2, int y2, int tolerance = 4) =>
        Math.Abs(x1 - x2) <= tolerance && Math.Abs(y1 - y2) <= tolerance;
}
