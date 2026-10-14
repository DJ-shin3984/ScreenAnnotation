using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScreenAnnotation.Rendering.Hud;

/// <summary>휠로 굵기를 바꿀 때 커서 위치에 0.8초간 보이는 굵기 원. 부모 좌표계(물리 픽셀)에 그린다.</summary>
public sealed class WidthPreview : FrameworkElement
{
    private static readonly Pen Outline = Frozen(new Pen(Brushes.White, 1));
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(0.8);

    private readonly DispatcherTimer _timer = new() { Interval = Duration };
    private Point _center;
    private double _diameter;
    private Brush _fill = Brushes.Red;

    public WidthPreview()
    {
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        _timer.Tick += (_, _) => { _timer.Stop(); Visibility = Visibility.Collapsed; };
    }

    public void Show(Point centerPx, double diameterPx, string colorHex)
    {
        _center = centerPx;
        _diameter = diameterPx;
        _fill = ColorHex.Brush(colorHex);
        Visibility = Visibility.Visible;
        InvalidateVisual();
        _timer.Stop();
        _timer.Start();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_diameter <= 0) return;
        dc.DrawEllipse(_fill, Outline, _center, _diameter / 2, _diameter / 2);
    }

    private static Pen Frozen(Pen p) { p.Freeze(); return p; }
}
