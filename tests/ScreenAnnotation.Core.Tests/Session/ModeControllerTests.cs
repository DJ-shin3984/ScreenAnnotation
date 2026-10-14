using ScreenAnnotation.Core.Session;

namespace ScreenAnnotation.Core.Tests.Session;

public class ModeControllerTests
{
    [Fact]
    public void HotkeyStartsCaptureThenActivateThenStops()
    {
        var mc = new ModeController();
        var seen = new List<Mode>();
        mc.ModeChanged += seen.Add;

        mc.OnHotkey().Should().Be(HotkeyAction.Start);
        mc.Mode.Should().Be(Mode.Capturing);
        mc.OnHotkey().Should().Be(HotkeyAction.Ignore, "캡처 중 핫키는 무시");
        mc.Activate().Should().BeTrue();
        mc.Mode.Should().Be(Mode.Active);
        mc.OnHotkey().Should().Be(HotkeyAction.Stop);
        mc.Mode.Should().Be(Mode.Off);

        seen.Should().Equal(Mode.Capturing, Mode.Active, Mode.Off);
    }

    [Fact]
    public void ActivateOutsideCapturingIsRejected()
    {
        var mc = new ModeController();
        mc.Activate().Should().BeFalse();
        mc.Mode.Should().Be(Mode.Off);
    }

    [Fact]
    public void DeactivateIsIdempotent()
    {
        var mc = new ModeController();
        var changes = 0;
        mc.ModeChanged += _ => changes++;

        mc.Deactivate();
        mc.OnHotkey();
        mc.Deactivate();
        mc.Deactivate();

        mc.Mode.Should().Be(Mode.Off);
        changes.Should().Be(2, "Capturing 진입 1회 + Off 복귀 1회");
    }
}
