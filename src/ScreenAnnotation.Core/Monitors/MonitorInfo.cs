namespace ScreenAnnotation.Core.Monitors;

/// <param name="Key">QueryDisplayConfig의 monitorDevicePath (실패 시 GDI 장치 이름).</param>
/// <param name="DeviceName">GDI 장치 이름 (예: \\.\DISPLAY1). DXGI 출력 매칭에 쓴다.</param>
/// <param name="Bounds">가상 화면 기준 물리 픽셀 경계.</param>
public sealed record MonitorInfo(string Key, string DeviceName, RectPx Bounds, RectPx WorkArea, uint DpiX, uint DpiY, bool IsPrimary);

public interface IMonitorService
{
    IReadOnlyList<MonitorInfo> Monitors { get; }
    event EventHandler? MonitorsChanged;
}
