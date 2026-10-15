using ScreenAnnotation.Core.Tools;
using ScreenAnnotation.Rendering.Toolbar;

namespace ScreenAnnotation.Rendering.Tests.Toolbar;

public class ToolbarControlTests
{
    private static void Sta(Action body)
    {
        Exception? error = null;
        var t = new Thread(() => { try { body(); } catch (Exception e) { error = e; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error is not null) throw error;
    }

    [Fact]
    public void ClickRaisesActionAndApplySetsVariant() => Sta(() =>
    {
        var bar = new ToolbarControl();
        ToolbarAction? got = null;
        bar.Action += a => got = a;
        bar.Click(ToolbarAction.EphemeralPen);
        got.Should().Be(ToolbarAction.EphemeralPen);

        var tools = new ToolState(Core.Settings.Settings.Default);
        ToolbarControl.Apply(tools, ToolbarAction.EphemeralPen);
        tools.Tool.Should().Be(Tool.Pen);
        tools.PenVariant.Should().Be(PenVariant.Ephemeral);

        bar.Sync(tools);
        bar.SelectedAction.Should().Be(ToolbarAction.EphemeralPen);
        bar.SelectedColor.Should().Be(1, "펜 기본 빨강");
    });

    [Fact]
    public void ColorDotRaisesNumber() => Sta(() =>
    {
        var bar = new ToolbarControl();
        var got = 0;
        bar.ColorPicked += n => got = n;
        bar.ClickColor(3);
        got.Should().Be(3);
        bar.PreferredHeight.Should().BeGreaterThan(300);
    });
}
