namespace ScreenAnnotation.Core.Tools;

public enum Tool { Pen, Highlighter, Shape, Eraser }
public enum PenVariant { Normal, Ephemeral, Laser }
public enum HighlighterVariant { Free, Straight }

/// <summary>숫자 1~9 = 팔레트 순서.</summary>
public static class Palette
{
    public static readonly IReadOnlyList<string> Colors =
        ["#FF3B30", "#FF9500", "#FFCC00", "#34C759", "#007AFF", "#3F51B5", "#AF52DE", "#FFFFFF", "#000000"];

    public static string ByNumber(int number) => Colors[number - 1];
}

/// <summary>휠 한 칸 = 한 단계. 얇을 땐 촘촘, 두꺼울 땐 성기게.</summary>
public static class WidthSteps
{
    public static readonly IReadOnlyList<int> Pen = [1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 40];
    public static readonly IReadOnlyList<int> Highlighter = [8, 12, 16, 24, 32, 48, 64];
    public static readonly IReadOnlyList<int> Shape = [1, 2, 3, 4, 6, 8, 12, 16];

    public static IReadOnlyList<int> For(Tool tool) => tool switch
    {
        Tool.Highlighter => Highlighter,
        Tool.Shape => Shape,
        _ => Pen, // Eraser는 펜 단계표(커서 원 크기)
    };

    /// <summary>설정값에 가장 가까운 단계의 인덱스.</summary>
    public static int NearestIndex(IReadOnlyList<int> steps, int width)
    {
        var best = 0;
        for (var i = 1; i < steps.Count; i++)
            if (Math.Abs(steps[i] - width) < Math.Abs(steps[best] - width)) best = i;
        return best;
    }
}
