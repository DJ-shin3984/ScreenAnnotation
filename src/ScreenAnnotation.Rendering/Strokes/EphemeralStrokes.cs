using System.Windows;

namespace ScreenAnnotation.Rendering.Strokes;

/// <summary>사라지는 펜 획 목록. Annotation·Undo 대상이 아니다. 마지막 획을 뗀 시점부터 전부 함께 유지 시간을 센 뒤 0.5초 페이드.</summary>
public sealed class EphemeralStrokes
{
    public sealed class Stroke(List<Point> points, string color, double width)
    {
        public List<Point> Points { get; } = points;
        public string Color { get; } = color;
        public double Width { get; } = width;
        public double Opacity { get; internal set; } = 1;
    }

    private readonly List<Stroke> _strokes = [];
    private TimeSpan? _expiresAt; // null = 그리는 중(만료 없음)

    public TimeSpan Hold { get; set; } = TimeSpan.FromSeconds(2.5);
    public TimeSpan Fade { get; } = TimeSpan.FromSeconds(0.5);
    public IReadOnlyList<Stroke> Strokes => _strokes;

    /// <summary>새 획 시작: 그리는 동안은 아무것도 사라지지 않는다.</summary>
    public Stroke Begin(string color, double width)
    {
        var s = new Stroke([], color, width);
        _strokes.Add(s);
        _expiresAt = null;
        foreach (var x in _strokes) x.Opacity = 1;
        return s;
    }

    /// <summary>획을 뗀 시점부터 모든 획이 한꺼번에 Hold를 센다.</summary>
    public void End(TimeSpan now) => _expiresAt = now + Hold;

    /// <summary>프레임마다 호출. 불투명도 갱신 후 완전히 사라진 획을 지운다. 바뀐 게 있으면 true.</summary>
    public bool Tick(TimeSpan now)
    {
        if (_expiresAt is not { } exp || _strokes.Count == 0) return false;
        var elapsed = now - exp;
        var opacity = elapsed <= TimeSpan.Zero ? 1 : Math.Max(0, 1 - elapsed / Fade);
        if (opacity <= 0)
        {
            _strokes.Clear();
            _expiresAt = null;
            return true;
        }
        foreach (var s in _strokes) s.Opacity = opacity;
        return opacity < 1;
    }

    public void Clear()
    {
        _strokes.Clear();
        _expiresAt = null;
    }
}

/// <summary>레이저 포인터: 획을 남기지 않고 0.3초 꼬리만 보인다.</summary>
public sealed class LaserTrail
{
    public readonly record struct Sample(Point Point, TimeSpan At);

    private readonly List<Sample> _samples = [];

    public TimeSpan Tail { get; } = TimeSpan.FromSeconds(0.3);
    public IReadOnlyList<Sample> Samples => _samples;
    public Point? Head => _samples.Count > 0 ? _samples[^1].Point : null;

    public void Add(Point p, TimeSpan now)
    {
        _samples.Add(new Sample(p, now));
        Prune(now);
    }

    /// <summary>오래된 꼬리를 버린다. 남은 표본이 있으면 true(계속 그려야 함).</summary>
    public bool Prune(TimeSpan now)
    {
        _samples.RemoveAll(s => now - s.At > Tail);
        return _samples.Count > 0;
    }

    public void Clear() => _samples.Clear();
}
