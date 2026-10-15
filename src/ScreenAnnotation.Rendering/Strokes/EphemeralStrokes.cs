using System.Windows;

namespace ScreenAnnotation.Rendering.Strokes;

/// <summary>사라지는 펜 획 목록. Annotation·Undo 대상이 아니다. 획마다 손을 뗀 시점부터 따로 Hold를 센 뒤 Fade 동안 서서히 사라진다.</summary>
public sealed class EphemeralStrokes
{
    public sealed class Stroke(List<Point> points, string color, double width)
    {
        public List<Point> Points { get; } = points;
        public string Color { get; } = color;
        public double Width { get; } = width;
        public double Opacity { get; internal set; } = 1;
        internal TimeSpan? ExpiresAt { get; set; } // null = 그리는 중(만료 없음)
    }

    private readonly List<Stroke> _strokes = [];

    public TimeSpan Hold { get; set; } = TimeSpan.FromSeconds(1.0);
    public TimeSpan Fade { get; } = TimeSpan.FromSeconds(0.7);
    public IReadOnlyList<Stroke> Strokes => _strokes;

    /// <summary>새 획 시작. 그리는 동안 이 획은 사라지지 않는다 (다른 획은 각자 타이머대로).</summary>
    public Stroke Begin(string color, double width)
    {
        var s = new Stroke([], color, width);
        _strokes.Add(s);
        return s;
    }

    /// <summary>가장 최근 획을 뗀 시점부터 그 획의 Hold를 센다.</summary>
    public void End(TimeSpan now) => End(_strokes.LastOrDefault(s => s.ExpiresAt is null), now);

    public void End(Stroke? stroke, TimeSpan now)
    {
        if (stroke is not null) stroke.ExpiresAt = now + Hold;
    }

    /// <summary>프레임마다 호출. 획별 불투명도 갱신 후 완전히 사라진 획을 지운다. 바뀐 게 있으면 true.</summary>
    public bool Tick(TimeSpan now)
    {
        var changed = false;
        for (var i = _strokes.Count - 1; i >= 0; i--)
        {
            var s = _strokes[i];
            if (s.ExpiresAt is not { } exp) continue;
            var elapsed = now - exp;
            var opacity = elapsed <= TimeSpan.Zero ? 1 : Math.Max(0, 1 - elapsed / Fade);
            if (opacity <= 0) { _strokes.RemoveAt(i); changed = true; continue; }
            if (opacity != s.Opacity) { s.Opacity = opacity; changed = true; }
        }
        return changed;
    }

    public void Clear() => _strokes.Clear();
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
