using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Recognition;
using ScreenAnnotation.Core.Windows;

namespace ScreenAnnotation.Recognition;

/// <summary>1차 UIA(검증·범위 확장) → 2차 이미지 분석(정지 화면). 둘 다 실패하면 null.</summary>
public sealed class ElementDetector(IUiaElementLocator uia, ILogger<ElementDetector> log) : IElementDetector
{
    public async Task<DetectedElement?> DetectAsync(int globalX, int globalY, WindowSnapshot snapshot, CapturedFrame? frame, CancellationToken ct)
    {
        var found = await uia.FindDetailedAsync(globalX, globalY, snapshot, ct);
        if (found is not null) return found;
        if (frame is null) return null;

        var m = frame.Monitor.Bounds;
        var lx = globalX - m.X;
        var ly = globalY - m.Y;
        if (!new RectPx(0, 0, frame.Width, frame.Height).Contains(lx, ly)) return null;
        // UI 스레드 금지: 400px 창 플러드필이라 수십 ms 걸릴 수 있다
        var rect = await Task.Run(() => ImageRegionDetector.Detect(frame, lx, ly), ct);
        if (rect is not { } r) return null;
        log.LogDebug("이미지 분석 {Rect}", r);
        return new DetectedElement(ElementKind.Image, new RectPx(r.X + m.X, r.Y + m.Y, r.Width, r.Height), null, ElementSource.ImageAnalysis, []);
    }
}
