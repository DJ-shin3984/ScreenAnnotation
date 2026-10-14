using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Annotations;

/// <summary>획 점. 확대 중 그린 획은 원본 좌표가 소수가 되므로 float.</summary>
public readonly record struct PointF2(float X, float Y);

public enum StrokeKind { Pen, Highlighter, StraightHighlighter }
public enum ShapeKind { Rect, Triangle, Ellipse, Cylinder, Arrow, Line }
public enum ElementKind { Image, Text }
public enum ElementSource { Uia, Ocr, ImageAnalysis }

/// <summary>확정된 판서 객체. 좌표는 모니터 로컬 물리 픽셀.</summary>
public abstract record Annotation
{
    public required Guid Id { get; init; }
    public required string MonitorKey { get; init; }
    public required long Order { get; init; }
    public required RectPx Bounds { get; init; }
}

public sealed record PenStroke : Annotation
{
    public required StrokeKind Kind { get; init; }
    public required IReadOnlyList<PointF2> Points { get; init; }
    public required string Color { get; init; }
    public required float Width { get; init; }
}

public sealed record ShapeAnnotation : Annotation
{
    public required ShapeKind Kind { get; init; }
    public required PointPx Start { get; init; }
    public required PointPx End { get; init; }
    public required string Color { get; init; }
    public required float Width { get; init; }
    public bool Constrained { get; init; }

    public static RectPx BoundsOf(PointPx a, PointPx b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}

public sealed record TextAnnotation : Annotation
{
    public required string Text { get; init; }
    public required PointPx Position { get; init; }
    public required string Color { get; init; }
    public required float FontSize { get; init; }
}

public sealed record ElementAnnotation : Annotation
{
    public required ElementKind Kind { get; init; }
    public required string CropPath { get; init; }
    public required RectPx SourceRect { get; init; }
    public string? Text { get; init; }
    public required ElementSource Source { get; init; }
}

public sealed record PatchAnnotation : Annotation
{
    public required RectPx Area { get; init; }
    public required string FillColor { get; init; }
}

public static class AnnotationIds
{
    private static long _order;

    /// <summary>전역 생성 순서.</summary>
    public static long Next() => Interlocked.Increment(ref _order);
}
