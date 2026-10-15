using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Recognition;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using OcrResult = ScreenAnnotation.Core.Recognition.OcrResult;
using OcrLine = ScreenAnnotation.Core.Recognition.OcrLine;
using OcrWord = ScreenAnnotation.Core.Recognition.OcrWord;

namespace ScreenAnnotation.Recognition;

/// <summary>Windows 내장 OCR (Windows.Media.Ocr). 한국어 → 사용자 언어 → 영어 순으로 엔진을 만든다.</summary>
public sealed class WindowsMediaOcrService : IOcrService
{
    private readonly OcrEngine? _engine;
    private readonly ILogger<WindowsMediaOcrService> _log;

    public WindowsMediaOcrService(ILogger<WindowsMediaOcrService> log)
    {
        _log = log;
        _engine = OcrEngine.TryCreateFromLanguage(new Language("ko"))
                  ?? OcrEngine.TryCreateFromUserProfileLanguages()
                  ?? OcrEngine.TryCreateFromLanguage(new Language("en"));
        if (_engine is null)
            log.LogWarning("OCR 엔진 없음: {Reason}", UnavailableReason);
        else
            log.LogInformation("OCR 엔진 {Language}", _engine.RecognizerLanguage.LanguageTag);
    }

    public bool IsAvailable => _engine is not null;

    public string? UnavailableReason => _engine is null
        ? "한국어 OCR 언어팩이 없습니다. 설정 → 시간 및 언어 → 언어 → 한국어 → 언어 옵션 → 광학 문자 인식"
        : null;

    public string? LanguageTag => _engine?.RecognizerLanguage.LanguageTag;

    public Task<OcrResult> RecognizeAsync(CapturedFrame frame, RectPx regionLocal, CancellationToken ct)
    {
        if (_engine is null) return Task.FromResult(OcrResult.Empty);
        var region = Clip(frame, regionLocal);
        if (region.Width < 1 || region.Height < 1) return Task.FromResult(OcrResult.Empty);
        return Task.Run(() => RecognizeCoreAsync(frame, region, ct), ct);
    }

    private async Task<OcrResult> RecognizeCoreAsync(CapturedFrame frame, RectPx region, CancellationToken ct)
    {
        using var bitmap = Crop(frame, region);
        var raw = await _engine!.RecognizeAsync(bitmap).AsTask(ct);
        var lines = raw.Lines.Select(l =>
        {
            var words = l.Words.Select(w => new OcrWord(w.Text, ToRect(w.BoundingRect, region))).ToList();
            return new OcrLine(l.Text, words.Select(w => w.Bounds).Aggregate(Union), words);
        }).ToList();
        _log.LogDebug("OCR {Region}: {Lines}줄", region, lines.Count);
        return new OcrResult(lines);
    }

    /// <summary>영역을 프레임 안으로, 그리고 엔진 최대 크기 안으로 자른다.</summary>
    private static RectPx Clip(CapturedFrame frame, RectPx r)
    {
        var max = (int)OcrEngine.MaxImageDimension;
        var x = Math.Clamp(r.X, 0, frame.Width);
        var y = Math.Clamp(r.Y, 0, frame.Height);
        var w = Math.Min(Math.Min(r.Right, frame.Width) - x, max);
        var h = Math.Min(Math.Min(r.Bottom, frame.Height) - y, max);
        return new RectPx(x, y, Math.Max(0, w), Math.Max(0, h));
    }

    private static SoftwareBitmap Crop(CapturedFrame frame, RectPx r)
    {
        var buf = new byte[r.Width * 4 * r.Height];
        for (var row = 0; row < r.Height; row++)
            Buffer.BlockCopy(frame.Bgra, (r.Y + row) * frame.Stride + r.X * 4, buf, row * r.Width * 4, r.Width * 4);
        var bmp = new SoftwareBitmap(BitmapPixelFormat.Bgra8, r.Width, r.Height, BitmapAlphaMode.Ignore);
        bmp.CopyFromBuffer(buf.AsBuffer());
        return bmp;
    }

    private static RectPx ToRect(Windows.Foundation.Rect b, RectPx region) =>
        new(region.X + (int)Math.Floor(b.X), region.Y + (int)Math.Floor(b.Y), (int)Math.Ceiling(b.Width), (int)Math.Ceiling(b.Height));

    private static RectPx Union(RectPx a, RectPx b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new RectPx(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }
}

/// <summary>Ctrl+드래그 영역 OCR → 글자. 호출자가 클립보드에 넣는다.</summary>
public static class RegionOcr
{
    public static async Task<string> CopyTextAsync(IOcrService ocr, CapturedFrame frame, RectPx regionLocal, CancellationToken ct) =>
        (await ocr.RecognizeAsync(frame, regionLocal, ct)).Text;
}
