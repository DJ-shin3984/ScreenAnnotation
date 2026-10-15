using System.IO;
using System.Windows.Threading;
using ScreenAnnotation.App;
using ScreenAnnotation.Core.Input;
using ScreenAnnotation.Core.Settings;
using ScreenAnnotation.Core.Tools;
using ScreenAnnotation.Storage;

namespace ScreenAnnotation.App.Tests;

public class SettingsWindowTests
{
    private sealed class FakeHotkey(bool accept) : IHotkeyService
    {
        public List<HotkeyGesture> Registered { get; } = [];
        public bool Register(HotkeyGesture gesture) { Registered.Add(gesture); return accept; }
        public void Unregister() { }
        public event EventHandler? HotkeyPressed { add { } remove { } }
    }

    private static void Sta(Action body)
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try { body(); }
            catch (Exception e) { error = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        error.Should().BeNull();
    }

    [Fact]
    public void RejectedHotkeyKeepsOldValueAndShowsError() => Sta(() =>
    {
        var store = new SettingsStore(Core.Settings.Settings.Default);
        var repo = new SettingsRepository(Path.Combine(Path.GetTempPath(), $"sa-{Guid.NewGuid():N}.json"));
        var hotkey = new FakeHotkey(accept: false);
        var w = new SettingsWindow(store, repo, hotkey, new ToolState(store.Current));

        w.SetPendingHotkey(new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, "F1"));
        w.ApplyHotkey();

        store.Current.Hotkey.Should().Be("Ctrl+Shift+Space");
        w.ErrorText.Should().Contain("사용 중");
        hotkey.Registered.Should().HaveCount(2, "새 조합 시도 후 옛 조합 재등록");
        hotkey.Registered[^1].Should().Be(HotkeyGesture.Default);
        w.Close();
    });

    [Fact]
    public void AcceptedHotkeyIsStoredAndSaved() => Sta(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), $"sa-{Guid.NewGuid():N}.json");
        var store = new SettingsStore(Core.Settings.Settings.Default);
        var repo = new SettingsRepository(path);
        var w = new SettingsWindow(store, repo, new FakeHotkey(accept: true), new ToolState(store.Current));

        w.SetPendingHotkey(new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, "F1"));
        w.ApplyHotkey();

        store.Current.Hotkey.Should().Be("Ctrl+Alt+F1");
        w.ErrorText.Should().BeEmpty();
        repo.Load().Hotkey.Should().Be("Ctrl+Alt+F1");
        w.Close();
        File.Delete(path);
    });
}
