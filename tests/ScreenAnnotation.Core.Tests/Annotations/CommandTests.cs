using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Tests.Annotations;

public class CommandTests
{
    private const string M = "mon";

    private static PenStroke Stroke(int x = 0) => new()
    {
        Id = Guid.NewGuid(), MonitorKey = M, Order = AnnotationIds.Next(), Bounds = new RectPx(x, 0, 10, 10),
        Kind = StrokeKind.Pen, Points = [new PointF2(x, 0), new PointF2(x + 10, 10)], Color = "#FF3B30", Width = 3,
    };

    private static (AnnotationStore, HistoryManager) New() { var s = new AnnotationStore(); return (s, new HistoryManager(s)); }

    [Fact]
    public void AddRoundTrips()
    {
        var (s, h) = New();
        var a = Stroke();
        h.Execute(new AddCommand(a));
        s.ForMonitor(M).Should().ContainSingle().Which.Should().Be(a);
        h.Undo().Should().BeTrue();
        s.Count.Should().Be(0);
        h.Redo().Should().BeTrue();
        s.Find(a.Id).Should().Be(a);
    }

    [Fact]
    public void RemoveRoundTrips()
    {
        var (s, h) = New();
        var a = Stroke(); var b = Stroke(20);
        h.Execute(new AddCommand(a)); h.Execute(new AddCommand(b));
        h.Execute(new RemoveCommand(a.Id));
        s.ForMonitor(M).Should().Equal(b);
        h.Undo();
        s.ForMonitor(M).Should().Equal(a, b);
        h.Redo();
        s.ForMonitor(M).Should().Equal(b);
    }

    [Fact]
    public void ReplaceKeepsOrderAndRoundTrips()
    {
        var (s, h) = New();
        var a = Stroke(); var b = Stroke(20);
        h.Execute(new AddCommand(a)); h.Execute(new AddCommand(b));
        var a2 = a with { Id = Guid.NewGuid(), Order = 0, Width = 8 };
        h.Execute(new ReplaceCommand(a.Id, a2));
        s.ForMonitor(M).Select(x => x.Id).Should().Equal(a2.Id, b.Id);
        s.Find(a2.Id)!.Order.Should().Be(a.Order);
        h.Undo();
        s.ForMonitor(M).Should().Equal(a, b);
        h.Redo();
        s.Find(a2.Id).Should().NotBeNull(); s.Find(a.Id).Should().BeNull();
    }

    [Fact]
    public void LiftElementAddsBothAndUndoRemovesBoth()
    {
        var (s, h) = New();
        var patch = new PatchAnnotation { Id = Guid.NewGuid(), MonitorKey = M, Order = AnnotationIds.Next(), Bounds = new RectPx(0, 0, 5, 5), Area = new RectPx(0, 0, 5, 5), FillColor = "#FFFFFF" };
        var el = new ElementAnnotation { Id = Guid.NewGuid(), MonitorKey = M, Order = AnnotationIds.Next(), Bounds = new RectPx(10, 10, 5, 5), Kind = ElementKind.Image, CropPath = "c.png", SourceRect = new RectPx(0, 0, 5, 5), Source = ElementSource.Uia };
        h.Execute(new LiftElementCommand(el, patch));
        s.ForMonitor(M).Should().Equal(patch, el);
        h.Undo();
        s.Count.Should().Be(0);
        h.Redo();
        s.Count.Should().Be(2);
    }

    [Fact]
    public void CompositeUndoesInReverse()
    {
        var (s, h) = New();
        var a = Stroke(); var b = Stroke(20);
        h.Execute(new AddCommand(a)); h.Execute(new AddCommand(b));
        var order = new List<Guid>();
        s.Changed += (_, r) => order.Add(r.X == 0 ? a.Id : b.Id);
        h.Execute(new CompositeCommand([new RemoveCommand(a.Id), new RemoveCommand(b.Id)]));
        s.Count.Should().Be(0);
        order.Clear();
        h.Undo();
        order.Should().Equal(b.Id, a.Id);
        s.ForMonitor(M).Should().Equal(a, b);
    }

    [Fact]
    public void ChangedCarriesDirtyRect()
    {
        var s = new AnnotationStore();
        var seen = new List<(string, RectPx)>();
        s.Changed += (k, r) => seen.Add((k, r));
        new AddCommand(Stroke(7)).Execute(s);
        seen.Should().Equal((M, new RectPx(7, 0, 10, 10)));
    }
}
