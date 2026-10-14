using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Media;

namespace ScreenAnnotation.Rendering;

/// <summary>"#RRGGBB" / "#AARRGGBB" 문자열 ↔ WPF 색. 브러시는 Frozen으로 캐시한다.</summary>
public static class ColorHex
{
    private static readonly ConcurrentDictionary<string, SolidColorBrush> Brushes = new(StringComparer.OrdinalIgnoreCase);

    public static Color Parse(string hex)
    {
        var s = hex.AsSpan().TrimStart('#');
        var v = uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (s.Length == 6) v |= 0xFF000000;
        return Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public static SolidColorBrush Brush(string hex) => Brushes.GetOrAdd(hex, h =>
    {
        var b = new SolidColorBrush(Parse(h));
        b.Freeze();
        return b;
    });
}
