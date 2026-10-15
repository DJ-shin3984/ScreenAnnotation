using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Recognition;
using ScreenAnnotation.Core.Settings;
using ScreenAnnotation.Core.Tools;
using ScreenAnnotation.Rendering;
using ScreenAnnotation.Rendering.Hud;
using ScreenAnnotation.Rendering.Radial;
using ScreenAnnotation.Rendering.Shapes;
using ScreenAnnotation.Rendering.Strokes;
using ScreenAnnotation.Rendering.Toolbar;
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
    private bool _panning;
    private bool _spaceDown;
    private Point _lastScreen;
    private EphemeralStrokes.Stroke? _ephemeral;
    private readonly HashSet<Guid> _erased = [];
    private readonly TextEditor _text;
    private readonly ToolbarControl _bar = new();
    private Point _lastErase;
    private long _lastMoveTick;
    private int _hiddenDrawn;

    public event Action? SaveRequested;
    public event Action? CopyRequested;
    public event Action? CloseRequested;

    public void Flash(string message) => _hud.Flash(message, TimeSpan.FromSeconds(1));

    // 사라지는 펜·레이저 시계: 렌더러도 같은 Environment.TickCount64 기준으로 Tick/Prune 한다.
    private static TimeSpan Now => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public BoardInput(OverlayWindow window, IOverlayRenderer renderer, ToolState tools, AnnotationStore store,
        HistoryManager history, TextEditor text, SettingsStore settingsStore, ILogger<BoardInput> log)
    {
        _text = text;
        _text.Closed += () => tools.Reset();
        _window = window;
        _renderer = renderer;
        _tools = tools;
        _store = store;
        _history = history;
        _log = log;
        _monitorKey = window.Monitor.Key;
        _renderer.Ephemeral.Hold = TimeSpan.FromSeconds(settingsStore.Current.EphemeralSeconds);
        settingsStore.Changed += () => _renderer.Ephemeral.Hold = TimeSpan.FromSeconds(settingsStore.Current.EphemeralSeconds);

        _layer.Width = window.Monitor.Bounds.Width;
        _layer.Height = window.Monitor.Bounds.Height;
        _layer.Children.Add(_eraserCursor);
        _layer.Children.Add(_widthPreview);
        _layer.Children.Add(_menu);
        _layer.Children.Add(_hud);
        _layer.Children.Add(_bar);
        Canvas.SetRight(_bar, ToolbarControl.EdgeMargin);
        Canvas.SetTop(_bar, Math.Max(0, (window.Monitor.Bounds.Height - _bar.PreferredHeight) / 2));
        _bar.Action += OnToolbar;
        _bar.ColorPicked += n => _tools.SetColor(n);
        _bar.ShapePicked += k => { _tools.SetTool(Tool.Shape); _tools.SetShape(k); };
        _tools.Changed += () => _bar.Sync(_tools);
        _bar.Sync(_tools);
        Canvas.SetRight(_hud, HudControl.EdgeMargin);
        Canvas.SetBottom(_hud, HudControl.EdgeMargin);
        window.Root.Children.Add(_layer);

        _menu.Selected += OnMenuSelected;
        _tools.Changed += UpdateHud;
        UpdateHud();

        window.PreviewMouseLeftButtonDown += OnLeftDown;
        window.PreviewMouseMove += OnMove;
        window.PreviewMouseLeftButtonUp += OnLeftUp;
        window.PreviewMouseDown += OnMiddleDown;
        window.PreviewMouseUp += OnMiddleUp;
        window.PreviewKeyUp += OnKeyUp;
        window.PreviewMouseRightButtonDown += OnRightDown;
        window.PreviewMouseRightButtonUp += OnRightUp;
        window.PreviewMouseWheel += OnWheel;
        window.PreviewKeyDown += OnKeyDown;
    }

    private static bool Ctrl => Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
    private static bool Shift => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
    /// <summary>저장 좌표(content px). Viewport의 RenderTransform 역변환을 WPF가 적용한다.</summary>
    private Point Pos(MouseEventArgs e) => e.GetPosition(_window.Viewport);
    /// <summary>화면 좌표(px). L4(HUD·팔레트·미리보기)와 이동량에 쓴다.</summary>
    private Point ScreenPos(MouseEventArgs e) => e.GetPosition(_window.Root);
    /// <summary>확대 중 그린 획 굵기 = 보이는 굵기 ÷ 배율.</summary>
    private double ContentWidth => _tools.Width / _window.View.Scale;
    private Size Host => new(_window.Monitor.Bounds.Width, _window.Monitor.Bounds.Height);

    // ---- 마우스 ----

    private void OnLeftDown(object sender, MouseButtonEventArgs e)
    {
        var p = Pos(e);
        if (_menu.IsOpen) { _menu.OnLeftButtonDown(ScreenPos(e)); e.Handled = true; return; }
        if (_bar.IsMouseOver) return; // 툴바 클릭은 툴바가 처리
        if (_text.IsEditing)
        {
            if (!_text.Contains(ScreenPos(e))) { _text.Commit(); e.Handled = true; }
            return;
        }
        if (Ctrl) return; // Ctrl+클릭은 요소 인식(ElementProbe) 몫
        if (e.ClickCount == 2)
        {
            // 첫 클릭이 점 하나짜리 획을 남겼으면 되돌린다 (ponytail: 클릭 지연 없이 단순하게)
            if (_tools.Tool is Tool.Pen or Tool.Highlighter && _history.CanUndo) { _history.Undo(); _renderer.InvalidateAll(); }
            if (!_text.TryBeginEdit(p, out _)) _text.Begin(p, _tools.Color, TextEditor.FontSizeFor(_tools.Width));
            e.Handled = true;
            return;
        }
        if (_spaceDown) { BeginPan(e); return; }

        _start = p;
        _dragging = true;
        _window.CaptureMouse();
        e.Handled = true;

        switch (_tools.Tool)
        {
            case Tool.Pen when _tools.PenVariant == PenVariant.Ephemeral:
                _ephemeral = _renderer.Ephemeral.Begin(_tools.Color, ContentWidth);
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
                    Width = ContentWidth,
                };
                _live.Points.Add(p);
                _renderer.UpdateLiveStroke(_live);
                break;
            case Tool.Shape:
                _renderer.UpdateLiveShape(new LiveShape(_tools.ShapeKind, p, p, Shift, _tools.Color, ContentWidth));
                break;
            case Tool.Eraser:
                _erased.Clear();
                _lastErase = p;
                EraseAt(p);
                break;
        }
        _lastMoveTick = Environment.TickCount64;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_panning)
        {
            var sp = ScreenPos(e);
            _window.View.Pan(sp.X - _lastScreen.X, sp.Y - _lastScreen.Y);
            _lastScreen = sp;
            _window.ApplyView();
            return;
        }
        var p = Pos(e);
        if (_tools.Tool == Tool.Eraser) MoveEraserCursor(ScreenPos(e));
        if (_menu.IsOpen) { _menu.OnMouseMove(ScreenPos(e)); return; }
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
                else if (StrokeBuilder.ShouldAppend(_live.Points, p)) { _live.Points.Add(p); _lastMoveTick = Environment.TickCount64; }
                _renderer.UpdateLiveStroke(_live);
                break;
            case Tool.Shape:
                _renderer.UpdateLiveShape(new LiveShape(_tools.ShapeKind, _start, p, Shift, _tools.Color, ContentWidth));
                break;
            case Tool.Eraser:
                EraseAlong(_lastErase, p);
                _lastErase = p;
                break;
        }
    }

    private void OnLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (_panning) { EndPan(); e.Handled = true; return; }
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
            var held = Environment.TickCount64 - _lastMoveTick;
            _live = null;
            _renderer.UpdateLiveStroke(null);
            Commit(new AddCommand(stroke), stroke.Bounds);
            // 획 끝에서 버튼을 누른 채 0.5초 멈추면 도형 보정 (Undo 한 번이면 원래 획)
            if (stroke.Kind == StrokeKind.Pen && held >= 500 && ShapeRecognizer.Recognize(stroke) is { } corrected)
                Commit(new ReplaceCommand(stroke.Id, corrected), AnnotationStore.Union(stroke.Bounds, corrected.Bounds));
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
                Kind = _tools.ShapeKind, Color = _tools.Color, Width = (float)ContentWidth, Constrained = Shift,
                Start = new PointPx((int)Math.Round(s.X), (int)Math.Round(s.Y)),
                End = new PointPx((int)Math.Round(en.X), (int)Math.Round(en.Y)),
                Bounds = default,
            };
            shape = shape with { Bounds = Inflate(ShapeAnnotation.BoundsOf(shape.Start, shape.End), ContentWidth) };
            Commit(new AddCommand(shape), shape.Bounds);
            return;
        }

        if (_tools.Tool == Tool.Eraser && _erased.Count > 0)
        {
            _history.Execute(new CompositeCommand(_erased.Select(id => (IAnnotationCommand)new RemoveCommand(id)).ToList()));
            _erased.Clear();
            _renderer.HiddenIds.Clear();
            _hiddenDrawn = 0;
            _renderer.InvalidateAll();
        }
    }

    /// <summary>지우개 경로를 반지름 간격으로 샘플링해 빠르게 움직여도 사이 객체를 놓치지 않는다.</summary>
    private void EraseAlong(Point a, Point b)
    {
        var step = Math.Max(ContentWidth / 2.0, 2);
        var n = Math.Max(1, (int)Math.Ceiling((b - a).Length / step));
        for (var i = 1; i <= n; i++)
            EraseAt(a + (b - a) * (i / (double)n));
    }

    private void OnToolbar(ToolbarAction a)
    {
        switch (a)
        {
            case ToolbarAction.Text:
                _text.Begin(_window.View.ToContent(Host.Width / 2, Host.Height / 2) is var (cx, cy) ? new Point(cx, cy) : default,
                    _tools.Color, TextEditor.FontSizeFor(_tools.Width));
                break;
            case ToolbarAction.Undo: if (_history.Undo()) _renderer.InvalidateAll(); break;
            case ToolbarAction.Redo: if (_history.Redo()) _renderer.InvalidateAll(); break;
            case ToolbarAction.Save: SaveRequested?.Invoke(); break;
            case ToolbarAction.Copy: CopyRequested?.Invoke(); break;
            case ToolbarAction.ZoomReset: _window.View.Reset(); _window.ApplyView(); break;
            case ToolbarAction.Close: CloseRequested?.Invoke(); break;
            default: ToolbarControl.Apply(_tools, a); break;
        }
    }

    private void OnRightDown(object sender, MouseButtonEventArgs e)
    {
        if (_dragging || Ctrl) return;
        e.Handled = true;
        _menu.Open(ScreenPos(e), _tools.Tool == Tool.Shape ? RadialItems.Shapes() : RadialItems.Colors(), Host);
    }

    private void OnRightUp(object sender, MouseButtonEventArgs e)
    {
        if (!_menu.IsOpen) return;
        e.Handled = true;
        _menu.OnRightButtonUp(ScreenPos(e));
    }

    private void OnMenuSelected(string key)
    {
        var color = RadialItems.ColorKeys.ToList().IndexOf(key);
        if (color >= 0) _tools.SetColor(color + 1);
        else if (Enum.TryParse<ShapeKind>(key, out var kind)) _tools.SetShape(kind);
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (Ctrl)
        {
            var sp = ScreenPos(e);
            _window.View.ZoomAt(sp.X, sp.Y, e.Delta > 0);
            _window.ApplyView();
            return;
        }
        if (_tools.Wheel(e.Delta))
            _widthPreview.Show(ScreenPos(e), _tools.Width, _tools.Color);
    }

    // ---- 화면 이동 (가운데 버튼 / Space+드래그) ----

    private void OnMiddleDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || _dragging) return;
        BeginPan(e);
    }

    private void OnMiddleUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || !_panning) return;
        EndPan();
        e.Handled = true;
    }

    private void BeginPan(MouseButtonEventArgs e)
    {
        _panning = true;
        _lastScreen = ScreenPos(e);
        _window.CaptureMouse();
        e.Handled = true;
    }

    private void EndPan()
    {
        _panning = false;
        if (_window.IsMouseCaptured) _window.ReleaseMouseCapture();
    }

    // ---- 키 ----

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_text.IsEditing) return; // TextBox가 처리 (Esc·Ctrl+Enter·Tab)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var handled = true;
        switch (key)
        {
            case Key.Space: _spaceDown = true; break;
            case Key.D0 when Ctrl:
            case Key.NumPad0 when Ctrl:
                _window.View.Reset(); _window.ApplyView(); break;
            case Key.Tab when Shift: _tools.NextVariant(); break;
            case Key.Tab: _tools.NextTool(); break;
            case Key.Escape: Reset(); _tools.Reset(); break;
            case Key.E: _tools.ToggleEraser(); break;
            case Key.T: _text.Begin(Mouse.GetPosition(_window.Viewport), _tools.Color, TextEditor.FontSizeFor(_tools.Width)); break;
            case Key.S when Ctrl: SaveRequested?.Invoke(); break;
            case Key.C when Ctrl: CopyRequested?.Invoke(); break;
            case Key.Z when Ctrl: if (_history.Undo()) _renderer.InvalidateAll(); break;
            case Key.Y when Ctrl: if (_history.Redo()) _renderer.InvalidateAll(); break;
            case >= Key.D1 and <= Key.D9: _tools.SetColor(key - Key.D0); break;
            case >= Key.NumPad1 and <= Key.NumPad9: _tools.SetColor(key - Key.NumPad0); break;
            case Key.Delete: break; // T42 선택 객체 삭제
            default: handled = false; break;
        }
        e.Handled = handled;
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;
        _spaceDown = false;
        e.Handled = true;
    }

    // ---- 지우개 ----

    private void EraseAt(Point p)
    {
        var radius = ContentWidth / 2.0;
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
            if (hit && _erased.Add(a.Id)) _renderer.HiddenIds.Add(a.Id);
        }
        if (_renderer.HiddenIds.Count != _hiddenDrawn)
        {
            _hiddenDrawn = _renderer.HiddenIds.Count;
            _renderer.InvalidateAll(); // 지나간 객체는 바로 사라져 보인다
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
        _panning = false;
        _spaceDown = false;
        _live = null;
        _ephemeral = null;
        _erased.Clear();
        _renderer.HiddenIds.Clear();
        _hiddenDrawn = 0;
        if (_text.IsEditing) _text.Cancel();
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
        _window.PreviewMouseDown -= OnMiddleDown;
        _window.PreviewMouseUp -= OnMiddleUp;
        _window.PreviewKeyUp -= OnKeyUp;
        _window.PreviewMouseRightButtonDown -= OnRightDown;
        _window.PreviewMouseRightButtonUp -= OnRightUp;
        _window.PreviewMouseWheel -= OnWheel;
        _window.PreviewKeyDown -= OnKeyDown;
        _window.Root.Children.Remove(_layer);
    }
}
