namespace ScreenAnnotation.Core.Session;

/// <summary>모니터 하나의 확대 뷰. screen = content * Scale + Offset. 판서 좌표(content)는 건드리지 않는다.</summary>
public sealed class ViewTransform(int viewportWidth, int viewportHeight)
{
    public const double MinScale = 1, MaxScale = 8, Step = 1.25;

    public double Scale { get; private set; } = 1;
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }
    public bool IsIdentity => Scale == 1;

    /// <summary>커서 아래 지점이 고정되도록 확대·축소.</summary>
    public void ZoomAt(double screenX, double screenY, bool zoomIn)
    {
        var next = Math.Clamp(zoomIn ? Scale * Step : Scale / Step, MinScale, MaxScale);
        if (next == Scale) return;
        var (cx, cy) = ToContent(screenX, screenY);
        Scale = next;
        OffsetX = screenX - cx * Scale;
        OffsetY = screenY - cy * Scale;
        Clamp();
    }

    public void Pan(double dx, double dy)
    {
        OffsetX += dx;
        OffsetY += dy;
        Clamp();
    }

    public void Reset()
    {
        Scale = 1;
        OffsetX = OffsetY = 0;
    }

    public (double X, double Y) ToContent(double screenX, double screenY) => ((screenX - OffsetX) / Scale, (screenY - OffsetY) / Scale);

    public (double X, double Y) ToScreen(double contentX, double contentY) => (contentX * Scale + OffsetX, contentY * Scale + OffsetY);

    // 내용이 뷰포트 밖으로 못 나가게: offset ∈ [viewport - viewport*scale, 0]
    private void Clamp()
    {
        OffsetX = Math.Clamp(OffsetX, viewportWidth - viewportWidth * Scale, 0);
        OffsetY = Math.Clamp(OffsetY, viewportHeight - viewportHeight * Scale, 0);
    }
}
