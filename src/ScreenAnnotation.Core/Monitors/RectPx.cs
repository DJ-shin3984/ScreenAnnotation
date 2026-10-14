namespace ScreenAnnotation.Core.Monitors;

/// <summary>물리 픽셀 사각형. 판서 좌표의 기준 단위.</summary>
public readonly record struct RectPx(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
}

public readonly record struct PointPx(int X, int Y);

public readonly record struct PointDip(double X, double Y);
