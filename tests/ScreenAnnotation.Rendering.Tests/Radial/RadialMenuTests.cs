using System.Windows;
using ScreenAnnotation.Rendering.Radial;

namespace ScreenAnnotation.Rendering.Tests.Radial;

public class RadialMenuTests
{
    private static void Sta(Action body)
    {
        Exception? error = null;
        var t = new Thread(() => { try { body(); } catch (Exception e) { error = e; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        error.Should().BeNull();
    }

    [Fact]
    public void Open_near_corner_keeps_all_slots_inside_host() => Sta(() =>
    {
        var menu = new RadialMenu();
        menu.Open(new Point(10, 10), RadialItems.Colors(), new Size(1000, 1000));

        menu.IsOpen.Should().BeTrue();
        foreach (UIElement child in menu.Children)
        {
            var left = System.Windows.Controls.Canvas.GetLeft(child);
            var top = System.Windows.Controls.Canvas.GetTop(child);
            left.Should().BeGreaterThanOrEqualTo(0);
            top.Should().BeGreaterThanOrEqualTo(0);
            (left + RadialMenu.SlotSize).Should().BeLessThanOrEqualTo(1000);
            (top + RadialMenu.SlotSize).Should().BeLessThanOrEqualTo(1000);
        }
    });

    [Fact]
    public void Drag_right_and_release_selects_highlighted_slot() => Sta(() =>
    {
        var menu = new RadialMenu();
        string? selected = null;
        menu.Selected += k => selected = k;
        var items = RadialItems.Colors();
        menu.Open(new Point(500, 500), items, new Size(1000, 1000));

        menu.OnMouseMove(new Point(600, 500));
        var idx = menu.HighlightedIndex;
        idx.Should().NotBeNull();
        menu.OnRightButtonUp(new Point(600, 500));

        selected.Should().Be(items[idx!.Value].Key);
        menu.IsOpen.Should().BeFalse();
    });

    [Fact]
    public void Short_right_click_stays_open_then_left_click_selects() => Sta(() =>
    {
        var menu = new RadialMenu();
        string? selected = null;
        menu.Selected += k => selected = k;
        menu.Open(new Point(500, 500), RadialItems.Shapes(), new Size(1000, 1000));

        menu.OnRightButtonUp(new Point(500, 500));
        menu.IsOpen.Should().BeTrue();

        // 슬롯 0은 중심 바로 위(반지름 70)
        menu.OnLeftButtonDown(new Point(500, 500 - RadialMenu.Radius));
        selected.Should().Be("Rect");
        menu.IsOpen.Should().BeFalse();
    });

    [Fact]
    public void Left_click_outside_ring_in_click_mode_cancels() => Sta(() =>
    {
        var menu = new RadialMenu();
        var cancelled = false;
        menu.Cancelled += () => cancelled = true;
        menu.Open(new Point(500, 500), RadialItems.Colors(), new Size(1000, 1000));
        menu.OnRightButtonUp(new Point(500, 500));
        menu.OnLeftButtonDown(new Point(900, 900));

        cancelled.Should().BeTrue();
        menu.IsOpen.Should().BeFalse();
    });
}
