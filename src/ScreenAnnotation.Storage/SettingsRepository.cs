using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenAnnotation.Core.Settings;

namespace ScreenAnnotation.Storage;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

public sealed class SettingsRepository
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenAnnotation", "settings.json");

    private readonly string _path;

    public SettingsRepository(string? path = null) => _path = path ?? DefaultPath;

    /// <summary>파일 없음 → 기본값. 손상 → .corrupt-타임스탬프로 백업 후 기본값.</summary>
    public Settings Load()
    {
        if (!File.Exists(_path)) return Settings.Default;
        try
        {
            using var stream = File.OpenRead(_path);
            return JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.Settings) ?? Settings.Default;
        }
        catch (JsonException)
        {
            File.Move(_path, $"{_path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            return Settings.Default;
        }
    }

    /// <summary>.tmp에 쓴 뒤 교체해 쓰다 만 파일이 남지 않게 한다.</summary>
    public void Save(Settings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        using (var stream = File.Create(tmp))
        {
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.Settings);
        }
        File.Move(tmp, _path, overwrite: true);
    }
}
