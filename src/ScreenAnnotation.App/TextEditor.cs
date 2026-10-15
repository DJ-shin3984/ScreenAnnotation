using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using FlowDirection = System.Windows.FlowDirection;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Rendering;

namespace ScreenAnnotation.App;

/// <summary>T31: 오버레이 위 텍스트 입력창. 좌표는 모니터 로컬 물리 픽셀(window.Root 기준).</summary>
public sealed class TextEditor
{
    private const string FontName = "Segoe UI";

    private readonly OverlayWindow _window;
    private readonly AnnotationStore _store;
    private readonly HistoryManager _history;
    private readonly Action<RectPx> _invalidate;
    private readonly Canvas _layer = new() { IsHitTestVisible = false };
    private readonly TextBox _box;
    private TextAnnotation? _original; // 재편집 중이면 원본

    public TextEditor(OverlayWindow window, AnnotationStore store, HistoryManager history, Action<RectPx> invalidate)
    {
        _window = window;
        _store = store;
        _history = history;
        _invalidate = invalidate;

        _box = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            BorderBrush = null,
            Padding = new Thickness(0),
            FontFamily = new FontFamily(FontName),
            AcceptsReturn = true,
            AcceptsTab = false,
            TextWrapping = TextWrapping.NoWrap,
            MinWidth = 40,
            Visibility = Visibility.Collapsed,
            CaretBrush = Brushes.White,
        };
        _box.PreviewKeyDown += OnKeyDown;
        _layer.Children.Add(_box);
        _window.Root.Children.Add(_layer);
    }

    public bool IsEditing => _box.Visibility == Visibility.Visible;

    /// <summary>닫힌 뒤(확정·취소 모두) 호스트가 도구를 펜으로 되돌린다.</summary>
    public event Action? Closed;

    /// <summary>테스트용.</summary>
    internal TextBox Box => _box;

    /// <summary>굵기 단계에 비례한 글자 크기.</summary>
    public static double FontSizeFor(int width) => Math.Clamp(12 + width * 2, 12, 96);

    public void Begin(Point contentPx, string colorHex, double fontSizePx)
    {
        _original = null;
        Open(contentPx, colorHex, fontSizePx, "");
    }

    /// <summary>그 지점에 기존 텍스트가 있으면 재편집으로 연다.</summary>
    public bool TryBeginEdit(Point contentPx, out TextAnnotation? existing)
    {
        existing = _store.ForMonitor(_window.Monitor.Key)
            .OfType<TextAnnotation>()
            .LastOrDefault(t => t.Bounds.Contains((int)contentPx.X, (int)contentPx.Y));
        if (existing is null) return false;

        _original = existing;
        Open(new Point(existing.Position.X, existing.Position.Y), existing.Color, existing.FontSize, existing.Text);
        _box.CaretIndex = _box.Text.Length;
        return true;
    }

    /// <summary>입력창 밖 클릭 판정용.</summary>
    public bool Contains(Point rootPx)
    {
        if (!IsEditing) return false;
        var x = Canvas.GetLeft(_box);
        var y = Canvas.GetTop(_box);
        return rootPx.X >= x && rootPx.X <= x + _box.ActualWidth && rootPx.Y >= y && rootPx.Y <= y + _box.ActualHeight;
    }

    public void Commit()
    {
        if (!IsEditing) return;

        // 한글 조합 중인 글자는 포커스를 잃을 때 IME가 확정한다. 그래서 Text를 읽기 전에 포커스를 먼저 옮긴다.
        Keyboard.Focus(_window);
        var text = _box.Text;

        if (text.Length > 0)
        {
            var color = ColorOf(_box.Foreground);
            var fontSize = (float)_box.FontSize;
            var pos = _original?.Position ?? new PointPx((int)Canvas.GetLeft(_box), (int)Canvas.GetTop(_box));
            var bounds = Measure(text, fontSize, pos);

            if (_original is { } o)
            {
                if (text != o.Text)
                {
                    var replacement = o with { Id = Guid.NewGuid(), Text = text, Bounds = bounds, Order = 0 };
                    _history.Execute(new ReplaceCommand(o.Id, replacement));
                    _invalidate(AnnotationStore.Union(o.Bounds, bounds));
                }
            }
            else
            {
                var a = new TextAnnotation
                {
                    Id = Guid.NewGuid(),
                    MonitorKey = _window.Monitor.Key,
                    Order = AnnotationIds.Next(),
                    Bounds = bounds,
                    Text = text,
                    Position = pos,
                    Color = color,
                    FontSize = fontSize,
                };
                _history.Execute(new AddCommand(a));
                _invalidate(bounds);
            }
        }
        Close();
    }

    public void Cancel()
    {
        if (!IsEditing) return;
        Keyboard.Focus(_window);
        Close();
    }

    private void Open(Point at, string colorHex, double fontSize, string text)
    {
        Canvas.SetLeft(_box, at.X);
        Canvas.SetTop(_box, at.Y);
        _box.Foreground = ColorHex.Brush(colorHex);
        _box.FontSize = fontSize;
        _box.Text = text;
        _box.Visibility = Visibility.Visible;
        _layer.IsHitTestVisible = true;
        _box.Focus();
        Keyboard.Focus(_box);
        _box.CaretIndex = text.Length;
    }

    private void Close()
    {
        _box.Visibility = Visibility.Collapsed;
        _layer.IsHitTestVisible = false;
        _box.Text = "";
        _original = null;
        Closed?.Invoke();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                e.Handled = true;
                Commit();
                break;
            case Key.Escape:
                e.Handled = true;
                if (_box.Text.Length == 0) Cancel(); else Commit();
                break;
            case Key.Tab:
                e.Handled = true; // 도구 전환 막기
                break;
        }
    }

    private static RectPx Measure(string text, float fontSize, PointPx pos)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontName), fontSize, Brushes.Black, 1.0);
        return new RectPx(pos.X, pos.Y, (int)Math.Ceiling(ft.WidthIncludingTrailingWhitespace) + 1, (int)Math.Ceiling(ft.Height) + 1);
    }

    private static string ColorOf(Brush brush) =>
        brush is SolidColorBrush s ? $"#{s.Color.R:X2}{s.Color.G:X2}{s.Color.B:X2}" : "#FF3B30";
}
