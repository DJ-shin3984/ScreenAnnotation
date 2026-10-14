using ScreenAnnotation.Core.Settings;

namespace ScreenAnnotation.Storage.Tests;

public sealed class SettingsRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sa-tests-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void MissingFile_ReturnsDefault()
    {
        new SettingsRepository(SettingsPath).Load().Should().BeEquivalentTo(Settings.Default);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var repo = new SettingsRepository(SettingsPath);
        var settings = new Settings
        {
            Hotkey = "Ctrl+Alt+F9",
            EphemeralSeconds = 4,
            SavePath = @"D:\shots",
            ToolDefaults = new() { ["Pen"] = new("#000000", 8), ["Highlighter"] = new("#34C759", 24) },
        };

        repo.Save(settings);

        repo.Load().Should().BeEquivalentTo(settings);
        File.Exists(SettingsPath + ".tmp").Should().BeFalse();
    }

    [Fact]
    public void CorruptFile_ReturnsDefaultAndBacksUp()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ not json");

        new SettingsRepository(SettingsPath).Load().Should().BeEquivalentTo(Settings.Default);

        Directory.GetFiles(_dir, "settings.json.corrupt-*").Should().HaveCount(1);
        File.Exists(SettingsPath).Should().BeFalse();
    }
}
