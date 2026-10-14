using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Diagnostics;

namespace ScreenAnnotation.Core.Tests.Diagnostics;

public class FileLoggerProviderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sa-logs-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void WritesToTodayFile()
    {
        var provider = new FileLoggerProvider(_dir);
        provider.CreateLogger("Test").LogInformation("hello {Who}", "world");

        var file = Path.Combine(_dir, $"{DateTime.Now:yyyyMMdd}.log");
        File.ReadAllText(file).Should().Contain("[Information] Test: hello world");
    }

    [Fact]
    public void DeletesFilesOlderThanRetention()
    {
        Directory.CreateDirectory(_dir);
        var old = Path.Combine(_dir, "20000101.log");
        var recent = Path.Combine(_dir, "20000102.log");
        File.WriteAllText(old, "x");
        File.WriteAllText(recent, "x");
        File.SetLastWriteTime(old, DateTime.Now.AddDays(-8));
        File.SetLastWriteTime(recent, DateTime.Now.AddDays(-3));

        _ = new FileLoggerProvider(_dir);

        File.Exists(old).Should().BeFalse();
        File.Exists(recent).Should().BeTrue();
    }
}
