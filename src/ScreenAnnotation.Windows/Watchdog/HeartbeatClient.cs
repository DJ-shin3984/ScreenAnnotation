using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Watchdog;

namespace ScreenAnnotation.Windows.Watchdog;

/// <summary>
/// 워치독 프로세스를 띄우고 Named Pipe로 500ms마다 하트비트를 보낸다.
/// 타이머는 스레드 풀에서 돌지만 실제 쓰기는 UI SynchronizationContext에 Post해서 UI 스레드가 한다.
/// UI 스레드가 멈추면 쓰기도 멈추고, 워치독이 그 침묵을 감지한다.
/// </summary>
public sealed class HeartbeatClient : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    private readonly IHeartbeatSource _source;
    private readonly ILogger<HeartbeatClient> _logger;
    private readonly int _pid = Environment.ProcessId;
    private Process? _watchdog;
    private Timer? _timer;
    private StreamWriter? _writer;
    private bool _disposed;

    public HeartbeatClient(IHeartbeatSource source, ILogger<HeartbeatClient> logger)
    {
        _source = source;
        _logger = logger;
    }

    public static string PipeName(int pid) => $"ScreenAnnotation.Watchdog.{pid}";

    /// <param name="uiContext">하트비트를 쓸 스레드(WPF UI 스레드)의 컨텍스트.</param>
    /// <param name="watchdogExe">null이면 앱 exe 옆의 ScreenAnnotation.Watchdog.exe. 테스트에서는 외부 서버를 쓰므로 "" 전달.</param>
    public void Start(SynchronizationContext uiContext, string? watchdogExe = null)
    {
        if (watchdogExe != "")
        {
            watchdogExe ??= Path.Combine(AppContext.BaseDirectory, "ScreenAnnotation.Watchdog.exe");
            try
            {
                _watchdog = Process.Start(new ProcessStartInfo(watchdogExe, _pid.ToString()) { UseShellExecute = false });
                _logger.LogInformation("워치독 시작 pid={Pid}", _watchdog?.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "워치독 실행 실패: {Exe}", watchdogExe);
            }
        }

        _timer = new Timer(_ => uiContext.Post(_ => Beat(), null), null, Interval, Interval);
    }

    private void Beat()
    {
        if (_disposed) return;
        try
        {
            if (_writer is null)
            {
                var pipe = new NamedPipeClientStream(".", PipeName(_pid), PipeDirection.Out);
                pipe.Connect(100);
                _writer = new StreamWriter(pipe) { AutoFlush = true };
            }
            _writer.WriteLine(_source.Current.Format());
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or ObjectDisposedException)
        {
            _logger.LogDebug("하트비트 파이프 재연결 예정: {Message}", ex.Message);
            _writer?.Dispose();
            _writer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _writer?.Dispose();
        try { if (_watchdog is { HasExited: false }) _watchdog.Kill(); } catch (InvalidOperationException) { }
        _watchdog?.Dispose();
    }
}
