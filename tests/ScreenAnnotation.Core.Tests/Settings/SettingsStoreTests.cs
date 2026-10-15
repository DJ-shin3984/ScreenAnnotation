using ScreenAnnotation.Core.Settings;
using ScreenAnnotation.Core.Tools;

namespace ScreenAnnotation.Core.Tests.Settings;

public class SettingsStoreTests
{
    [Fact]
    public void UpdateReplacesCurrentAndRaisesChanged()
    {
        var store = new SettingsStore(Core.Settings.Settings.Default);
        var raised = 0;
        store.Changed += () => raised++;

        store.Update(s => s with { EphemeralSeconds = 5 });

        store.Current.EphemeralSeconds.Should().Be(5);
        raised.Should().Be(1);
    }

    [Fact]
    public void ApplyDefaultsChangesToolColorImmediately()
    {
        var tools = new ToolState(Core.Settings.Settings.Default);
        var changed = 0;
        tools.Changed += () => changed++;

        var s = Core.Settings.Settings.Default with
        {
            ToolDefaults = new() { ["Pen"] = new ToolDefault("#007AFF", 8), ["Highlighter"] = new("#FFCC00", 16), ["Shape"] = new("#FF3B30", 3) },
        };
        tools.ApplyDefaults(s);

        tools.Color.Should().Be("#007AFF");
        tools.Width.Should().Be(8);
        changed.Should().Be(1);
    }
}
