using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Recognition;

namespace ScreenAnnotation.Core.Tests.Recognition;

public class ShapeRecognizerTests
{
    private static readonly Random Rng = new(7);

    private static PointF2 Noisy(float x, float y) => new(x + (float)(Rng.NextDouble() * 4 - 2), y + (float)(Rng.NextDouble() * 4 - 2));

    private static PenStroke Stroke(IEnumerable<PointF2> pts) => new()
    {
        Id = Guid.NewGuid(), MonitorKey = "m", Order = AnnotationIds.Next(), Bounds = new RectPx(0, 0, 1, 1),
        Kind = StrokeKind.Pen, Points = pts.ToList(), Color = "#FF3B30", Width = 3,
    };

    private static IEnumerable<PointF2> Segment(float x0, float y0, float x1, float y1, int n)
    {
        for (var i = 0; i < n; i++) { var t = i / (float)(n - 1); yield return Noisy(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t); }
    }

    [Fact]
    public void WobblyLineIsLine()
    {
        var s = ShapeRecognizer.Recognize(Stroke(Segment(10, 10, 300, 120, 30)));
        s!.Kind.Should().Be(ShapeKind.Line);
        s.Start.X.Should().BeCloseTo(10, 3);
        s.End.X.Should().BeCloseTo(300, 3);
    }

    [Fact]
    public void WobblyRectangleIsRect()
    {
        var pts = Segment(100, 100, 300, 100, 10).Concat(Segment(300, 100, 300, 250, 10))
            .Concat(Segment(300, 250, 100, 250, 10)).Concat(Segment(100, 250, 100, 112, 10)); // 살짝 열림
        var s = ShapeRecognizer.Recognize(Stroke(pts));
        s!.Kind.Should().Be(ShapeKind.Rect);
        s.Bounds.X.Should().BeCloseTo(100, 5);
        s.Bounds.Y.Should().BeCloseTo(100, 5);
        s.Bounds.Width.Should().BeCloseTo(200, 5);
        s.Bounds.Height.Should().BeCloseTo(150, 5);
    }

    [Fact]
    public void NoisyCircleIsEllipse()
    {
        var pts = Enumerable.Range(0, 37).Select(i => { var a = i * 10 * MathF.PI / 180; return Noisy(200 + 80 * MathF.Cos(a), 200 + 80 * MathF.Sin(a)); });
        var s = ShapeRecognizer.Recognize(Stroke(pts));
        s!.Kind.Should().Be(ShapeKind.Ellipse);
        s.Bounds.Width.Should().BeCloseTo(160, 6);
    }

    [Fact]
    public void ZigzagAndVAreRejected()
    {
        var zig = Segment(0, 0, 50, 100, 8).Concat(Segment(50, 100, 100, 0, 8)).Concat(Segment(100, 0, 150, 100, 8)).Concat(Segment(150, 100, 200, 0, 8));
        ShapeRecognizer.Recognize(Stroke(zig)).Should().BeNull();
        var v = Segment(0, 0, 100, 150, 15).Concat(Segment(100, 150, 200, 0, 15));
        ShapeRecognizer.Recognize(Stroke(v)).Should().BeNull();
    }

    [Fact]
    public void ReplaceThenUndoRestoresStroke()
    {
        var store = new AnnotationStore();
        var history = new HistoryManager(store);
        var stroke = Stroke(Segment(10, 10, 300, 120, 30));
        history.Execute(new AddCommand(stroke));
        var shape = ShapeRecognizer.Recognize(stroke)!;

        history.Execute(new ReplaceCommand(stroke.Id, shape));
        store.ForMonitor("m").Should().ContainSingle().Which.Should().BeOfType<ShapeAnnotation>();

        history.Undo().Should().BeTrue();
        store.ForMonitor("m").Should().ContainSingle().Which.Should().Be(stroke);
    }
}
