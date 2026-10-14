using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ScreenAnnotation.Windows.IntegrationTests;

/// <summary>ILogger → 테스트 출력.</summary>
public sealed class XunitLogger<T>(ITestOutputHelper output) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => output.WriteLine($"[{logLevel}] {formatter(state, exception)}{(exception is null ? "" : " " + exception)}");
}
