using Microsoft.Extensions.Logging;

namespace ScreenAnnotation.Core.Diagnostics;

/// <summary>일 단위 파일(yyyyMMdd.log)에 추가 기록. 생성 시 보관 기간이 지난 파일을 지운다.</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly object _lock = new();

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenAnnotation", "logs");

    public FileLoggerProvider(string? directory = null, int retainDays = 7)
    {
        _directory = directory ?? DefaultDirectory;
        Directory.CreateDirectory(_directory);
        var cutoff = DateTime.Now.AddDays(-retainDays);
        foreach (var f in Directory.EnumerateFiles(_directory, "*.log"))
        {
            if (File.GetLastWriteTime(f) < cutoff) File.Delete(f);
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose() { }

    private void Write(string line)
    {
        lock (_lock)
        {
            File.AppendAllText(Path.Combine(_directory, $"{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine);
        }
    }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{logLevel}] {category}: {formatter(state, exception)}";
            if (exception is not null) line += Environment.NewLine + exception;
            owner.Write(line);
        }
    }
}
