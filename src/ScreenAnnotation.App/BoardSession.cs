using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Annotations;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Diagnostics;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Session;
using ScreenAnnotation.Core.Settings;
using ScreenAnnotation.Core.Tools;
using ScreenAnnotation.Core.Watchdog;
using ScreenAnnotation.Core.Windows;
using ScreenAnnotation.Rendering;
using ScreenAnnotation.Windows.Focus;

namespace ScreenAnnotation.App;

/// <summary>한 번의 켜기~끄기(보드). 핫키 → 포그라운드 저장 → 창 스냅샷 → 캡처 → 오버레이 → Active. 끄기 → 숨김 → 포커스 복귀.</summary>
public sealed class BoardSession(
    ModeController mode,
    IMonitorService monitors,
    IWindowSnapshotService snapshots,
    ICaptureService capture,
    OverlayHost overlay,
    ForegroundWindowService foreground,
    StaticHeartbeatSource heartbeat,
    ElementProbe probe,
    ModeHolder holder,
    AnnotationStore store,
    HistoryManager history,
    ToolState tools,
    Settings settings,
    ILoggerFactory loggerFactory,
    ILogger<BoardSession> log)
{
    private nint _previousForeground;
    private readonly Dictionary<OverlayWindow, (WpfOverlayRenderer Renderer, BoardInput Input)> _boards = [];

    private (WpfOverlayRenderer Renderer, BoardInput Input) Board(OverlayWindow w)
    {
        if (!_boards.TryGetValue(w, out var b))
        {
            var renderer = new WpfOverlayRenderer(w, store);
            var input = new BoardInput(w, renderer, tools, store, history, settings, loggerFactory.CreateLogger<BoardInput>());
            _boards[w] = b = (renderer, input);
        }
        return b;
    }

    public ModeController Mode => mode;
    public WindowSnapshot? Snapshot { get; private set; }

    /// <summary>UI 스레드에서 호출.</summary>
    public async Task OnHotkeyAsync()
    {
        switch (mode.OnHotkey())
        {
            case HotkeyAction.Start: await StartAsync(); break;
            case HotkeyAction.Stop: Stop(); break;
        }
    }

    private async Task StartAsync()
    {
        using var perf = new PerfScope(log, "Board.Start");
        _previousForeground = foreground.Capture();
        using (new PerfScope(log, "Board.Snapshot"))
        {
            Snapshot = snapshots.Take();
            holder.Snapshot = Snapshot;
        }

        IReadOnlyList<CapturedFrame> frames;
        try
        {
            frames = await capture.CaptureAllAsync(monitors.Monitors);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "캡처 실패");
            frames = [];
        }

        if (frames.Count == 0 || mode.Mode != Core.Session.Mode.Capturing)
        {
            mode.Deactivate();
            return;
        }

        using (new PerfScope(log, "Board.Show"))
        {
            overlay.ShowAll(frames);
            foreach (var w in overlay.Windows)
            {
                var (renderer, _) = Board(w);
                renderer.InvalidateAll();
                renderer.Start();
                probe.Attach(w);
            }
            tools.Reset();
        }
        heartbeat.Current = new Heartbeat(true, overlay.Handles.Select(h => (long)h).ToList());
        mode.Activate();
    }

    /// <summary>어떤 상태에서든 안전하게 끈다 (캡처 실패·종료 포함).</summary>
    public void Stop()
    {
        probe.Reset();
        foreach (var (renderer, input) in _boards.Values)
        {
            input.Reset();
            renderer.Stop();
        }
        overlay.HideAll();
        history.Clear();
        store.DiscardAll(); // 보드는 끄면 버린다 (저장은 T50/T51)
        heartbeat.Current = Heartbeat.Inactive;
        Snapshot = null;
        holder.Snapshot = null;
        mode.Deactivate();
        if (foreground.Restore(_previousForeground))
            log.LogInformation("Board.Stop 포커스 복귀 0x{Hwnd:X}", _previousForeground);
        else
            log.LogWarning("Board.Stop 이전 포그라운드 창 복귀 실패 0x{Hwnd:X}", _previousForeground);
        _previousForeground = 0;
        GC.Collect(); // 모니터별 캡처 버퍼(수십 MB, LOH)를 바로 돌려준다
    }
}
