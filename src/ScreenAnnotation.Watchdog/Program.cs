// ScreenAnnotation.Watchdog.exe <appPid>
// App의 UI 스레드가 Active 상태에서 2초간 하트비트를 못 보내면 오버레이 창을 숨기고 App을 죽인다.
using System.Diagnostics;
using System.IO.Pipes;
using ScreenAnnotation.Core.Watchdog;
using ScreenAnnotation.Windows.Watchdog;

if (args.Length < 1 || !int.TryParse(args[0], out var pid)) return 2;

Process app;
try { app = Process.GetProcessById(pid); }
catch (ArgumentException) { return 0; }

var silence = TimeSpan.FromSeconds(2);
var lastBeatAt = Stopwatch.GetTimestamp();
var last = Heartbeat.Inactive;
var gate = new object();

// 파이프 읽기 스레드: 클라이언트가 끊기면 다시 기다린다.
var reader = new Thread(() =>
{
    while (!app.HasExited)
    {
        try
        {
            using var server = new NamedPipeServerStream(HeartbeatClient.PipeName(pid), PipeDirection.In, 1);
            server.WaitForConnection();
            using var sr = new StreamReader(server);
            while (sr.ReadLine() is { } line)
            {
                if (!Heartbeat.TryParse(line, out var hb)) continue;
                lock (gate) { last = hb; lastBeatAt = Stopwatch.GetTimestamp(); }
            }
        }
        catch (IOException) { }
    }
}) { IsBackground = true };
reader.Start();

while (!app.WaitForExit(250))
{
    Heartbeat hb; TimeSpan quiet;
    lock (gate) { hb = last; quiet = Stopwatch.GetElapsedTime(lastBeatAt); }
    if (!hb.Active || quiet < silence) continue;

    foreach (var h in hb.OverlayHandles) OverlayHider.Hide(h);
    try { app.Kill(); } catch (InvalidOperationException) { }
    return 0;
}
return 0;
