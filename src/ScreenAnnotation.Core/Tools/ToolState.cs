using ScreenAnnotation.Core.Annotations;

namespace ScreenAnnotation.Core.Tools;

/// <summary>현재 도구·변형·색·굵기. 도구별로 마지막 색·굵기·변형을 기억한다. UI 스레드 전용.</summary>
public sealed class ToolState
{
    private readonly Dictionary<Tool, string> _color = new();
    private readonly Dictionary<Tool, int> _widthIndex = new();
    private Tool _beforeEraser = Tool.Pen;
    private int _wheelAccum;

    public ToolState(Settings.Settings settings)
    {
        ApplyDefaults(settings);
        _color[Tool.Eraser] = "#FFFFFF";
        _widthIndex[Tool.Eraser] = WidthSteps.NearestIndex(WidthSteps.Pen, 16);
    }

    /// <summary>설정 화면에서 바꾼 도구별 기본 색·굵기를 바로 반영한다.</summary>
    public void ApplyDefaults(Settings.Settings settings)
    {
        foreach (var tool in new[] { Tool.Pen, Tool.Highlighter, Tool.Shape })
        {
            var d = settings.ToolDefaults.TryGetValue(tool.ToString(), out var v) ? v : new Settings.ToolDefault("#FF3B30", 3);
            _color[tool] = d.Color;
            _widthIndex[tool] = WidthSteps.NearestIndex(WidthSteps.For(tool), d.Width);
        }
        Changed?.Invoke();
    }

    public Tool Tool { get; private set; } = Tool.Pen;
    public PenVariant PenVariant { get; private set; }
    public HighlighterVariant HighlighterVariant { get; private set; }
    public ShapeKind ShapeKind { get; private set; }

    public string Color => _color[Tool];
    public int Width => WidthSteps.For(Tool)[_widthIndex[Tool]];

    public event Action? Changed;

    /// <summary>Tab: 펜 → 형광펜 → 도형 → 펜. 지우개에서는 펜으로.</summary>
    public void NextTool() => Set(Tool switch
    {
        Tool.Pen => Tool.Highlighter,
        Tool.Highlighter => Tool.Shape,
        _ => Tool.Pen,
    });

    /// <summary>Shift+Tab: 현재 도구의 변형 순환.</summary>
    public void NextVariant()
    {
        switch (Tool)
        {
            case Tool.Pen: PenVariant = Next(PenVariant); break;
            case Tool.Highlighter: HighlighterVariant = Next(HighlighterVariant); break;
            case Tool.Shape: ShapeKind = Next(ShapeKind); break;
            default: return;
        }
        Changed?.Invoke();
    }

    /// <summary>Esc: 펜/일반 펜. 색·굵기·다른 도구의 변형은 유지.</summary>
    public void Reset()
    {
        PenVariant = PenVariant.Normal;
        Set(Tool.Pen);
    }

    /// <summary>E: 지우개 토글. 다시 E면 직전 도구.</summary>
    public void ToggleEraser()
    {
        if (Tool == Tool.Eraser) { Set(_beforeEraser); return; }
        _beforeEraser = Tool;
        Set(Tool.Eraser);
    }

    /// <summary>숫자 1~9. 지우개에서는 무시.</summary>
    public void SetColor(int number)
    {
        if (Tool == Tool.Eraser || number is < 1 or > 9) return;
        _color[Tool] = Palette.ByNumber(number);
        Changed?.Invoke();
    }

    public void SetShape(ShapeKind kind)
    {
        ShapeKind = kind;
        Changed?.Invoke();
    }

    /// <summary>툴바 클릭. 지우개로 가면 직전 도구를 기억한다.</summary>
    public void SetTool(Tool tool)
    {
        if (tool == Tool.Eraser && Tool != Tool.Eraser) _beforeEraser = Tool;
        Set(tool);
    }

    public void SetPenVariant(PenVariant variant)
    {
        PenVariant = variant;
        SetTool(Tool.Pen);
    }

    public void SetHighlighterVariant(HighlighterVariant variant)
    {
        HighlighterVariant = variant;
        SetTool(Tool.Highlighter);
    }

    /// <summary>휠 델타 누적, ±120마다 한 단계. 굵기가 바뀌면 true.</summary>
    public bool Wheel(int delta)
    {
        _wheelAccum += delta;
        var steps = _wheelAccum / 120;
        if (steps == 0) return false;
        _wheelAccum -= steps * 120;

        var table = WidthSteps.For(Tool);
        var next = Math.Clamp(_widthIndex[Tool] + steps, 0, table.Count - 1);
        if (next == _widthIndex[Tool]) return false;
        _widthIndex[Tool] = next;
        Changed?.Invoke();
        return true;
    }

    private void Set(Tool tool)
    {
        Tool = tool;
        _wheelAccum = 0;
        Changed?.Invoke();
    }

    private static T Next<T>(T value) where T : struct, Enum
    {
        var all = Enum.GetValues<T>();
        return all[(Array.IndexOf(all, value) + 1) % all.Length];
    }
}
