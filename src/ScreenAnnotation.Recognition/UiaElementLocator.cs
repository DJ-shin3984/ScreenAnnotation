using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Recognition;
using ScreenAnnotation.Core.Windows;
using Interop.UIAutomationClient;

namespace ScreenAnnotation.Recognition;

public sealed class UiaElementLocator(ILogger<UiaElementLocator> logger) : IUiaElementLocator
{
    private const int TimeoutMs = 150;
    private const int MaxDepth = 64;

    public Task<UiaElement?> FindAsync(int x, int y, WindowSnapshot snapshot, CancellationToken ct)
    {
        var window = snapshot.HitTest(x, y);
        if (window is null) return Task.FromResult<UiaElement?>(null);
        return RunAsync("Snapshot+Descend", ct, uia =>
        {
            var root = uia.ElementFromHandle(window.Handle);
            var walker = uia.RawViewWalker;
            IUIAutomationElement? best = null; // 창 전체 크기는 결과로 치지 않는다
            var current = root;
            for (var depth = 0; depth < MaxDepth && current is not null; depth++)
            {
                var rect = ToRect(current.CurrentBoundingRectangle);
                if (depth > 0 && rect.Contains(x, y) && rect != window.Bounds) best = current;
                current = FirstChildContaining(walker, current, x, y);
            }
            return best;
        });
    }

    public Task<UiaElement?> FindFromPointAsync(int x, int y, CancellationToken ct) =>
        RunAsync("ElementFromPoint", ct, uia => uia.ElementFromPoint(new tagPOINT { x = x, y = y }));

