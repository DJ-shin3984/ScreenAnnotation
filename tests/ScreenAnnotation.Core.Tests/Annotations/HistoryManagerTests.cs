using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Tests.Annotations;

public class HistoryManagerTests
{
    private static PenStroke Stroke() => new()
    {
        Id = Guid.NewGuid(), MonitorKey = "m", Order = AnnotationIds.Next(), Bounds = new RectPx(0, 0, 1, 1),
        Kind = StrokeKind.Pen, Points = [], Color = "#000000", Width = 1,
    };

    [Fact]
    public void CapacityDropsOldest()
    {
        var s = new AnnotationStore();
        var h = new HistoryManager(s, 200);
        var first = Stroke();
        h.Execute(new AddCommand(first));
        for (var i = 0; i < 200; i++) h.Execute(new AddCommand(Stroke()));

        h.UndoCount.Should().Be(200);
        while (h.Undo()) { }
        s.Count.Should().Be(1, "가장 오래된 Add는 스택에서 밀려나 Undo되지 않는다");
        s.Find(first.Id).Should().NotBeNull();
    }

    [Fact]
    public void NewCommandClearsRedo()
    {
        var s = new AnnotationStore();
        var h = new HistoryManager(s);
        h.Execute(new AddCommand(Stroke()));
        h.Undo();
        h.CanRedo.Should().BeTrue();
        h.Execute(new AddCommand(Stroke()));
        h.CanRedo.Should().BeFalse();
        h.Redo().Should().BeFalse();
    }

    [Fact]
    public void UndoRedoOnEmptyReturnFalse()
    {
        var h = new HistoryManager(new AnnotationStore());
        h.Undo().Should().BeFalse();
        h.Redo().Should().BeFalse();
        h.CanUndo.Should().BeFalse();
    }
}
