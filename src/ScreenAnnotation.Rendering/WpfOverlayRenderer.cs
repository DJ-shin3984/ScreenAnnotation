using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Rendering.Shapes;
using ScreenAnnotation.Rendering.Strokes;

namespace ScreenAnnotation.Rendering;

/// <summary>DrawingVisual 하나를 품는 최소 호스트.</summary>
internal sealed class VisualHost : FrameworkElement
{
    public DrawingVisual Visual { get; } = new();

    public VisualHost()
    {
        IsHitTestVisible = false;
        AddVisualChild(Visual);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => Visual;
}

/// <summary>L2 확정 판서 = RenderTargetBitmap 캐시, L3 그리는 중인 것 = DrawingVisual. 둘 다 모니터 로컬 물리 픽셀.</summary>
public sealed class WpfOverlayRenderer : IOverlayRenderer
{
    private readonly OverlayWindow _window;
    private readonly AnnotationStore _store;
    private readonly RenderTargetBitmap _l2;
    private readonly Image _l2Image;
    private readonly VisualHost _l3 = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private LiveStroke? _liveStroke;
    private LiveShape? _liveShape;
    private bool _ticking;

    public EphemeralStrokes Ephemeral { get; } = new();
    public LaserTrail Laser { get; } = new();
    public double LastL3FrameMs { get; private set; }

    public WpfOverlayRenderer(OverlayWindow window, AnnotationStore store)
    {
        _window = window;
        _store = store;
        var b = window.Monitor.Bounds;
        _l2 = new RenderTargetBitmap(b.Width, b.Height, 96, 96, PixelFormats.Pbgra32);
        _l2Image = new Image { Source = _l2, Stretch = Stretch.None, IsHitTestVisible = false, SnapsToDevicePixels = true };
        window.Viewport.Children.Add(_l2Image);
        window.Viewport.Children.Add(_l3);
    }

    public HashSet<Guid> HiddenIds { get; } = [];

    public void Start() => InvalidateAll();

    public void Stop()
    {
        _liveStroke = null;
        _liveShape = null;
        Ephemeral.Clear();
        Laser.Clear();
        RedrawL3();
        _l2.Clear();
    }

    // ponytail: 부분 재합성 대신 전체 다시 그림. 1,000개 전체 재합성이 16ms를 넘으면 더티 영역만 두 번째 RTB로 합성하는 방식으로 올린다.
    public void InvalidateAnnotations(RectPx dirtyLocal) => InvalidateAll();

    public void InvalidateAll()
    {
        _l2.Clear();
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            foreach (var a in _store.ForMonitor(_window.Monitor.Key))
                if (!HiddenIds.Contains(a.Id)) Draw(dc, a);
        _l2.Render(dv);
    }

    public void UpdateLiveStroke(LiveStroke? stroke) { _liveStroke = stroke; RedrawL3(); }
    public void UpdateLiveShape(LiveShape? shape) { _liveShape = shape; RedrawL3(); }

    /// <summary>프레임마다 호출: 사라지는 펜·레이저가 남아 있으면 갱신. 렌더러 밖(내보내기 등)에서도 쓸 수 있게 공개.</summary>
    public void RedrawL3()
    {
        var sw = Stopwatch.StartNew();
        using (var dc = _l3.Visual.RenderOpen())
        {
            foreach (var s in Ephemeral.Strokes) DrawEphemeral(dc, s);
            if (_liveStroke is { } ls)
            {
                var highlighter = ls.Kind != StrokeKind.Pen;
                if (highlighter) dc.PushOpacity(0.4);
                dc.DrawGeometry(null, Pen(ls.Color, ls.Width, round: !highlighter), StrokeBuilder.ToGeometry(ls.Points));
                if (highlighter) dc.Pop();
            }
            if (_liveShape is { } sh)
                DrawShape(dc, sh.Kind, sh.Start, sh.End, sh.Constrained, sh.Color, sh.Width);
            DrawLaser(dc);
        }
        LastL3FrameMs = sw.Elapsed.TotalMilliseconds;
        EnsureTicking();
    }

