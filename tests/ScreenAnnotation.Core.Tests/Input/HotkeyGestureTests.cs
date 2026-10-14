using ScreenAnnotation.Core.Input;

namespace ScreenAnnotation.Core.Tests.Input;

public class HotkeyGestureTests
{
    [Fact]
    public void ParsesCtrlShiftSpace()
    {
        HotkeyGesture.TryParse("Ctrl+Shift+Space", out var g).Should().BeTrue();
        g.Should().Be(new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Shift, "Space"));
    }

    [Fact]
    public void ParsesCaseInsensitive()
    {
        HotkeyGesture.TryParse("ctrl+alt+F1", out var g).Should().BeTrue();
        g.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        g.Key.Should().Be("F1");
    }

    [Theory]
    [InlineData("Space+Ctrl+")]
    [InlineData("Ctrl+Foo")]
    [InlineData("Ctrl+Shift")]
    [InlineData("A+B")]
    [InlineData("")]
    public void RejectsInvalid(string text) => HotkeyGesture.TryParse(text, out _).Should().BeFalse();

    [Theory]
    [InlineData("Ctrl+Shift+Space")]
    [InlineData("Ctrl+Alt+F1")]
    [InlineData("Win+A")]
    public void ToStringRoundTrips(string text)
    {
        HotkeyGesture.TryParse(text, out var g).Should().BeTrue();
        g.ToString().Should().Be(text);
        HotkeyGesture.TryParse(g.ToString(), out var again).Should().BeTrue();
        again.Should().Be(g);
    }
}