    public Task<DetectedElement?> FindDetailedAsync(int x, int y, WindowSnapshot snapshot, CancellationToken ct)
    {
        var window = snapshot.HitTest(x, y);
        if (window is null) return Task.FromResult<DetectedElement?>(null);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeoutMs);
        var work = Task.Run(() => Detail(x, y, window), CancellationToken.None);
        return work.WaitAsync(cts.Token).ContinueWith(t =>
        {
            if (t.IsFaulted) logger.LogDebug(t.Exception, "UIA Detail 실패");
            return t.IsCompletedSuccessfully ? t.Result : null;
        }, TaskScheduler.Default);
    }

    private static DetectedElement? Detail(int x, int y, WindowInfo window)
    {
        var uia = new CUIAutomation8();
        var walker = uia.RawViewWalker;
        var chain = new List<IUIAutomationElement>(); // 루트 제외, 점을 포함하는 경로
        var current = walker.GetFirstChildElement(uia.ElementFromHandle(window.Handle));
        for (var depth = 0; depth < MaxDepth && current is not null; depth++)
        {
            if (!ToRect(current.CurrentBoundingRectangle).Contains(x, y))
            {
                current = walker.GetNextSiblingElement(current);
                continue;
            }
            chain.Add(current);
            current = walker.GetFirstChildElement(current);
        }
        // 검증: 창 전체·화면 전체 크기 요소는 결과로 치지 않는다
        chain.RemoveAll(e => { var r = ToRect(e.CurrentBoundingRectangle); return r.Width <= 0 || r.Height <= 0 || r == window.Bounds || r.Width >= window.Bounds.Width && r.Height >= window.Bounds.Height; });
        if (chain.Count == 0) return null;
        var deepest = chain[^1];
        var bounds = ToRect(deepest.CurrentBoundingRectangle);

        // 텍스트: TextPattern RangeFromPoint → 단어 → 줄 → 문단
        if (deepest.GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId) is IUIAutomationTextPattern tp)
        {
            var range = tp.RangeFromPoint(new tagPOINT { x = x, y = y });
            if (range is not null)
            {
                var levels = new List<ElementLevel>();
                foreach (var unit in new[] { TextUnit.TextUnit_Word, TextUnit.TextUnit_Line, TextUnit.TextUnit_Paragraph })
                {
                    range.ExpandToEnclosingUnit(unit);
                    var r = Union(range.GetBoundingRectangles());
                    if (r is null || !r.Value.Contains(x, y)) continue;
                    var text = range.GetText(-1)?.Trim();
                    if (levels.Count == 0 || levels[^1].Bounds != r.Value) levels.Add(new ElementLevel(r.Value, text));
                }
                if (levels.Count > 0)
                    return new DetectedElement(ElementKind.Text, levels[0].Bounds, levels[0].Text, ElementSource.Uia, levels.Skip(1).ToList());
            }
        }

        // 이미지·기타: 자기 자신 + 감싼 요소들 (큰 순서대로 커지는 것만)
        var ancestors = new List<ElementLevel>();
        var last = bounds;
        for (var i = chain.Count - 2; i >= 0; i--)
        {
            var r = ToRect(chain[i].CurrentBoundingRectangle);
            if (r.Width > last.Width || r.Height > last.Height) { ancestors.Add(new ElementLevel(r, null)); last = r; }
        }
        var kind = deepest.CurrentControlType == 50006 /* Image */ ? ElementKind.Image
                 : deepest.CurrentControlType == 50020 /* Text */ ? ElementKind.Text : ElementKind.Image;
        var name = kind == ElementKind.Text ? deepest.CurrentName : null;
        return new DetectedElement(kind, bounds, string.IsNullOrWhiteSpace(name) ? null : name, ElementSource.Uia, ancestors);
    }

    private static RectPx? Union(Array rects)
    {
        if (rects is not double[] d || d.Length < 4) return null;
        double l = double.MaxValue, t = double.MaxValue, r = double.MinValue, b = double.MinValue;
        for (var i = 0; i + 3 < d.Length; i += 4)
        {
            l = Math.Min(l, d[i]); t = Math.Min(t, d[i + 1]);
            r = Math.Max(r, d[i] + d[i + 2]); b = Math.Max(b, d[i + 1] + d[i + 3]);
        }
        return new RectPx((int)Math.Floor(l), (int)Math.Floor(t), (int)Math.Ceiling(r - l), (int)Math.Ceiling(b - t));
    }

    private Task<UiaElement?> RunAsync(string method, CancellationToken ct, Func<IUIAutomation, IUIAutomationElement?> query)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeoutMs);
        // ponytail: 시간 초과 시 작업은 백그라운드에서 끝까지 돌고 결과만 버린다. 하위 앱이 UIA에 응답 안 하면 스레드 하나가 묶이는 상한.
        var work = Task.Run(() =>
        {
            var element = query(new CUIAutomation8());
            if (element is null) return null;
            var rect = ToRect(element.CurrentBoundingRectangle);
            if (rect.Width <= 0 || rect.Height <= 0) return null;
            return new UiaElement(ControlTypeName(element.CurrentControlType), element.CurrentName ?? "", rect, method, sw.Elapsed.TotalMilliseconds);
        }, CancellationToken.None);
        return work.WaitAsync(cts.Token).ContinueWith(t =>
        {
            var result = t.IsCompletedSuccessfully ? t.Result : null;
            if (t.IsFaulted) logger.LogDebug(t.Exception, "UIA {Method} 실패", method);
            else if (t.IsCanceled) logger.LogDebug("UIA {Method} 시간 초과 {ElapsedMs:F1}ms", method, sw.Elapsed.TotalMilliseconds);
            else logger.LogDebug("UIA {Method} {ControlType} '{Name}' {Bounds} {ElapsedMs:F1}ms",
                method, result?.ControlType, result?.Name, result?.Bounds, result?.ElapsedMs);
            return result;
        }, TaskScheduler.Default);
    }

    private static IUIAutomationElement? FirstChildContaining(IUIAutomationTreeWalker walker, IUIAutomationElement parent, int x, int y)
    {
        for (var child = walker.GetFirstChildElement(parent); child is not null; child = walker.GetNextSiblingElement(child))
            if (ToRect(child.CurrentBoundingRectangle).Contains(x, y)) return child;
        return null;
    }

    private static RectPx ToRect(tagRECT r) => new(r.left, r.top, r.right - r.left, r.bottom - r.top);

    private static string ControlTypeName(int id) => id switch
    {
        50000 => "Button", 50004 => "Edit", 50005 => "Hyperlink", 50006 => "Image", 50007 => "ListItem",
        50008 => "List", 50020 => "Text", 50021 => "ToolBar", 50025 => "Group", 50026 => "Thumb",
        50030 => "Document", 50032 => "Window", 50033 => "Pane", _ => id.ToString(),
    };
}
