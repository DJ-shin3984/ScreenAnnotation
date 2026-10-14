using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Tools;

namespace ScreenAnnotation.Core.Tests.Tools;

public class ToolStateTests
{
    private static ToolState New() => new(Core.Settings.Settings.Default);

    [Fact]
    public void TabCyclesPenHighlighterShape()
    {
        var s = New();
        s.Tool.Should().Be(Tool.Pen);
        s.NextTool(); s.Tool.Should().Be(Tool.Highlighter);
        s.NextTool(); s.Tool.Should().Be(Tool.Shape);
        s.NextTool(); s.Tool.Should().Be(Tool.Pen);
    }

    [Fact]
    public void HighlighterStraightVariantSurvivesThreeTabs()
    {
        var s = New();
        s.NextTool();            // Highlighter
        s.NextVariant();         // Straight
        s.HighlighterVariant.Should().Be(HighlighterVariant.Straight);
        s.NextTool(); s.NextTool(); s.NextTool(); // Shape → Pen → Highlighter
        s.Tool.Should().Be(Tool.Highlighter);
        s.HighlighterVariant.Should().Be(HighlighterVariant.Straight);
    }

    [Fact]
    public void VariantCyclesPerTool()
    {
        var s = New();
        s.NextVariant(); s.PenVariant.Should().Be(PenVariant.Ephemeral);
        s.NextVariant(); s.PenVariant.Should().Be(PenVariant.Laser);
        s.NextVariant(); s.PenVariant.Should().Be(PenVariant.Normal);
        s.NextTool(); s.NextTool(); // Shape
        s.NextVariant(); s.ShapeKind.Should().Be(ShapeKind.Triangle);
        s.SetShape(ShapeKind.Line);
        s.NextVariant(); s.ShapeKind.Should().Be(ShapeKind.Rect);
    }

    [Fact]
    public void EscResetsToNormalPenButKeepsColors()
    {
        var s = New();
        s.NextVariant();         // Pen Ephemeral
        s.NextTool();            // Highlighter
        s.SetColor(5);           // blue highlighter
        s.Reset();
        s.Tool.Should().Be(Tool.Pen);
        s.PenVariant.Should().Be(PenVariant.Normal);
        s.Color.Should().Be("#FF3B30");
        s.NextTool();
        s.Color.Should().Be("#007AFF", "Esc는 색을 건드리지 않는다");
    }

    [Fact]
    public void EraserTogglesBackToPreviousTool()
    {
        var s = New();
        s.NextTool();            // Highlighter
        s.ToggleEraser(); s.Tool.Should().Be(Tool.Eraser);
        s.SetColor(1);           // ignored
        s.ToggleEraser(); s.Tool.Should().Be(Tool.Highlighter);
        s.Color.Should().Be("#FFCC00");

        s.ToggleEraser();
        s.Reset(); s.Tool.Should().Be(Tool.Pen);
        s.ToggleEraser(); s.Tool.Should().Be(Tool.Eraser);
        s.ToggleEraser(); s.Tool.Should().Be(Tool.Pen);
    }

    [Fact]
    public void WheelAccumulatesAndClamps()
    {
        var s = New();
        s.Width.Should().Be(3);
        s.Wheel(40).Should().BeFalse();
        s.Wheel(40).Should().BeFalse();
        s.Wheel(40).Should().BeTrue();
        s.Width.Should().Be(4);
        s.Wheel(-120).Should().BeTrue();
        s.Width.Should().Be(3);
        s.Wheel(-120 * 10).Should().BeTrue();
        s.Width.Should().Be(1);
        s.Wheel(-120).Should().BeFalse();
        s.Wheel(120 * 20); s.Width.Should().Be(40);
    }

    [Fact]
    public void ColorAndWidthAreRememberedPerTool()
    {
        var s = New();
        s.Color.Should().Be("#FF3B30");
        s.SetColor(4);
        s.NextTool();
        s.Color.Should().Be("#FFCC00");
        s.Width.Should().Be(16);
        s.Wheel(120); s.Width.Should().Be(24);
        s.NextTool(); s.NextTool();
        s.Color.Should().Be("#34C759");
        s.Width.Should().Be(3);
    }

    [Fact]
    public void WidthInitializesToNearestStep()
    {
        WidthSteps.NearestIndex(WidthSteps.Highlighter, 16).Should().Be(2);
        WidthSteps.NearestIndex(WidthSteps.Pen, 5).Should().Be(3);
        WidthSteps.NearestIndex(WidthSteps.Shape, 100).Should().Be(7);
        Palette.ByNumber(9).Should().Be("#000000");
    }
}
