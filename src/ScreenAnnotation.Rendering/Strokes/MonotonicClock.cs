namespace ScreenAnnotation.Rendering.Strokes;

/// <summary>사라지는 펜·레이저가 쓰는 단일 시계. 입력(BoardInput)과 렌더러가 반드시 같은 기준을 써야 만료 계산이 맞는다.</summary>
public static class MonotonicClock
{
    public static TimeSpan Now => TimeSpan.FromMilliseconds(Environment.TickCount64);
}
