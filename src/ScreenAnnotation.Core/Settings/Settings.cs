namespace ScreenAnnotation.Core.Settings;

public sealed record ToolDefault(string Color, int Width);

public sealed record Settings
{
    public string Hotkey { get; init; } = "Ctrl+Shift+Space";

    public Dictionary<string, ToolDefault> ToolDefaults { get; init; } = new()
    {
        ["Pen"] = new("#FF3B30", 3),
        ["Highlighter"] = new("#FFCC00", 16),
        ["Shape"] = new("#FF3B30", 3),
    };

    /// <summary>사라지는 펜 유지 시간(초). 1~10.</summary>
    public double EphemeralSeconds { get; init; } = 2.5;

    public string SavePath { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ScreenAnnotation");

    public bool RunAtStartup { get; init; }

    public static Settings Default => new();
}
