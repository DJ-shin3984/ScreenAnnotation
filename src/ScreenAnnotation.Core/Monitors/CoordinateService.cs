namespace ScreenAnnotation.Core.Monitors;

/// <summary>전역 픽셀 ↔ 모니터 로컬 픽셀 ↔ DIP. 판서는 모니터 로컬 픽셀로 저장하고 DIP는 표시 직전에만 쓴다.</summary>
public static class CoordinateService
{
    public static PointPx GlobalToLocal(MonitorInfo m, PointPx p) => new(p.X - m.Bounds.X, p.Y - m.Bounds.Y);

    public static PointPx LocalToGlobal(MonitorInfo m, PointPx p) => new(p.X + m.Bounds.X, p.Y + m.Bounds.Y);

    public static PointDip LocalPxToDip(MonitorInfo m, PointPx p) => new(p.X * 96.0 / m.DpiX, p.Y * 96.0 / m.DpiY);

    public static PointPx DipToLocalPx(MonitorInfo m, PointDip p) =>
        new((int)Math.Round(p.X * m.DpiX / 96.0), (int)Math.Round(p.Y * m.DpiY / 96.0));

    public static MonitorInfo? FindMonitor(IEnumerable<MonitorInfo> monitors, PointPx global) =>
        monitors.FirstOrDefault(m => m.Bounds.Contains(global.X, global.Y));
}
