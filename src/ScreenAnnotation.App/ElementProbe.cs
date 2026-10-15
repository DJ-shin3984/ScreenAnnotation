using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseEventHandler = System.Windows.Input.MouseEventHandler;
using KeyEventHandler = System.Windows.Input.KeyEventHandler;
using Clipboard = System.Windows.Clipboard;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Recognition;
using ScreenAnnotation.Rendering;

namespace ScreenAnnotation.App;

/// <summary>
/// Ctrl+클릭 요소 조작 (T40~T42). Ctrl 누르면 점선 미리보기, Ctrl+클릭으로 선택(같은 자리 반복 = 한 단계 큰 범위),
/// 드래그 이동(처음 움직일 때 구멍 메움 패치와 함께 Lift), Alt+드래그 복제, 모서리 핸들 비율 크기 조절, Delete, Ctrl+C.
/// 창 클래스 핸들러로 등록해 BoardInput(인스턴스 핸들러)보다 먼저 받는다. 선택 중에는 그 위의 판서 입력을 가로챈다.
/// </summary>
public sealed class ElementProbe
{
    private static ElementProbe? _current;

    static ElementProbe()
    {
        EventManager.RegisterClassHandler(typeof(OverlayWindow), UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) => _current?.OnDown((OverlayWindow)s, e)));
        EventManager.RegisterClassHandler(typeof(OverlayWindow), UIElement.PreviewMouseMoveEvent, new MouseEventHandler((s, e) => _current?.OnMove((OverlayWindow)s, e)));
        EventManager.RegisterClassHandler(typeof(OverlayWindow), UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) => _current?.OnUp((OverlayWindow)s, e)));
        EventManager.RegisterClassHandler(typeof(OverlayWindow), UIElement.PreviewKeyDownEvent, new KeyEventHandler((s, e) => _current?.OnKey((OverlayWindow)s, e)));
        EventManager.RegisterClassHandler(typeof(OverlayWindow), UIElement.PreviewKeyUpEvent, new KeyEventHandler((s, e) => _current?.OnKeyUp((OverlayWindow)s, e)));
    }

    private readonly IElementDetector _detector;
    private readonly ModeHolder _mode;
    private readonly HistoryManager _history;
    private readonly ILogger<ElementProbe> _log;

    private sealed class Board
    {
        public required Rectangle Preview;
        public required Rectangle Frame;
        public required Rectangle[] Handles; // 0..7: TL, T, TR, R, BR, B, BL, L
        public BitmapSource? Bitmap; // 정지 화면 전체 (요소 잘라내기용), 보드마다 새로
    }

    private readonly Dictionary<OverlayWindow, Board> _boards = [];
    private bool _busy;

    // 선택 상태 (한 번에 하나)
    private OverlayWindow? _selWindow;
    private ElementAnnotation? _sel;   // _lifted면 store에 있음, 아니면 아직 "선택만"
    private bool _lifted;
    private DetectedElement? _levels;
    private int _level;
    private (int X, int Y) _lastClick;
    private string _patchColor = "#FFFFFF";

    private enum Drag { None, Move, Resize }
    private Drag _drag;
    private int _handle;
    private Point _dragStart;
    private RectPx _dragOrigin;
    private int _replacesInDrag;

    public ElementProbe(IElementDetector detector, ModeHolder mode, HistoryManager history, ILogger<ElementProbe> log)
    {
        _detector = detector; _mode = mode; _history = history; _log = log;
        _current = this;
    }

    public bool HasSelection => _sel is not null;

    public void Attach(OverlayWindow w)
    {
        if (_boards.TryGetValue(w, out var existing)) { existing.Bitmap = null; return; }
        var canvas = new Canvas { IsHitTestVisible = false };
        var preview = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 2, StrokeDashArray = [4, 2], Visibility = Visibility.Collapsed };
        var frame = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 1.5, Visibility = Visibility.Collapsed };
        var handles = new Rectangle[8];
        canvas.Children.Add(preview);
        canvas.Children.Add(frame);
        for (var i = 0; i < 8; i++)
        {
            handles[i] = new Rectangle { Width = 10, Height = 10, Fill = Brushes.White, Stroke = Brushes.DodgerBlue, StrokeThickness = 1.5, Visibility = Visibility.Collapsed };
            canvas.Children.Add(handles[i]);
        }
        w.Viewport.Children.Add(canvas); // content px: 확대를 따라간다
        _boards[w] = new Board { Preview = preview, Frame = frame, Handles = handles };
    }

    /// <summary>보드 종료: 선택·미리보기·비트맵 캐시 해제.</summary>
    public void Reset()
    {
        Deselect();
        foreach (var b in _boards.Values) { b.Preview.Visibility = Visibility.Collapsed; b.Bitmap = null; }
        ElementImageCache.Clear();
        _levels = null;
    }

    public void Deselect()
    {
        _sel = null; _lifted = false; _drag = Drag.None;
        if (_selWindow is { } w && _boards.TryGetValue(w, out var b)) ShowSelection(b, null);
        _selWindow = null;
    }

    // ---- 입력 ----

    private static bool Ctrl => Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
    private static bool Alt => Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

    private async void OnDown(OverlayWindow w, MouseButtonEventArgs e)
    {
        if (!_boards.TryGetValue(w, out var b)) return;
        var p = e.GetPosition(w.Viewport);

        if (_sel is not null && _selWindow == w && !Ctrl)
        {
            var h = HitHandle(p);
            if (h >= 0) { BeginDrag(w, Drag.Resize, h, p); e.Handled = true; return; }
            if (_sel.Bounds.Contains((int)p.X, (int)p.Y))
            {
                if (Alt) Duplicate();
                BeginDrag(w, Drag.Move, -1, p);
                e.Handled = true;
                return;
            }
            Deselect(); // 빈 곳 클릭: 선택 해제, 이벤트는 BoardInput으로 흘려 보낸다
            return;
        }

        if (!Ctrl || _mode.Snapshot is not { } snap || _busy) return;
        e.Handled = true;
        var g = Global(w, p);
        var frame = FrameOf(w);

        if (_levels is not null && _selWindow == w && !_lifted && ElementSelection.SamePoint(g.X, g.Y, _lastClick.X, _lastClick.Y))
            _level = ElementSelection.Next(_levels.Levels.Count, _level);
        else
        {
            _busy = true;
            try { _levels = await _detector.DetectAsync(g.X, g.Y, snap, frame, CancellationToken.None); }
            finally { _busy = false; }
            _level = 0;
            _lastClick = g;
            if (_levels is null) { _log.LogInformation("Ctrl+클릭 ({X},{Y}) 인식 실패", g.X, g.Y); Deselect(); return; }
        }
        Deselect();
        Select(w, b, frame, _levels, _level);
    }

    private void OnMove(OverlayWindow w, MouseEventArgs e)
    {
        if (!_boards.TryGetValue(w, out var b)) return;
        var p = e.GetPosition(w.Viewport);

        if (_drag != Drag.None && _selWindow == w && _sel is not null)
        {
            e.Handled = true;
            var dx = (int)(p.X - _dragStart.X);
            var dy = (int)(p.Y - _dragStart.Y);
            var next = _drag == Drag.Move
                ? _dragOrigin with { X = _dragOrigin.X + dx, Y = _dragOrigin.Y + dy }
                : Resize(_dragOrigin, _handle, dx, dy);
            if (next == _sel.Bounds) return;
            if (!_lifted) Lift();
            var moved = _sel with { Bounds = next };
            _history.Execute(new ReplaceCommand(_sel.Id, moved));
            _replacesInDrag++;
            _sel = moved;
            Invalidate(w);
            ShowSelection(b, next);
            return;
        }

        if (!Ctrl || _sel is not null) { if (!Ctrl) b.Preview.Visibility = Visibility.Collapsed; return; }
        if (_busy || _mode.Snapshot is not { } snap) return;
        _busy = true;
        _ = HoverAsync(w, b, snap, Global(w, p));
    }

    private async Task HoverAsync(OverlayWindow w, Board b, Core.Windows.WindowSnapshot snap, (int X, int Y) g)
    {
        try
        {
            var el = await _detector.DetectAsync(g.X, g.Y, snap, FrameOf(w), CancellationToken.None);
            Place(w, b.Preview, el?.Bounds);
        }
        finally { _busy = false; }
    }

    private void OnUp(OverlayWindow w, MouseButtonEventArgs e)
    {
        if (_drag == Drag.None || _selWindow != w || _sel is null) return;
        e.Handled = true;
        w.ReleaseMouseCapture();
        _drag = Drag.None;
        // ponytail: 드래그 중 쌓인 Replace를 하나로 합친다 — 전부 Undo 후 시작→끝 Replace 하나만 다시 실행
        if (_replacesInDrag > 1)
        {
            var final = _sel;
            for (var i = 0; i < _replacesInDrag; i++) _history.Undo();
            _history.Execute(new ReplaceCommand(final.Id, final));
        }
        _replacesInDrag = 0;
        Invalidate(w);
    }

    private void Invalidate(OverlayWindow w) => _mode.RendererOf?.Invoke(w)?.InvalidateAll();

    private void OnKey(OverlayWindow w, KeyEventArgs e)
    {
        if (_sel is null || _selWindow != w || !_boards.TryGetValue(w, out var b)) return;
        switch (e.Key)
        {
            case Key.Escape:
                Deselect();
                e.Handled = true;
                break;
            case Key.Delete:
                if (_lifted) _history.Execute(new RemoveCommand(_sel.Id));
                else _history.Execute(new AddCommand(MakePatch())); // 아직 안 뗐으면 구멍 메움만 남긴다
                Deselect();
                Invalidate(w);
                e.Handled = true;
                break;
            case Key.C when Ctrl:
                try
                {
                    if (_sel.Kind == ElementKind.Text && !string.IsNullOrEmpty(_sel.Text)) Clipboard.SetText(_sel.Text);
                    else if (ElementImageCache.Get(_sel.Id) is { } img) Clipboard.SetImage(img);
                }
                catch (Exception ex) { _log.LogWarning(ex, "클립보드 복사 실패"); }
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUp(OverlayWindow w, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl && _boards.TryGetValue(w, out var b)) b.Preview.Visibility = Visibility.Collapsed;
    }

    // ---- 선택·떼어내기 ----

    private void Select(OverlayWindow w, Board b, CapturedFrame? frame, DetectedElement det, int level)
    {
        var lv = det.Levels[level];
        var m = w.Monitor.Bounds;
        var local = Clamp(new RectPx(lv.Bounds.X - m.X, lv.Bounds.Y - m.Y, lv.Bounds.Width, lv.Bounds.Height), m.Width, m.Height);
        if (local.Width < 2 || local.Height < 2 || frame is null) return;

        var crop = Crop(b, frame, local);
        var id = Guid.NewGuid();
        ElementImageCache.Set(id, crop);
        _patchColor = PatchColor.MedianRing(frame.Bgra, frame.Width, frame.Height, frame.Stride, local);
        _sel = new ElementAnnotation
        {
            Id = id, MonitorKey = w.Monitor.Key, Order = AnnotationIds.Next(), Bounds = local,
            Kind = det.Kind, CropPath = SaveCrop(crop, id), SourceRect = local, Text = lv.Text, Source = det.Source,
        };
        _lifted = false;
        _selWindow = w;
        ShowSelection(b, local);
        _log.LogInformation("요소 선택 {Kind} {Source} {Level}/{Levels} {Bounds} '{Text}'", det.Kind, det.Source, level + 1, det.Levels.Count, local, lv.Text);
    }

    /// <summary>처음 움직이는 순간: 요소 + 구멍 메움 패치를 한 명령으로 추가 (Undo 한 번이면 원래 화면).</summary>
    private void Lift()
    {
        _history.Execute(new LiftElementCommand(_sel!, MakePatch()));
        _lifted = true;
    }

    private PatchAnnotation MakePatch() => new()
    {
        Id = Guid.NewGuid(), MonitorKey = _sel!.MonitorKey,
        // 패치는 판서보다 아래, 정지 화면 바로 위: Order를 음수 영역에 둬 ForMonitor 정렬에서 항상 먼저 그려진다
        Order = long.MinValue + AnnotationIds.Next(),
        Bounds = _sel.SourceRect, Area = _sel.SourceRect, FillColor = _patchColor,
    };

    private void Duplicate()
    {
        if (!_lifted) Lift();
        var copy = _sel! with { Id = Guid.NewGuid(), Order = AnnotationIds.Next() };
        ElementImageCache.Set(copy.Id, ElementImageCache.Get(_sel!.Id)!);
        _history.Execute(new AddCommand(copy));
        _sel = copy; // 복제본을 끌고 간다, 원래 자리 메움 없음
    }

    private void BeginDrag(OverlayWindow w, Drag kind, int handle, Point p)
    {
        _drag = kind; _handle = handle; _dragStart = p; _dragOrigin = _sel!.Bounds; _replacesInDrag = 0;
        w.CaptureMouse();
    }

    /// <summary>모서리(0,2,4,6)는 비율 유지, 변(1,3,5,7)은 그 축만.</summary>
    internal static RectPx Resize(RectPx o, int handle, int dx, int dy)
    {
        int x = o.X, y = o.Y, wd = o.Width, ht = o.Height;
        if (handle is 0 or 2 or 4 or 6)
        {
            var sx = handle is 2 or 4 ? dx : -dx;
            var scale = Math.Max(0.05, (o.Width + sx) / (double)o.Width);
            wd = Math.Max(4, (int)Math.Round(o.Width * scale));
            ht = Math.Max(4, (int)Math.Round(o.Height * scale));
            if (handle is 0 or 6) x = o.X + o.Width - wd;
            if (handle is 0 or 2) y = o.Y + o.Height - ht;
        }
        else
        {
            switch (handle)
            {
                case 1: y = o.Y + dy; ht = o.Height - dy; break;
                case 3: wd = o.Width + dx; break;
                case 5: ht = o.Height + dy; break;
                case 7: x = o.X + dx; wd = o.Width - dx; break;
            }
            wd = Math.Max(4, wd); ht = Math.Max(4, ht);
        }
        return new RectPx(x, y, wd, ht);
    }

    // ---- 표시 ----

    private void ShowSelection(Board b, RectPx? r)
    {
        if (r is not { } rect)
        {
            b.Frame.Visibility = Visibility.Collapsed;
            foreach (var h in b.Handles) h.Visibility = Visibility.Collapsed;
            return;
        }
        Canvas.SetLeft(b.Frame, rect.X); Canvas.SetTop(b.Frame, rect.Y);
        b.Frame.Width = rect.Width; b.Frame.Height = rect.Height;
        b.Frame.Visibility = Visibility.Visible;
        var pts = HandlePoints(rect);
        for (var i = 0; i < 8; i++)
        {
            Canvas.SetLeft(b.Handles[i], pts[i].X - 5); Canvas.SetTop(b.Handles[i], pts[i].Y - 5);
            b.Handles[i].Visibility = Visibility.Visible;
        }
    }

    private static Point[] HandlePoints(RectPx r) =>
    [
        new(r.X, r.Y), new(r.X + r.Width / 2.0, r.Y), new(r.X + r.Width, r.Y), new(r.X + r.Width, r.Y + r.Height / 2.0),
        new(r.X + r.Width, r.Y + r.Height), new(r.X + r.Width / 2.0, r.Y + r.Height), new(r.X, r.Y + r.Height), new(r.X, r.Y + r.Height / 2.0),
    ];

    private int HitHandle(Point p)
    {
        if (_sel is null) return -1;
        var pts = HandlePoints(_sel.Bounds);
        for (var i = 0; i < 8; i++)
            if (Math.Abs(pts[i].X - p.X) <= 7 && Math.Abs(pts[i].Y - p.Y) <= 7) return i;
        return -1;
    }

    private static void Place(OverlayWindow w, Rectangle r, RectPx? global)
    {
        if (global is not { } b) { r.Visibility = Visibility.Collapsed; return; }
        Canvas.SetLeft(r, b.X - w.Monitor.Bounds.X);
        Canvas.SetTop(r, b.Y - w.Monitor.Bounds.Y);
        r.Width = b.Width; r.Height = b.Height;
        r.Visibility = Visibility.Visible;
    }

    private static (int X, int Y) Global(OverlayWindow w, Point local) =>
        ((int)local.X + w.Monitor.Bounds.X, (int)local.Y + w.Monitor.Bounds.Y);

    private CapturedFrame? FrameOf(OverlayWindow w) =>
        _mode.Frames is { } f && f.TryGetValue(w.Monitor.Key, out var frame) ? frame : null;

    private static RectPx Clamp(RectPx r, int w, int h)
    {
        var x = Math.Max(0, r.X); var y = Math.Max(0, r.Y);
        return new RectPx(x, y, Math.Min(r.X + r.Width, w) - x, Math.Min(r.Y + r.Height, h) - y);
    }

    private static BitmapSource Crop(Board b, CapturedFrame frame, RectPx r)
    {
        b.Bitmap ??= BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Bgra, frame.Stride);
        var crop = new CroppedBitmap(b.Bitmap, new Int32Rect(r.X, r.Y, r.Width, r.Height));
        crop.Freeze();
        return crop;
    }

    private static string SaveCrop(BitmapSource crop, Guid id)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ScreenAnnotation", "crops");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{id}.png");
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(crop));
        using var fs = File.Create(path);
        enc.Save(fs);
        return path;
    }
}

/// <summary>ElementProbe가 BoardSession을 직접 참조하면 순환이라, 스냅샷·정지 화면만 넘겨 주는 홀더.</summary>
public sealed class ModeHolder
{
    public Core.Windows.WindowSnapshot? Snapshot { get; set; }

    /// <summary>모니터 Key → 정지 화면. BoardSession의 _frames 사전을 그대로 넘긴다.</summary>
    public IReadOnlyDictionary<string, CapturedFrame>? Frames { get; set; }

    /// <summary>창 → 그 창의 렌더러 (요소 이동 후 L2 재합성용). BoardSession이 넣는다.</summary>
    public Func<OverlayWindow, IOverlayRenderer?>? RendererOf { get; set; }
}
