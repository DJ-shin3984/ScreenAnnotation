using ScreenAnnotation.Core.Tools;

namespace ScreenAnnotation.Core.Tests.Tools;

public class ToolStateSetterTests
{
    [Fact]
    public void SetToolEraserRemembersPreviousForToggle()
    {
        var t = new ToolState(Core.Settings.Settings.Default);
        t.SetHighlighterVariant(HighlighterVariant.Straight);
        t.SetTool(Tool.Eraser);
        t.SetTool(Tool.Eraser); // 중복 호출로 기억이 덮이지 않음
        t.ToggleEraser();
        t.Tool.Should().Be(Tool.Highlighter);
        t.HighlighterVariant.Should().Be(HighlighterVariant.Straight);
    }

    [Fact]
    public void SetPenVariantSwitchesToPenAndRaisesChanged()
    {
        var t = new ToolState(Core.Settings.Settings.Default);
        var changed = 0;
        t.Changed += () => changed++;
        t.SetTool(Tool.Shape);
        t.SetPenVariant(PenVariant.Laser);
        t.Tool.Should().Be(Tool.Pen);
        t.PenVariant.Should().Be(PenVariant.Laser);
        changed.Should().Be(2);
    }
}
