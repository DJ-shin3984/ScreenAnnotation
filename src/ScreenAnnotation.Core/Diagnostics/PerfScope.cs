using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ScreenAnnotation.Core.Diagnostics;

/// <summary>using 블록의 소요 시간을 Dispose 시점에 로그로 남긴다.</summary>
public sealed class PerfScope : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _name;
    private readonly long _start = Stopwatch.GetTimestamp();

    public PerfScope(ILogger logger, string name)
    {
        _logger = logger;
        _name = name;
    }

    public double ElapsedMs => Stopwatch.GetElapsedTime(_start).TotalMilliseconds;

    public void Dispose() => _logger.LogInformation("{Name} took {ElapsedMs:F1}ms", _name, ElapsedMs);
}
