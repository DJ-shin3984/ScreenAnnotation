using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ScreenAnnotation.Rendering.Hud;

/// <summary>우측 하단 HUD: 색 점 + "도구 · 변형 · 굵기px". 호출자가 루트 캔버스 우측 하단에 놓는다.</summary>
public sealed class HudControl : UserControl
{
    public static readonly double EdgeMargin = 24; // 루트 캔버스 우측 하단에서 띄울 거리

    private readonly Ellipse _dot = new() { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
    private readonly TextBlock _text = new() { Foreground = System.Windows.Media.Brushes.White, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _flash = new() { Foreground = System.Windows.Media.Brushes.White, FontSize = 14, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 0) };
    private readonly DispatcherTimer _flashTimer = new();

    public HudControl()
    {
        IsHitTestVisible = false;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_dot);
        row.Children.Add(_text);
        var column = new StackPanel();
        column.Children.Add(row);
        column.Children.Add(_flash);
        Content = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = ColorHex.Brush("#CC202020"),
            Padding = new Thickness(10, 6, 10, 6),
            Child = column,
        };
        _flashTimer.Tick += (_, _) => { _flashTimer.Stop(); _flash.Visibility = Visibility.Collapsed; };
    }

    /// <summary>현재 표시 문구 (테스트용).</summary>
    public string Text => _text.Text;

    public void Update(string tool, string variant, int widthPx, string colorHex)
    {
        _dot.Fill = ColorHex.Brush(colorHex);
        _text.Text = $"{tool} · {variant} · {widthPx}px";
    }

    public void Flash(string message, TimeSpan duration)
    {
        _flash.Text = message;
        _flash.Visibility = Visibility.Visible;
        _flashTimer.Stop();
        _flashTimer.Interval = duration;
        _flashTimer.Start();
    }
}
