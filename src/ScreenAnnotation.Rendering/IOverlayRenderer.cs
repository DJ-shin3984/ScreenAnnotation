using System.Windows;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Rendering.Strokes;

namespace ScreenAnnotation.Rendering;

/// <summary>그리는 중인 획(L3). 좌표는 모니터 로컬 물리 픽셀.</summary>
public sealed class LiveStroke
{
    public required StrokeKind Kind { get; init; }
    public required string Color { get; init; }
    public required double Width { get; init; }
    public List<Point> Points { get; } = [];
}

/// <summary>그리는 중인 도형 미리보기(L3).</summary>
public sealed record LiveShape(ShapeKind Kind, Point Start, Point End, bool Constrained, string Color, double Width);

/// <summary>모니터 하나의 판서 렌더러. L2 확정 판서는 캐시 비트맵, L3 그리는 중인 것만 매 프레임.</summary>
public interface IOverlayRenderer
{
    /// <summary>확정 판서가 바뀐 영역(모니터 로컬 px). 해당 영역만 다시 합성한다.</summary>
    void InvalidateAnnotations(RectPx dirtyLocal);

    /// <summary>전체 다시 합성 (보드 시작·Undo 등).</summary>
    void InvalidateAll();

    /// <summary>지우개가 지나간 객체: 확정 전까지 L2에서 숨긴다.</summary>
    HashSet<Guid> HiddenIds { get; }

    void UpdateLiveStroke(LiveStroke? stroke);
    void UpdateLiveShape(LiveShape? shape);

    /// <summary>사라지는 펜·레이저. 렌더러가 프레임 틱으로 Tick/Prune를 돌리며 그린다.</summary>
    EphemeralStrokes Ephemeral { get; }
    LaserTrail Laser { get; }
}
