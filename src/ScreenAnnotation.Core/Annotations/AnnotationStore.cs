using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Annotations;

/// <summary>보드의 판서 객체 저장소. 변경은 IAnnotationCommand를 통해서만 (mutation은 internal).</summary>
public sealed class AnnotationStore
{
    private readonly Dictionary<Guid, Annotation> _byId = new();

    /// <summary>(monitorKey, dirty) — 변경된 영역.</summary>
    public event Action<string, RectPx>? Changed;

    public int Count => _byId.Count;

    // ponytail: 매 호출 정렬 O(n log n). 렌더러가 프레임마다 부르면 캐시 추가.
    public IReadOnlyList<Annotation> ForMonitor(string key) =>
        _byId.Values.Where(a => a.MonitorKey == key).OrderBy(a => a.Order).ToList();

    public Annotation? Find(Guid id) => _byId.GetValueOrDefault(id);

    internal void Add(Annotation a)
    {
        _byId[a.Id] = a;
        Changed?.Invoke(a.MonitorKey, a.Bounds);
    }

    internal Annotation? Remove(Guid id)
    {
        if (!_byId.Remove(id, out var a)) return null;
        Changed?.Invoke(a.MonitorKey, a.Bounds);
        return a;
    }

    /// <summary>oldId를 replacement로 바꾼다. replacement.Order가 0이면 원래 Order를 유지한다. 돌려주는 값은 원래 객체.</summary>
    internal Annotation? Replace(Guid oldId, Annotation replacement)
    {
        if (!_byId.Remove(oldId, out var old)) return null;
        if (replacement.Order == 0) replacement = replacement with { Order = old.Order };
        _byId[replacement.Id] = replacement;
        Changed?.Invoke(old.MonitorKey, Union(old.Bounds, replacement.Bounds));
        return old;
    }

    internal IReadOnlyList<Annotation> Clear()
    {
        var removed = _byId.Values.ToList();
        _byId.Clear();
        foreach (var g in removed.GroupBy(a => a.MonitorKey))
            Changed?.Invoke(g.Key, g.Select(a => a.Bounds).Aggregate(Union));
        return removed;
    }

    public static RectPx Union(RectPx a, RectPx b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new RectPx(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }
}
