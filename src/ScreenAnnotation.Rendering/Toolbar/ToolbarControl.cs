using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Tools;
using ScreenAnnotation.Rendering.Radial;

namespace ScreenAnnotation.Rendering.Toolbar;

public enum ToolbarAction
{
    Pen, EphemeralPen, Laser, Highlighter, StraightHighlighter, Shape, Text, Eraser,
    WidthDown, WidthUp, Undo, Redo, Save, Copy, ZoomReset, Close,
}

/// <summary>화면 우측 세로 툴바 (L4, 화면 픽셀). 호스트가 Action/ColorPicked/ShapePicked를 받아 처리한다.</summary>
public sealed class ToolbarControl : UserControl
{
    public static readonly double EdgeMargin = 12;

    private static readonly Brush PanelBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xCC, 0x20, 0x20, 0x20)));
    private static readonly Brush HoverBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush SelectedBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x7A, 0xFF))); // 선택 = 파란 채움 (항상 보임)
    private static readonly Brush SelectedHoverBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x95, 0xFF)));
    private static readonly HashSet<Border> Selected = [];
    private static readonly Brush CloseHoverBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xAA, 0xE0, 0x30, 0x30)));

    private readonly Dictionary<ToolbarAction, Border> _buttons = [];
    private readonly List<(Border Dot, Ellipse Ring)> _colors = [];
    private readonly StackPanel _shapeFlyout = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    private readonly TextBlock _widthLabel = new() { Foreground = Brushes.White, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };

    public event Action<ToolbarAction>? Action;
    public event Action<int>? ColorPicked;
    public event Action<ShapeKind>? ShapePicked;

    internal ToolbarAction? SelectedAction { get; private set; }
    internal int SelectedColor { get; private set; }

    public double PreferredHeight => DesiredSize.Height;

    public ToolbarControl()
    {
        // ToolTipService 속성은 트리를 따라 상속된다: 모든 버튼에 150ms 지연·10초 표시
        ToolTipService.SetInitialShowDelay(this, 150);
        ToolTipService.SetShowDuration(this, 10000);
        ToolTipService.SetBetweenShowDelay(this, 50);
        Focusable = false;
        var column = new StackPanel { Orientation = Orientation.Vertical };

        foreach (var (a, tip, icon) in ToolButtons) column.Children.Add(Button(a, tip, icon));
        column.Children.Add(Separator());

        var colors = new WrapPanel { Width = 2 * 24, HorizontalAlignment = HorizontalAlignment.Center };
        for (var i = 0; i < RadialItems.ColorKeys.Count; i++) colors.Children.Add(ColorDot(i + 1));
        column.Children.Add(colors);
        column.Children.Add(Separator());

        column.Children.Add(Button(ToolbarAction.WidthUp, "굵게 (휠 위)", "M4,12 H20 M12,4 V20"));
        column.Children.Add(_widthLabel);
        column.Children.Add(Button(ToolbarAction.WidthDown, "가늘게 (휠 아래)", "M4,12 H20"));
        column.Children.Add(Separator());

        column.Children.Add(Button(ToolbarAction.Undo, "Undo (Ctrl+Z)", "M9,6 L4,11 L9,16 M4,11 H15 A5,5 0 0 1 15,21 H10"));
        column.Children.Add(Button(ToolbarAction.Redo, "Redo (Ctrl+Y)", "M15,6 L20,11 L15,16 M20,11 H9 A5,5 0 0 0 9,21 H14"));
        column.Children.Add(Separator());

        column.Children.Add(Button(ToolbarAction.Save, "저장 (Ctrl+S)", "M12,3 V15 M7,10 L12,15 L17,10 M4,19 H20"));
        column.Children.Add(Button(ToolbarAction.Copy, "복사 (Ctrl+C)", "M8,8 H20 V20 H8 Z M4,16 V4 H16"));
        column.Children.Add(Button(ToolbarAction.ZoomReset, "확대 원래대로 (Ctrl+0)", "M6,8 L9,5 V19 M13,5 L19,19 M19,5 L13,19"));
        column.Children.Add(Separator());

        var close = Button(ToolbarAction.Close, "끄기 (Ctrl+Shift+Space)", "M5,5 L19,19 M19,5 L5,19");
        close.MouseEnter += (_, _) => close.Background = CloseHoverBrush;
        column.Children.Add(close);

        foreach (var item in RadialItems.Shapes())
        {
            var kind = Enum.Parse<ShapeKind>(item.Key);
            var b = IconButton(item.Icon!, $"도형: {item.Key}");
            b.MouseLeftButtonUp += (_, e) => { e.Handled = true; _shapeFlyout.Visibility = Visibility.Collapsed; ShapePicked?.Invoke(kind); };
            _shapeFlyout.Children.Add(b);
        }
        var flyoutPanel = new Border { Background = PanelBrush, CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Child = _shapeFlyout, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6 + 5 * 40, 4, 0) };
        flyoutPanel.SetBinding(VisibilityProperty, new System.Windows.Data.Binding(nameof(Visibility)) { Source = _shapeFlyout });

        var panel = new Border { Background = PanelBrush, CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Child = column };
        var root = new StackPanel { Orientation = Orientation.Horizontal };
        root.Children.Add(flyoutPanel);
        root.Children.Add(panel);
        Content = root;

        // Preview 이벤트를 여기서 Handled 처리하면 자식 버튼의 MouseLeftButtonUp이 아예 안 올라온다.
        // 호스트(BoardInput)가 IsMouseOver로 툴바 위 클릭을 건너뛴다.
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
    }

    /// <summary>ToolState 변경 시 호스트가 호출: 선택 강조·굵기 표시.</summary>
    public void Sync(ToolState tools)
    {
        SelectedAction = tools.Tool switch
        {
            Tool.Pen => tools.PenVariant switch { PenVariant.Ephemeral => ToolbarAction.EphemeralPen, PenVariant.Laser => ToolbarAction.Laser, _ => ToolbarAction.Pen },
            Tool.Highlighter => tools.HighlighterVariant == HighlighterVariant.Straight ? ToolbarAction.StraightHighlighter : ToolbarAction.Highlighter,
            Tool.Shape => ToolbarAction.Shape,
            _ => ToolbarAction.Eraser,
        };
        foreach (var (a, b) in _buttons) SetSelected(b, a == SelectedAction);

        SelectedColor = RadialItems.ColorKeys.ToList().IndexOf(tools.Color) + 1;
        for (var i = 0; i < _colors.Count; i++) _colors[i].Ring.Visibility = i + 1 == SelectedColor ? Visibility.Visible : Visibility.Collapsed;

        _widthLabel.Text = $"{tools.Width}px";
    }

    /// <summary>도구·변형·굵기 액션을 ToolState에 적용. 나머지(Text/Undo/Save…)는 호스트가 처리.</summary>
    public static void Apply(ToolState tools, ToolbarAction a)
    {
        switch (a)
        {
            case ToolbarAction.Pen: tools.SetPenVariant(PenVariant.Normal); break;
            case ToolbarAction.EphemeralPen: tools.SetPenVariant(PenVariant.Ephemeral); break;
            case ToolbarAction.Laser: tools.SetPenVariant(PenVariant.Laser); break;
            case ToolbarAction.Highlighter: tools.SetHighlighterVariant(HighlighterVariant.Free); break;
            case ToolbarAction.StraightHighlighter: tools.SetHighlighterVariant(HighlighterVariant.Straight); break;
            case ToolbarAction.Shape: tools.SetTool(Tool.Shape); break;
            case ToolbarAction.Eraser: if (tools.Tool != Tool.Eraser) tools.ToggleEraser(); break;
            case ToolbarAction.WidthDown: tools.Wheel(-120); break;
            case ToolbarAction.WidthUp: tools.Wheel(120); break;
        }
    }

    internal void Click(ToolbarAction a)
    {
        if (a == ToolbarAction.Shape) _shapeFlyout.Visibility = _shapeFlyout.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        else _shapeFlyout.Visibility = Visibility.Collapsed;
        Action?.Invoke(a);
    }

    internal void ClickColor(int number) => ColorPicked?.Invoke(number);

    private static readonly (ToolbarAction, string, string)[] ToolButtons =
    [
        (ToolbarAction.Pen, "펜 (Tab)", "M4,20 L6,14 L16,4 L20,8 L10,18 Z M14,6 L18,10"),
        (ToolbarAction.EphemeralPen, "사라지는 펜 (Shift+Tab)", "M4,20 L6,14 L14,6 L18,10 L10,18 Z M19,15 A3,3 0 1 0 19,21 A3,3 0 1 0 19,15 M19,16.5 V18 H20.5"),
        (ToolbarAction.Laser, "레이저 포인터 (Shift+Tab)", "M12,9 A3,3 0 1 0 12,15 A3,3 0 1 0 12,9 M12,3 V6 M12,18 V21 M3,12 H6 M18,12 H21 M5.6,5.6 L7.8,7.8 M16.2,16.2 L18.4,18.4 M5.6,18.4 L7.8,16.2 M16.2,7.8 L18.4,5.6"),
        (ToolbarAction.Highlighter, "형광펜 (Tab)", "M6,19 L4,15 L14,5 L19,10 L9,20 Z M4,21 H12"),
        (ToolbarAction.StraightHighlighter, "직선 형광펜 (Shift+Tab)", "M8,14 L6,10 L14,2 L18,6 L10,14 Z M3,20 H21"),
        (ToolbarAction.Shape, "도형 (Tab)", "M4,4 H20 V20 H4 Z"),
        (ToolbarAction.Text, "텍스트 (T)", "M5,5 H19 M12,5 V19 M9,19 H15"),
        (ToolbarAction.Eraser, "지우개 (E)", "M3,15 L12,6 L19,13 L13,19 H8 Z M8,19 L5,16 M20,19 H13"),
    ];

    private Border Button(ToolbarAction a, string tip, string path)
    {
        var b = IconButton(Geometry.Parse(path), tip);
        b.MouseLeftButtonUp += (_, e) => { e.Handled = true; Click(a); };
        _buttons[a] = b;
        return b;
    }

    private static Border IconButton(Geometry icon, string tip)
    {
        var b = new Border
        {
            Width = 36, Height = 36, Margin = new Thickness(0, 2, 0, 2), CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = Tip(tip), Focusable = false,
            Child = new Path { Data = icon, Stroke = Brushes.White, StrokeThickness = 2, Width = 24, Height = 24, Stretch = Stretch.None, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false },
        };
        // 선택 상태는 별도 집합으로 기억: 호버 중 선택이 바뀌어도 떠날 때 올바른 배경으로 돌아간다
        b.MouseEnter += (_, _) => b.Background = Selected.Contains(b) ? SelectedHoverBrush : HoverBrush;
        b.MouseLeave += (_, _) => b.Background = Selected.Contains(b) ? SelectedBrush : Brushes.Transparent;
        return b;
    }

    private static void SetSelected(Border b, bool on)
    {
        if (on) Selected.Add(b); else Selected.Remove(b);
        b.Background = on ? (b.IsMouseOver ? SelectedHoverBrush : SelectedBrush) : (b.IsMouseOver ? HoverBrush : Brushes.Transparent);
    }

    /// <summary>텍스트 모드(ToolState 밖의 상태)를 활성으로 표시. 끄면 Sync가 준 선택으로 돌아간다.</summary>
    public void SetTextMode(bool on, ToolState tools)
    {
        if (on) { SelectedAction = ToolbarAction.Text; foreach (var (a, b) in _buttons) SetSelected(b, a == ToolbarAction.Text); }
        else Sync(tools);
    }

    private Border ColorDot(int number)
    {
        var hex = RadialItems.ColorKeys[number - 1];
        var ring = new Ellipse { Width = 20, Height = 20, Stroke = Brushes.White, StrokeThickness = 2, Visibility = Visibility.Collapsed };
        var grid = new Grid { Width = 20, Height = 20 };
        grid.Children.Add(new Ellipse { Width = 16, Height = 16, Fill = ColorHex.Brush(hex), Stroke = Brushes.Gray, StrokeThickness = 0.5 });
        grid.Children.Add(ring);
        var b = new Border { Width = 24, Height = 24, Child = grid, Cursor = Cursors.Hand, ToolTip = Tip($"색 {number} (키 {number})"), Focusable = false, Background = Brushes.Transparent };
        b.MouseLeftButtonUp += (_, e) => { e.Handled = true; ClickColor(number); };
        _colors.Add((b, ring));
        return b;
    }

    private static Rectangle Separator() => new() { Height = 1, Fill = Brushes.Gray, Opacity = 0.6, Margin = new Thickness(4, 4, 4, 4) };

    private static Brush Freeze(Brush b) { b.Freeze(); return b; }

    /// <summary>호버 즉시(150ms) 뜨는 큼직한 툴팁. 오버레이가 Topmost라 기본 지연이면 눈에 안 띈다.</summary>
    private static ToolTip Tip(string text)
    {
        var t = new ToolTip
        {
            Content = text, FontSize = 14, Padding = new Thickness(10, 6, 10, 6),
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x20, 0x20, 0x20)), Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)), Placement = System.Windows.Controls.Primitives.PlacementMode.Left,
            HorizontalOffset = -8,
        };
        return t;
    }
}
