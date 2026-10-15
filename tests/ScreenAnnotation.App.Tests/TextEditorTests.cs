using System.Windows;
using Point = System.Windows.Point;
using System.Windows.Threading;
using ScreenAnnotation.App;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Rendering;

namespace ScreenAnnotation.App.Tests;

public class TextEditorTests
{
    private static void Sta(Action<OverlayWindow, AnnotationStore, HistoryManager, TextEditor> body)
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            OverlayWindow? w = null;
            try
            {
                var monitor = new MonitorInfo("test", @"\\.\DISPLAY1", new RectPx(0, 0, 800, 600), new RectPx(0, 0, 800, 560), 96, 96, true);
                w = new OverlayWindow(monitor);
                var store = new AnnotationStore();
                var history = new HistoryManager(store);
                var editor = new TextEditor(w, store, history, _ => { });
                body(w, store, history, editor);
            }
            catch (Exception e) { error = e; }
            finally
            {
                w?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        error.Should().BeNull();
    }

    [Fact]
    public void CommitStoresKoreanText() => Sta((w, store, _h, editor) =>
    {
        editor.Begin(new Point(100, 50), "#FF3B30", 24);
        editor.IsEditing.Should().BeTrue();
        editor.Box.Text = "한글";
        editor.Commit();

        editor.IsEditing.Should().BeFalse();
        var t = store.ForMonitor("test").Should().ContainSingle().Which.Should().BeOfType<TextAnnotation>().Subject;
        t.Text.Should().Be("한글");
        t.Position.Should().Be(new PointPx(100, 50));
        t.Color.Should().Be("#FF3B30");
        t.Bounds.Width.Should().BeGreaterThan(0);
    });

    [Fact]
    public void EmptyCancelStoresNothing() => Sta((_w, store, _h, editor) =>
    {
        var closed = 0;
        editor.Closed += () => closed++;
        editor.Begin(new Point(0, 0), "#000000", 12);
        editor.Cancel();
        store.Count.Should().Be(0);
        closed.Should().Be(1);
        editor.IsEditing.Should().BeFalse();
    });

    [Fact]
    public void ReEditReplacesAndUndoRestores() => Sta((_w, store, history, editor) =>
    {
        editor.Begin(new Point(10, 10), "#007AFF", 20);
        editor.Box.Text = "처음";
        editor.Commit();

        editor.TryBeginEdit(new Point(12, 12), out var existing).Should().BeTrue();
        existing!.Text.Should().Be("처음");
        editor.Box.Text.Should().Be("처음");
        editor.Box.Text = "수정됨";
        editor.Commit();

        var after = store.ForMonitor("test").Should().ContainSingle().Which.As<TextAnnotation>();
        after.Text.Should().Be("수정됨");
        after.Position.Should().Be(new PointPx(10, 10));

        history.Undo().Should().BeTrue();
        store.ForMonitor("test").Should().ContainSingle().Which.As<TextAnnotation>().Text.Should().Be("처음");

        editor.TryBeginEdit(new Point(700, 500), out _).Should().BeFalse();
    });

    [Fact]
    public void FontSizeScalesWithWidth()
    {
        TextEditor.FontSizeFor(1).Should().Be(14);
        TextEditor.FontSizeFor(40).Should().Be(92);
        TextEditor.FontSizeFor(100).Should().Be(96);
    }
}
