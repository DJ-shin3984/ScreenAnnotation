using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Diagnostics;

namespace ScreenAnnotation.Core.Tests.Diagnostics;

public class PerfScopeTests
{
    private sealed class MemoryLogger : ILogger
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception));
    }

    [Fact]
    public void LogsNameAndElapsedOnDispose()
    {
        var logger = new MemoryLogger();
        double elapsed;
        using (var scope = new PerfScope(logger, "capture"))
        {
            elapsed = scope.ElapsedMs;
        }

        elapsed.Should().BeGreaterThanOrEqualTo(0);
        logger.Lines.Should().ContainSingle().Which.Should().StartWith("capture took ").And.EndWith("ms");
    }
}
