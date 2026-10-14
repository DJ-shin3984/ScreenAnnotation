using System.Globalization;

namespace ScreenAnnotation.Core.Watchdog;

/// <summary>
/// 앱 → 워치독 하트비트 한 줄. 형식: "1 0x1A2B 0x3C4D" (Active + 오버레이 창 핸들들) 또는 "0" (비활성).
/// </summary>
public readonly record struct Heartbeat(bool Active, IReadOnlyList<long> OverlayHandles)
{
    public static readonly Heartbeat Inactive = new(false, Array.Empty<long>());

    public string Format()
    {
        if (!Active) return "0";
        return "1" + string.Concat(OverlayHandles.Select(h => " 0x" + h.ToString("X", CultureInfo.InvariantCulture)));
    }

    public static bool TryParse(string? line, out Heartbeat heartbeat)
    {
        heartbeat = Inactive;
        var parts = (line ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts[0] is not ("0" or "1")) return false;

        var handles = new List<long>(parts.Length - 1);
        foreach (var p in parts.Skip(1))
        {
            if (!p.StartsWith("0x", StringComparison.Ordinal) ||
                !long.TryParse(p.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h))
                return false;
            handles.Add(h);
        }

        heartbeat = new Heartbeat(parts[0] == "1", handles);
        return true;
    }
}

public interface IHeartbeatSource
{
    Heartbeat Current { get; }
}

/// <summary>T15의 ModeController가 붙기 전까지 App이 직접 값을 넣는 자리.</summary>
public sealed class StaticHeartbeatSource : IHeartbeatSource
{
    public Heartbeat Current { get; set; } = Heartbeat.Inactive;
}
