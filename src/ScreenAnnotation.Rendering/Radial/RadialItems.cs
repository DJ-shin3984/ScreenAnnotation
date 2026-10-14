using System.Windows;
using System.Windows.Media;

namespace ScreenAnnotation.Rendering.Radial;

/// <summary>팔레트 항목. 색은 숫자 키 1~9 순서, 도형은 Shift+Tab 순환 순서.</summary>
public static class RadialItems
{
    public static readonly IReadOnlyList<string> ColorKeys =
        ["#FF3B30", "#FF9500", "#FFCC00", "#34C759", "#007AFF", "#3F51B5", "#AF52DE", "#FFFFFF", "#000000"];

    public static IReadOnlyList<RadialItem> Colors() =>
        ColorKeys.Select(k => new RadialItem(k, Frozen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(k))), null)).ToList();

    public static IReadOnlyList<RadialItem> Shapes()
    {
        var fill = Frozen(new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)));
        return
        [
            new("Rect", fill, Geometry.Parse("M2,4 H22 V20 H2 Z")),
            new("Triangle", fill, Geometry.Parse("M12,3 L22,21 H2 Z")),
            new("Ellipse", fill, new EllipseGeometry(new Point(12, 12), 10, 8)),
            new("Cylinder", fill, Geometry.Parse("M3,6 A9,3 0 1 0 21,6 A9,3 0 1 0 3,6 M3,6 V18 A9,3 0 0 0 21,18 V6")),
            new("Arrow", fill, Geometry.Parse("M3,12 H21 M14,5 L21,12 L14,19")),
            new("Line", fill, Geometry.Parse("M3,21 L21,3")),
        ];
    }

    private static Brush Frozen(Brush b) { b.Freeze(); return b; }
}
