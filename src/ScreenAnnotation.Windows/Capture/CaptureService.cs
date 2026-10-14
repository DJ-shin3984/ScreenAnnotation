using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Diagnostics;
using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Windows.Capture;

/// <summary>모니터별 병렬 캡처. 1순위 DXGI Desktop Duplication, 실패 시 GDI BitBlt.</summary>
// ponytail: Windows.Graphics.Capture 경로는 생략. 실제 장비에서 DXGI가 실패하는 경우(RDP, 일부 드라이버)가 보이면 추가.
public sealed class CaptureService : ICaptureService, IDisposable
{
    private readonly ILogger<CaptureService> _log;
    private readonly Lazy<DxgiDuplicationCapturer?> _dxgi;

    public CaptureService(ILogger<CaptureService> log)
    {
        _log = log;
        _dxgi = new Lazy<DxgiDuplicationCapturer?>(() =>
        {
            try { return new DxgiDuplicationCapturer(log); }
            catch (Exception ex) { log.LogWarning(ex, "DXGI 초기화 실패 — GDI만 사용"); return null; }
        });
    }

    /// <summary>켜기 전에 호출해 두면 DXGI duplication이 미리 만들어져 첫 캡처가 빨라진다.</summary>
    public void Warmup() => _ = _dxgi.Value;

    public async Task<IReadOnlyList<CapturedFrame>> CaptureAllAsync(IReadOnlyList<MonitorInfo> monitors, CancellationToken ct = default)
    {
        using var _ = new PerfScope(_log, $"CaptureAll({monitors.Count})");
        var tasks = monitors.Select(m => Task.Run(() => CaptureOne(m), ct)).ToArray();
        var frames = await Task.WhenAll(tasks).ConfigureAwait(false);
        return frames.Where(f => f is not null).ToArray()!;
    }

    private CapturedFrame? CaptureOne(MonitorInfo m)
    {
        var sw = Stopwatch.StartNew();
        CapturedFrame? frame = null;
        try
        {
            frame = _dxgi.Value?.Capture(m);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "DXGI 캡처 실패 {Device} — GDI로", m.DeviceName);
        }
        try
        {
            frame ??= GdiCapturer.Capture(m);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "GDI 캡처 실패 {Device}", m.DeviceName);
            return null;
        }
        _log.LogInformation("캡처 {Device} {W}x{H} {Method} {Ms:F1}ms", m.DeviceName, frame.Width, frame.Height, frame.Method, sw.Elapsed.TotalMilliseconds);
        return frame;
    }

    public void Dispose()
    {
        if (_dxgi.IsValueCreated) _dxgi.Value?.Dispose();
    }
}
