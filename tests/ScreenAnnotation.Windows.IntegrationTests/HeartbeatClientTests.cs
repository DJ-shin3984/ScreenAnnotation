using System.IO.Pipes;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenAnnotation.Core.Watchdog;
using ScreenAnnotation.Windows.Watchdog;

namespace ScreenAnnotation.Windows.IntegrationTests;

public class HeartbeatClientTests
{
    [Fact]
    public async Task WritesHeartbeatsToPipe()
    {
        var lines = new List<string>();
        using var server = new NamedPipeServerStream(HeartbeatClient.PipeName(Environment.ProcessId), PipeDirection.In, 1);
        var reading = Task.Run(() =>
        {
            server.WaitForConnection();
            using var sr = new StreamReader(server);
            while (lines.Count < 2 && sr.ReadLine() is { } line) lines.Add(line);
        });

        var source = new StaticHeartbeatSource { Current = new Heartbeat(true, [0x10]) };
        using var client = new HeartbeatClient(source, NullLogger<HeartbeatClient>.Instance);
        client.Start(new SynchronizationContext(), watchdogExe: "");

        (await Task.WhenAny(reading, Task.Delay(TimeSpan.FromSeconds(3)))).Should().BeSameAs(reading);
        lines.Should().HaveCountGreaterOrEqualTo(2).And.AllBe("1 0x10");
    }
}
