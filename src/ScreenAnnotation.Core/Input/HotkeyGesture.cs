namespace ScreenAnnotation.Core.Input;

/// <summary>값은 Win32 MOD_* 와 같다.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>"Ctrl+Shift+Space" 같은 전역 단축키 조합.</summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, string Key)
{
    public static readonly HotkeyGesture Default = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, "Space");

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var mods = HotkeyModifiers.None;
        string? key = null;
        foreach (var raw in text.Split('+'))
        {
            var token = raw.Trim();
            if (token.Length == 0) return false;
            switch (token.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": mods |= HotkeyModifiers.Control; break;
                case "SHIFT": mods |= HotkeyModifiers.Shift; break;
                case "ALT": mods |= HotkeyModifiers.Alt; break;
                case "WIN": mods |= HotkeyModifiers.Win; break;
                default:
                    if (key is not null || !IsKnownKey(token)) return false;
                    key = Normalize(token);
                    break;
            }
        }

        if (key is null) return false;
        gesture = new HotkeyGesture(mods, key);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Key);
        return string.Join('+', parts);
    }

    private static bool IsKnownKey(string token)
    {
        var u = token.ToUpperInvariant();
        if (u == "SPACE") return true;
        if (u.Length == 1 && (char.IsAsciiLetterUpper(u[0]) || char.IsAsciiDigit(u[0]))) return true;
        return u.Length is 2 or 3 && u[0] == 'F' && int.TryParse(u[1..], out var n) && n is >= 1 and <= 12;
    }

    private static string Normalize(string token)
    {
        var u = token.ToUpperInvariant();
        return u == "SPACE" ? "Space" : u;
    }
}

public interface IHotkeyService
{
    /// <summary>등록 실패 시 false (다른 앱이 이미 같은 조합을 쓰는 경우 등).</summary>
    bool Register(HotkeyGesture gesture);
    void Unregister();
    event EventHandler? HotkeyPressed;
}