    private void DrawLaser(DrawingContext dc)
    {
        var samples = Laser.Samples;
        if (samples.Count == 0) return;
        var color = _liveStroke?.Color ?? "#FF3B30";
        var width = _liveStroke?.Width ?? 4;
        for (var i = 1; i < samples.Count; i++)
        {
            var t = (double)i / samples.Count;
            dc.PushOpacity(t * 0.6);
            dc.DrawLine(Pen(color, width * t, round: true), samples[i - 1].Point, samples[i].Point);
            dc.Pop();
        }
        var head = samples[^1].Point;
        dc.PushOpacity(0.5);
        dc.DrawEllipse(ColorHex.Brush(color), null, head, width * 2, width * 2);
        dc.Pop();
        dc.DrawEllipse(Brushes.White, null, head, width * 0.6, width * 0.6);
    }

    private void EnsureTicking()
    {
        var need = Ephemeral.Strokes.Count > 0 || Laser.Samples.Count > 0;
        if (need == _ticking) return;
        _ticking = need;
        if (need) CompositionTarget.Rendering += OnFrame;
        else CompositionTarget.Rendering -= OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var changed = Ephemeral.Tick(now);
        var laserBefore = Laser.Samples.Count;
        Laser.Prune(now);
        if (changed || Laser.Samples.Count != laserBefore) RedrawL3();
        else if (Ephemeral.Strokes.Count == 0 && Laser.Samples.Count == 0) EnsureTicking();
    }

    /// <summary>사라지는 펜 획 하나를 현재 불투명도로 그린다. 내보내기(T50)도 같은 코드를 쓴다.</summary>
    public static void DrawEphemeral(DrawingContext dc, EphemeralStrokes.Stroke s)
    {
        dc.PushOpacity(s.Opacity);
        dc.DrawGeometry(null, Pen(s.Color, s.Width, round: true), StrokeBuilder.ToGeometry(s.Points));
        dc.Pop();
    }

    /// <summary>확정 Annotation 하나를 그린다. 내보내기(T50)도 같은 코드를 쓴다.</summary>
    public static void Draw(DrawingContext dc, Annotation a)
    {
        switch (a)
        {
            case PenStroke p:
                var pts = p.Points.Select(q => new Point(q.X, q.Y)).ToList();
                var highlighter = p.Kind != StrokeKind.Pen;
                if (highlighter) dc.PushOpacity(0.4); // 획 단위 불투명: 같은 획 안에서 겹쳐도 진해지지 않음
                dc.DrawGeometry(null, Pen(p.Color, p.Width, round: !highlighter), StrokeBuilder.ToGeometry(pts));
                if (highlighter) dc.Pop();
                break;
            case ShapeAnnotation s:
                DrawShape(dc, s.Kind, new Point(s.Start.X, s.Start.Y), new Point(s.End.X, s.End.Y), s.Constrained, s.Color, s.Width);
                break;
            case TextAnnotation t:
                var ft = new FormattedText(t.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), t.FontSize, ColorHex.Brush(t.Color), 1.0);
                dc.DrawText(ft, new Point(t.Position.X, t.Position.Y));
                break;
            case PatchAnnotation patch:
                dc.DrawRectangle(ColorHex.Brush(patch.FillColor), null,
                    new Rect(patch.Area.X, patch.Area.Y, patch.Area.Width, patch.Area.Height));
                break;
            case ElementAnnotation:
                break; // T41에서 잘라낸 비트맵을 그린다
        }
    }

    private static void DrawShape(DrawingContext dc, ShapeKind kind, Point start, Point end, bool constrained, string color, double width)
    {
        var g = GeometryFactory.Build(kind, start, end, constrained, width);
        var pen = Pen(color, width, round: true);
        dc.DrawGeometry(kind == ShapeKind.Arrow ? ColorHex.Brush(color) : null, pen, g.Solid); // 화살표 머리는 채움
        if (g.Dashed is not null)
        {
            var dashed = new Pen(ColorHex.Brush(color), width) { DashStyle = DashStyles.Dash, DashCap = PenLineCap.Flat };
            dashed.Freeze();
            dc.DrawGeometry(null, dashed, g.Dashed);
        }
    }

    private static Pen Pen(string color, double width, bool round)
    {
        var cap = round ? PenLineCap.Round : PenLineCap.Flat;
        var pen = new Pen(ColorHex.Brush(color), width) { StartLineCap = cap, EndLineCap = cap, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }
}
