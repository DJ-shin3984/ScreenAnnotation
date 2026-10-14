using System.Windows;
using ScreenAnnotation.Rendering;
using ScreenAnnotation.Rendering.Hud;

namespace ScreenAnnotation.Rendering.Tests.Hud;

public class HudTests
{
    [Fact]
    public void ColorHex_parses_rgb()
    {
        var c = ColorHex.Parse("#FF3B30");
        (c.A, c.R, c.G, c.B).Should().Be(((byte)255, (byte)255, (byte)59, (byte)48));
        ColorHex.Brush("#FF3B30").Should().BeSameAs(ColorHex.Brush("#FF3B30"), "브러시는 캐시된다");
    }

    [Fact]
    public void Hud_update_sets_text_and_preview_shows() => Sta(() =>
    {
        var hud = new HudControl();
        hud.Update("펜", "일반", 3, "#FF3B30");
        hud.Text.Should().Be("펜 · 일반 · 3px");

        var preview = new WidthPreview();
        preview.Visibility.Should().Be(Visibility.Collapsed);
        preview.Show(new Point(100, 100), 12, "#FFCC00");
        preview.Visibility.Should().Be(Visibility.Visible);
    });

    private static void Sta(Action body)
    {
        Exception? error = null;
        var t = new Thread(() => { try { body(); } catch (Exception e) { error = e; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        error.Should().BeNull();
    }
}
