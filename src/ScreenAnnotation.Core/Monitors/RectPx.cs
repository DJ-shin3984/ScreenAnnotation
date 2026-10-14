namespace ScreenAnnotation.Core.Monitors;

/// <summary>물리 픽셀 사각형.</summary>
public readonly record struct RectPx(int X, int Y, int Width, int Height)
{
    public bool Contains(int x, int y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}
