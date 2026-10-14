using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Settings;
using ScreenAnnotation.Core.Tools;
using ScreenAnnotation.Rendering;
using ScreenAnnotation.Rendering.Hud;
using ScreenAnnotation.Rendering.Radial;
using ScreenAnnotation.Rendering.Shapes;
using ScreenAnnotation.Rendering.Strokes;
using Brushes = System.Windows.Media.Brushes;
using Ellipse = System.Windows.Shapes.Ellipse;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ScreenAnnotation.App;

/// <summary>오버레이 창 하나의 판서 입력: 펜·형광펜·도형·지우개·키·휠·우클릭 팔레트·HUD (L4).</summary>
public sealed class BoardInput : IDisposable
{
    private readonly OverlayWindow _window;
    private readonly IOverlayRenderer _renderer;
    private readonly ToolState _tools;
    private readonly AnnotationStore _store;
    private readonly HistoryManager _history;
    private readonly ILogger<BoardInput> _log;
    private readonly string _monitorKey;

    private readonly Canvas _layer = new() { IsHitTestVisible = false };
    private readonly HudControl _hud = new();
    private readonly WidthPreview _widthPreview = new();
    private readonly RadialMenu _menu = new();
    private readonly Ellipse _eraserCursor = new() { Stroke = Brushes.White, StrokeThickness = 1.5, Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0, 0, 0)), Visibility = Visibility.Collapsed };

    private LiveStroke? _live;
    private Point _start;
    private bool _dragging;
    private EphemeralStrokes.Stroke? _ephemeral;
    private readonly HashSet<Guid> _erased = [];

    // 사라지는 펜·레이저 시계: 렌더러도 같은 Environment.TickCount64 기준으로 Tick/Prune 한다.
    private static TimeSpan Now => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public BoardInput(OverlayWindow window, IOverlayRenderer renderer, ToolState tools, AnnotationStore store,
        HistoryManager history, Settings settings, ILogger<BoardInput> log)
    {
        _window = window;
        _renderer = renderer;
        _tools = tools;
        _store = store;
        _history = history;
        _log = log;
        _monitorKey = window.Monitor.Key;
        _renderer.Ephemeral.Hold = TimeSpan.FromSeconds(settings.EphemeralSeconds);

        _layer.Width = window.Monitor.Bounds.Width;
        _layer.Height = window.Monitor.Bounds.Height;
        _layer.Children.Add(_eraserCursor);
        _layer.Children.Add(_widthPreview);
        _layer.Children.Add(_menu);
        _layer.Children.Add(_hud);
        Canvas.SetRight(_hud, HudControl.EdgeMargin);
        Canvas.SetBottom(_hud, HudControl.EdgeMargin);
        window.Root.Children.Add(_layer);

        _menu.Selected += OnMenuSelected;
        _tools.Changed += UpdateHud;
        UpdateHud();

        window.PreviewMouseLeftButtonDown += OnLeftDown;
        window.PreviewMouseMove += OnMove;
        window.PreviewMouseLeftButtonUp += OnLeftUp;
        window.PreviewMouseRightButtonDown += OnRightDown;
        window.PreviewMouseRightButtonUp += OnRightUp;
        window.PreviewMouseWheel += OnWheel;
        window.PreviewKeyDown += OnKeyDown;
    }

    private static bool Ctrl => Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
    private static bool Shift => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
    private Point Pos(MouseEventArgs e) => e.GetPosition(_window.Root);
    private Size Host => new(_window.Monitor.Bounds.Width, _window.Monitor.Bounds.Height);

    // ---- 마우스 ----

    private void OnLeftDown(object sender, MouseButtonEventArgs e)
    {
        var p = Pos(e);
        if (_menu.IsOpen) { _menu.OnLeftButtonDown(p); e.Handled = true; return; }
        if (Ctrl) return; // Ctrl+클릭은 요소 인식(ElementProbe) 몫

        _start = p;
        _dragging = true;
        _window.CaptureMouse();
        e.Handled = true;

        switch (_tools.Tool)
        {
            case Tool.Pen when _tools.PenVariant == PenVariant.Ephemeral:
                _ephemeral = _renderer.Ephemeral.Begin(_tools.Color, _tools.Width);
                _ephemeral.Points.Add(p);
                break;
            case Tool.Pen when _tools.PenVariant == PenVariant.Laser:
                _renderer.Laser.Add(p, Now);
                break;
            case Tool.Pen:
            case Tool.Highlighter:
                _live = new LiveStroke
                {
                    Kind = _tools.Tool == Tool.Pen ? StrokeKind.Pen
                         : _tools.HighlighterVariant == HighlighterVariant.Straight ? StrokeKind.StraightHighlighter : StrokeKind.Highlighter,
                    Color = _tools.Color,
                    Width = _tools.Width,
                };
                _live.Points.Add(p);
                _renderer.UpdateLiveStroke(_live);
                break;
            case Tool.Shape:
                _renderer.UpdateLiveShape(new LiveShape(_tools.ShapeKind, p, p, Shift, _tools.Color, _tools.Width));
                break;
            case Tool.Eraser:
                _erased.Clear();
                EraseAt(p);
                break;
        }
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        var p = Pos(e);
        if (_tools.Tool == Tool.Eraser) MoveEraserCursor(p);
        if (_menu.IsOpen) { _menu.OnMouseMove(p); return; }
        if (!_dragging) return;

        switch (_tools.Tool)
        {
            case Tool.Pen when _ephemeral is not null:
                if (StrokeBuilder.ShouldAppend(_ephemeral.Points, p)) _ephemeral.Points.Add(p);
                break;
            case Tool.Pen when _tools.PenVariant == PenVariant.Laser:
                _renderer.Laser.Add(p, Now);
                break;
            case Tool.Pen:
            case Tool.Highlighter when _live is not null:
                if (_live is null) break;
                if (_live.Kind == StrokeKind.StraightHighlighter)
                {
                    _live.Points.Clear();
                    _live.Points.Add(_start);
                    _live.Points.Add(SnapHorizontal(_start, p));
                }
                else if (StrokeBuilder.ShouldAppend(_live.Points, p)) _live.Points.Add(p);
                _renderer.UpdateLiveStroke(_live);
                break;
            case Tool.Shape:
                _renderer.UpdateLiveShape(new LiveShape(_tools.ShapeKind, _start, p, Shift, _tools.Color, _tools.Width));
                break;
            case Tool.Eraser:
                EraseAt(p);
                break;
        }
    }

    private void OnLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _window.ReleaseMouseCapture();
        e.Handled = true;
        var p = Pos(e);

        if (_ephemeral is not null)
        {
            _ephemeral = null;
            _renderer.Ephemeral.End(Now);
            return;
        }

        if (_live is not null)
        {
            var pts = _live.Kind == StrokeKind.StraightHighlighter
                ? new List<Point> { _start, SnapHorizontal(_start, p) }
                : StrokeBuilder.Simplify(_live.Points, 0.5);
            var stroke = new PenStroke
            {
                Id = Guid.NewGuid(), MonitorKey = _monitorKey, Order = AnnotationIds.Next(),
                Kind = _live.Kind, Color = _live.Color, Width = (float)_live.Width,
                Points = pts.Select(q => new PointF2((float)q.X, (float)q.Y)).ToList(),
                Bounds = Inflate(BoundsOf(pts), _live.Width),
            };
            _live = null;
            _renderer.UpdateLiveStroke(null);
            Commit(new AddCommand(stroke), stroke.Bounds);
            return;
        }

        if (_tools.Tool == Tool.Shape)
        {
            _renderer.UpdateLiveShape(null);
            var (s, en) = Shift ? GeometryFactory.Constrain(_tools.ShapeKind, _start, p) : (_start, p);
            if (Math.Abs(en.X - s.X) < 2 && Math.Abs(en.Y - s.Y) < 2) return;
            var shape = new ShapeAnnotation
            {
                Id = Guid.NewGuid(), MonitorKey = _monitorKey, Order = AnnotationIds.Next(),
                Kind = _tools.ShapeKind, Color = _tools.Color, Width = _tools.Width, Constrained = Shift,
                Start = new PointPx((int)Math.Round(s.X), (int)Math.Round(s.Y)),
                End = new PointPx((int)Math.Round(en.X), (int)Math.Round(en.Y)),
                Bounds = default,
            };
            shape = shape with { Bounds = Inflate(ShapeAnnotation.BoundsOf(shape.Start, shape.End), _tools.Width) };
            Commit(new AddCommand(shape), shape.Bounds);
            return;
        }

        if (_tools.Tool == Tool.Eraser && _erased.Count > 0)
        {
            _history.Execute(new CompositeCommand(_erased.Select(id => (IAnnotationCommand)new RemoveCommand(id)).ToList()));
            _erased.Clear();
            _renderer.InvalidateAll();
        }
    }

    private void OnRightDown(object sender, MouseButtonEventArgs e)
    {
        if (_dragging || Ctrl) return;
        e.Handled = true;
        _menu.Open(Pos(e), _tools.Tool == Tool.Shape ? RadialItems.Shapes() : RadialItems.Colors(), Host);
    }

    private void OnRightUp(object sender, MouseButtonEventArgs e)
    {
        if (!_menu.IsOpen) return;
        e.Handled = true;
        _menu.OnRightButtonUp(Pos(e));
    }

    private void OnMenuSelected(string key)
    {
        var color = RadialItems.ColorKeys.ToList().IndexOf(key);
        if (color >= 0) _tools.SetColor(color + 1);
        else if (Enum.TryParse<ShapeKind>(key, out var kind)) _tools.SetShape(kind);
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (Ctrl) return; // Ctrl+휠 확대는 T30
        e.Handled = true;
        if (_tools.Wheel(e.Delta))
            _widthPreview.Show(Pos(e), _tools.Width, _tools.Color);
    }

    // ---- 키 ----

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var handled = true;
        switch (key)
        {
            case Key.Tab when Shift: _tools.NextVariant(); break;
            case Key.Tab: _tools.NextTool(); break;
            case Key.Escape: Reset(); _tools.Reset(); break;
            case Key.E: _tools.ToggleEraser(); break;
            case Key.Z when Ctrl: if (_history.Undo()) _renderer.InvalidateAll(); break;
            case Key.Y when Ctrl: if (_history.Redo()) _renderer.InvalidateAll(); break;
            case >= Key.D1 and <= Key.D9: _tools.SetColor(key - Key.D0); break;
            case >= Key.NumPad1 and <= Key.NumPad9: _tools.SetColor(key - Key.NumPad0); break;
            case Key.Delete: break; // T42 선택 객체 삭제
            default: handled = false; break;
        }
        e.Handled = handled;
    }

    // ---- 지우개 ----

    private void EraseAt(Point p)
    {
        var radius = _tools.Width / 2.0;
        var x = (float)p.X;
        var y = (float)p.Y;
        foreach (var a in _store.ForMonitor(_monitorKey))
        {
            if (_erased.Contains(a.Id)) continue;
            var b = a.Bounds;
            if (p.X < b.X - radius || p.Y < b.Y - radius || p.X > b.X + b.Width + radius || p.Y > b.Y + b.Height + radius) continue;

            var hit = a switch
            {
                PenStroke s => HitTest.DistanceToPolyline(s.Points, x, y) <= Math.Max(s.Width / 2, 6) + radius,
                ShapeAnnotation s => GeometryFactory
                    .Build(s.Kind, new Point(s.Start.X, s.Start.Y), new Point(s.End.X, s.End.Y), false, s.Width).Solid
                    .StrokeContains(new Pen(Brushes.Black, s.Width + radius * 2), p),
                _ => true, // Text·Patch·Element: Bounds 판정
            };
            if (hit) _erased.Add(a.Id);
        }
    }

    private void MoveEraserCursor(Point p)
    {
        var d = _tools.Width;
        _eraserCursor.Width = _eraserCursor.Height = d;
        Canvas.SetLeft(_eraserCursor, p.X - d / 2.0);
        Canvas.SetTop(_eraserCursor, p.Y - d / 2.0);
        _eraserCursor.Visibility = Visibility.Visible;
    }

    // ---- 공통 ----

    private void Commit(IAnnotationCommand cmd, RectPx dirty)
    {
        _history.Execute(cmd);
        _renderer.InvalidateAnnotations(dirty);
    }

    private void UpdateHud()
    {
        var (tool, variant) = _tools.Tool switch
        {
            Tool.Pen => ("펜", _tools.PenVariant switch { PenVariant.Ephemeral => "사라지는", PenVariant.Laser => "레이저", _ => "일반" }),
            Tool.Highlighter => ("형광펜", _tools.HighlighterVariant == HighlighterVariant.Straight ? "직선" : "자유"),
            Tool.Shape => ("도형", _tools.ShapeKind switch
            {
                ShapeKind.Rect => "사각형", ShapeKind.Triangle => "삼각형", ShapeKind.Ellipse => "원",
                ShapeKind.Cylinder => "원기둥", ShapeKind.Arrow => "화살표", _ => "직선",
            }),
            _ => ("지우개", "원"),
        };
        _hud.Update(tool, variant, _tools.Width, _tools.Color);
        if (_tools.Tool != Tool.Eraser) _eraserCursor.Visibility = Visibility.Collapsed;
    }

    private static Point SnapHorizontal(Point a, Point b)
    {
        var angle = Math.Abs(Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI);
        return angle <= 10 || angle >= 170 ? new Point(b.X, a.Y) : b;
    }

    private static RectPx BoundsOf(IReadOnlyList<Point> pts)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in pts)
        {
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
        }
        return new RectPx((int)Math.Floor(minX), (int)Math.Floor(minY), (int)Math.Ceiling(maxX - minX), (int)Math.Ceiling(maxY - minY));
    }

    private static RectPx Inflate(RectPx r, double by)
    {
        var i = (int)Math.Ceiling(by);
        return new RectPx(r.X - i, r.Y - i, r.Width + 2 * i, r.Height + 2 * i);
    }

    /// <summary>진행 중인 작업 취소, 메뉴 닫기. 보드 종료·Esc에 호출.</summary>
    public void Reset()
    {
        _dragging = false;
        _live = null;
        _ephemeral = null;
        _erased.Clear();
        if (_window.IsMouseCaptured) _window.ReleaseMouseCapture();
        _renderer.UpdateLiveStroke(null);
        _renderer.UpdateLiveShape(null);
        _menu.Close();
    }

    public void Dispose()
    {
        Reset();
        _tools.Changed -= UpdateHud;
        _menu.Selected -= OnMenuSelected;
        _window.PreviewMouseLeftButtonDown -= OnLeftDown;
        _window.PreviewMouseMove -= OnMove;
        _window.PreviewMouseLeftButtonUp -= OnLeftUp;
        _window.PreviewMouseRightButtonDown -= OnRightDown;
        _window.PreviewMouseRightButtonUp -= OnRightUp;
        _window.PreviewMouseWheel -= OnWheel;
        _window.PreviewKeyDown -= OnKeyDown;
        _window.Root.Children.Remove(_layer);
    }
}
