using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Recognition.Dataset;

/// <summary>녹화된 UIA 트리 노드 (T43). 좌표는 전역 물리 픽셀.</summary>
public sealed class RecordedNode
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public List<RecordedNode> Children { get; set; } = [];

    [JsonIgnore] public RectPx Bounds => new(X, Y, W, H);
}

public sealed class RecordedClick
{
    public int X { get; set; }
    public int Y { get; set; }
    public RecordedNode Expected { get; set; } = new(); // X,Y,W,H만 사용
}

public sealed class RecordedScreen
{
    public string Name { get; set; } = "";
    public string Png { get; set; } = "";
    public List<RecordedNode> Windows { get; set; } = []; // 창마다 루트 하나, Z 순서
    public List<RecordedClick> Clicks { get; set; } = [];

    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static RecordedScreen Load(string path) => JsonSerializer.Deserialize<RecordedScreen>(File.ReadAllText(path), Json)!;
}

/// <summary>녹화된 트리에서 점을 포함하는 가장 깊은 노드 (창 전체 크기는 제외). 오프라인 재생용.</summary>
public static class RecordedTreeLocator
{
    public static RectPx? Deepest(RecordedScreen screen, int x, int y)
    {
        foreach (var root in screen.Windows)
        {
            if (!root.Bounds.Contains(x, y)) continue;
            RectPx? best = null;
            var node = root;
            while (node is not null)
            {
                if (node != root && node.Bounds != root.Bounds && node.W > 0 && node.H > 0) best = node.Bounds;
                // 점을 포함하는 자식 중 가장 작은 것으로 내려간다 (창 전체 크기 Pane이 먼저 와도 건너뛴다)
                node = node.Children.Where(c => c.Bounds.Contains(x, y)).MinBy(c => (long)c.W * c.H);
            }
            return best;
        }
        return null;
    }

    public static double IoU(RectPx a, RectPx b)
    {
        var ix = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X));
        var iy = Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
        double inter = ix * iy;
        var union = (double)a.Width * a.Height + (double)b.Width * b.Height - inter;
        return union <= 0 ? 0 : inter / union;
    }
}
