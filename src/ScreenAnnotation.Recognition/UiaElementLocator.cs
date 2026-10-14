using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
