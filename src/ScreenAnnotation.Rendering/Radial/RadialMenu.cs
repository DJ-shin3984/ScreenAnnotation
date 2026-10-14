using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScreenAnnotation.Rendering.Radial;

public sealed record RadialItem(string Key, Brush Fill, Geometry? Icon);

/// <summary>우클릭 원형 팔레트. 호스트(물리 픽셀 캔버스)가 마우스 이벤트를 넘겨 준다.
/// 누른 채 끌다 떼면 방향 선택, 짧게 클릭하면 열린 채로 두고 좌클릭 선택.</summary>
public sealed class RadialMenu : Canvas
{
    public const double Radius = 70;
    public const double SlotSize = 44;
    private const double DeadZone = 20;
    private const int ShortClickMs = 250;

    private readonly List<(RadialItem Item, Grid Visual, Point Center)> _slots = [];
    private readonly Stopwatch _openedAt = new();
    private Point _center;
    private bool _clickMode;

    public RadialMenu()
    {
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
    }

    public bool IsOpen { get; private set; }
    public int? HighlightedIndex { get; private set; }

    public event Action<string>? Selected;
    public event Action? Cancelled;

    public void Open(Point centerPx, IReadOnlyList<RadialItem> items, Size hostSize)
    {
        Children.Clear();
        _slots.Clear();
        HighlightedIndex = null;
        _clickMode = false;

        // 링 전체가 호스트 안에 들어오도록 중심을 안쪽으로 민다.
        var margin = Radius + SlotSize / 2;
        _center = new Point(
            Math.Clamp(centerPx.X, margin, Math.Max(margin, hostSize.Width - margin)),
            Math.Clamp(centerPx.Y, margin, Math.Max(margin, hostSize.Height - margin)));

        for (var i = 0; i < items.Count; i++)
        {
            var angle = AngleOf(i, items.Count);
            var c = new Point(_center.X + Radius * Math.Cos(angle), _center.Y + Radius * Math.Sin(angle));
            var visual = BuildSlot(items[i], i);
            SetLeft(visual, c.X - SlotSize / 2);
            SetTop(visual, c.Y - SlotSize / 2);
            Children.Add(visual);
            _slots.Add((items[i], visual, c));
        }

        Visibility = Visibility.Visible;
        IsOpen = true;
        _openedAt.Restart();
    }

    public void Close()
    {
        IsOpen = false;
        _clickMode = false;
        HighlightedIndex = null;
        Visibility = Visibility.Collapsed;
    }

    public void OnMouseMove(Point p)
    {
        if (!IsOpen) return;
        Highlight(IndexByDirection(p));
    }

    public void OnRightButtonUp(Point p)
    {
        if (!IsOpen) return;
        var far = Distance(p, _center) > DeadZone;
        if (!far && _openedAt.ElapsedMilliseconds <= ShortClickMs)
        {
            _clickMode = true; // 짧은 클릭: 열어 두고 좌클릭으로 고른다
            return;
        }
        Highlight(IndexByDirection(p));
        Commit(HighlightedIndex);
    }

    public void OnLeftButtonDown(Point p)
    {
        if (!IsOpen || !_clickMode) return;
        Commit(IndexUnderPointer(p));
    }

    /// <summary>슬롯 0은 위(-90°), 시계 방향.</summary>
    private static double AngleOf(int index, int count) => -Math.PI / 2 + 2 * Math.PI * index / count;

    private int? IndexByDirection(Point p)
    {
        if (_slots.Count == 0 || Distance(p, _center) <= DeadZone) return null;
        var a = Math.Atan2(p.Y - _center.Y, p.X - _center.X);
        int? best = null;
        var bestDiff = double.MaxValue;
        for (var i = 0; i < _slots.Count; i++)
        {
            var diff = Math.Abs(Math.Atan2(Math.Sin(a - AngleOf(i, _slots.Count)), Math.Cos(a - AngleOf(i, _slots.Count))));
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    private int? IndexUnderPointer(Point p)
    {
        for (var i = 0; i < _slots.Count; i++)
            if (Distance(p, _slots[i].Center) <= SlotSize / 2) return i;
        return null;
    }

    private void Highlight(int? index)
    {
        if (index == HighlightedIndex) return;
        HighlightedIndex = index;
        for (var i = 0; i < _slots.Count; i++)
        {
            var on = i == index;
            var g = _slots[i].Visual;
            g.RenderTransform = on ? new ScaleTransform(1.2, 1.2, SlotSize / 2, SlotSize / 2) : null;
            ((Ellipse)g.Children[0]).Stroke = on ? Brushes.White : SlotOutline(_slots[i].Item);
            ((Ellipse)g.Children[0]).StrokeThickness = on ? 3 : 1;
        }
    }

    private void Commit(int? index)
    {
        var key = index is { } i ? _slots[i].Item.Key : null;
        Close();
        if (key is null) Cancelled?.Invoke();
        else Selected?.Invoke(key);
    }

    private static Grid BuildSlot(RadialItem item, int index)
    {
        var g = new Grid { Width = SlotSize, Height = SlotSize };
        g.Children.Add(new Ellipse { Fill = item.Fill, Stroke = SlotOutline(item), StrokeThickness = 1 });
        if (item.Icon is not null)
        {
            g.Children.Add(new Path
            {
                Data = item.Icon, Stroke = Brushes.White, StrokeThickness = 2, Fill = null,
                Width = 24, Height = 24, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            });
        }
        else if (index < 9)
        {
            var dark = item.Fill is SolidColorBrush { Color: var c } && c.R + c.G + c.B > 380;
            g.Children.Add(new TextBlock
            {
                Text = (index + 1).ToString(), FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = dark ? Brushes.Black : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            });
        }
        return g;
    }

    private static Brush SlotOutline(RadialItem item) =>
        item.Fill is SolidColorBrush { Color: var c } && (c.R + c.G + c.B > 700 || c.R + c.G + c.B < 60)
            ? Brushes.Gray : Brushes.Transparent;

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
