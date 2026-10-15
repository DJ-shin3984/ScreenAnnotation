using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Recognition;
using ScreenAnnotation.Rendering;
using ScreenAnnotation.Windows.Overlay;

namespace ScreenAnnotation.App;

/// <summary>T17 PoC: Ctrl을 누르면 커서 아래 UIA 요소에 점선, Ctrl+클릭이면 실선 + 두 방식 소요 시간 로그.</summary>
public sealed class ElementProbe(IUiaElementLocator locator, ModeHolder mode, ILogger<ElementProbe> log)
{
    private readonly Dictionary<OverlayWindow, (Rectangle Preview, Rectangle Pick)> _attached = [];
    private bool _busy;

    public void Attach(OverlayWindow w)
    {
        if (_attached.ContainsKey(w)) return;

        var canvas = new Canvas { IsHitTestVisible = false };
        var preview = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 2, StrokeDashArray = [4, 2], Visibility = Visibility.Collapsed };
        var pick = new Rectangle { Stroke = Brushes.Red, StrokeThickness = 3, Visibility = Visibility.Collapsed };
        canvas.Children.Add(preview);
        canvas.Children.Add(pick);
        w.Viewport.Children.Add(canvas); // 확대 시 사각형도 내용과 함께 움직인다
        _attached[w] = (preview, pick);

        w.PreviewMouseMove += async (_, e) =>
        {
            if (!IsCtrl || _busy) { if (!IsCtrl) preview.Visibility = Visibility.Collapsed; return; }
            var (gx, gy) = Global(w, e.GetPosition(w.Viewport));
            _busy = true;
            try
            {
                var el = mode.Snapshot is { } snap ? await locator.FindAsync(gx, gy, snap, CancellationToken.None) : null;
                Place(w, preview, el);
            }
            finally { _busy = false; }
        };

        w.PreviewMouseLeftButtonDown += async (_, e) =>
        {
            if (!IsCtrl || mode.Snapshot is not { } snap) return;
            e.Handled = true;
            var (gx, gy) = Global(w, e.GetPosition(w.Viewport));

            var a = await locator.FindAsync(gx, gy, snap, CancellationToken.None);
            OverlayNative.SetClickThrough(w.Handle, true);
            UiaElement? b;
            try { b = await locator.FindFromPointAsync(gx, gy, CancellationToken.None); }
            finally { OverlayNative.SetClickThrough(w.Handle, false); }

            log.LogInformation("Ctrl+클릭 ({X},{Y}) Snapshot+Descend: {A} / ElementFromPoint: {B}", gx, gy, Describe(a), Describe(b));
            Place(w, pick, a ?? b);
        };

        w.PreviewKeyUp += (_, e) =>
        {
            if (e.Key is Key.LeftCtrl or Key.RightCtrl) preview.Visibility = Visibility.Collapsed;
        };
    }

    public void Reset()
    {
        foreach (var (preview, pick) in _attached.Values)
        {
            preview.Visibility = Visibility.Collapsed;
            pick.Visibility = Visibility.Collapsed;
        }
    }

    private static bool IsCtrl => Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

    private static (int X, int Y) Global(OverlayWindow w, Point local) =>
        ((int)local.X + w.Monitor.Bounds.X, (int)local.Y + w.Monitor.Bounds.Y);

    private static void Place(OverlayWindow w, Rectangle r, UiaElement? el)
    {
        if (el is null) { r.Visibility = Visibility.Collapsed; return; }
        var b = el.Bounds; // UIA는 전역 물리 픽셀
        Canvas.SetLeft(r, b.X - w.Monitor.Bounds.X);
        Canvas.SetTop(r, b.Y - w.Monitor.Bounds.Y);
        r.Width = b.Width;
        r.Height = b.Height;
        r.Visibility = Visibility.Visible;
    }

    private static string Describe(UiaElement? el) =>
        el is null ? "없음" : $"{el.ControlType} '{el.Name}' {el.Bounds} {el.ElapsedMs:F1}ms";
}

/// <summary>ElementProbe가 BoardSession을 직접 참조하면 순환이라, 스냅샷만 넘겨 주는 작은 홀더.</summary>
public sealed class ModeHolder
{
    public Core.Windows.WindowSnapshot? Snapshot { get; set; }
}
