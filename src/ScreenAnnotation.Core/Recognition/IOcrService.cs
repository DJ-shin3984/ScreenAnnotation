using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;

namespace ScreenAnnotation.Core.Recognition;

/// <summary>OCR 단어. Bounds는 프레임(모니터) 로컬 물리 픽셀.</summary>
public sealed record OcrWord(string Text, RectPx Bounds);

public sealed record OcrLine(string Text, RectPx Bounds, IReadOnlyList<OcrWord> Words);

public sealed record OcrResult(IReadOnlyList<OcrLine> Lines)
{
    public static readonly OcrResult Empty = new([]);

    public string Text => string.Join("\n", Lines.Select(l => l.Text));
}

/// <summary>Ctrl+클릭 텍스트 인식의 2차 경로·Ctrl+드래그 영역 OCR. Windows 내장 엔진만 쓴다.</summary>
public interface IOcrService
{
    bool IsAvailable { get; }

    /// <summary>IsAvailable이 false인 이유 (언어팩 설치 안내).</summary>
    string? UnavailableReason { get; }

    /// <summary>프레임의 regionLocal 영역만 인식한다. 백그라운드에서 돌며 결과 좌표는 프레임 로컬 px.</summary>
    Task<OcrResult> RecognizeAsync(CapturedFrame frame, RectPx regionLocal, CancellationToken ct);
}
