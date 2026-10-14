using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Capture;

/// <summary>모니터 한 장의 정지 화면. BGRA 32bpp, 상단부터 Stride 바이트씩.</summary>
public sealed record CapturedFrame(MonitorInfo Monitor, int Width, int Height, int Stride, byte[] Bgra, string Method);

public interface ICaptureService
{
    /// <summary>모든 모니터를 병렬로 캡처한다. 실패한 모니터는 결과에서 빠진다.</summary>
    Task<IReadOnlyList<CapturedFrame>> CaptureAllAsync(IReadOnlyList<MonitorInfo> monitors, CancellationToken ct = default);
}
