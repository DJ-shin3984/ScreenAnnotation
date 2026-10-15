using System.IO;
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
using ScreenAnnotation.Storage;
using ScreenAnnotation.Rendering.Export;
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
    SettingsStore settingsStore,
    SettingsRepository settingsRepository,
    ILoggerFactory loggerFactory,
    ILogger<BoardSession> log)
{
    private nint _previousForeground;
    private readonly Dictionary<OverlayWindow, (WpfOverlayRenderer Renderer, BoardInput Input)> _boards = [];
    private readonly Dictionary<string, CapturedFrame> _frames = [];
    private CloseConfirmWindow? _dialog; // 끄기 확인창이 떠 있는 동안만 non-null

    private (WpfOverlayRenderer Renderer, BoardInput Input) Board(OverlayWindow w)
    {
        if (!_boards.TryGetValue(w, out var b))
        {
            var renderer = new WpfOverlayRenderer(w, store);
            var text = new TextEditor(w, store, history, renderer.InvalidateAnnotations);
            var input = new BoardInput(w, renderer, tools, store, history, text, settingsStore, loggerFactory.CreateLogger<BoardInput>());
            input.SaveRequested += SaveAll;
            input.CopyRequested += () => Copy(w);
            input.CloseRequested += () =>
            {
                // 툴바 X: 판서가 있으면 저장/버리기/계속을 묻는다 (핫키는 묻지 않음)
                if (mode.Mode == Core.Session.Mode.Active && store.Count > 0 && !ConfirmClose()) return;
                _ = OnHotkeyAsync();
            };
            _boards[w] = b = (renderer, input);
        }
        return b;
    }

    public ModeController Mode => mode;
    public WindowSnapshot? Snapshot { get; private set; }

    /// <summary>UI 스레드에서 호출.</summary>
    public async Task OnHotkeyAsync()
    {
        log.LogInformation("핫키 수신 mode={Mode}", mode.Mode);
        if (_dialog is not null)
        {
            _dialog.Choose(CloseChoice.Discard); // 확인창에서 한 번 더 → 버리고 끔
            return;
        }
        // 사용자 결정(2026-10-15): 핫키로 끌 때 저장 여부를 묻지 않고 바로 끈다. 저장은 Ctrl+S/툴바. 확인창은 툴바 X에서만.

        switch (mode.OnHotkey())
        {
            case HotkeyAction.Start: await StartAsync(); break;
            case HotkeyAction.Stop: Stop(); break;
        }
    }

    /// <summary>커서가 있는 모니터 가운데에 확인창. true면 끈다(저장은 여기서 끝냄), false면 계속 판서.</summary>
    private bool ConfirmClose()
    {
        foreach (var (_, input) in _boards.Values) { input.Reset(); input.Suspended = true; }
        try
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            var monitor = CoordinateService.FindMonitor(monitors.Monitors, new PointPx(cursor.X, cursor.Y));
            var owner = overlay.Windows.FirstOrDefault(w => w.Monitor.Key == monitor?.Key) ?? overlay.Windows.First();

            var choice = CloseConfirmWindow.Show(owner, out _dialog);
            _dialog = null;
            if (choice == CloseChoice.Save) SaveAll();
            return choice != CloseChoice.Continue;
        }
        finally
        {
            _dialog = null;
            foreach (var (_, input) in _boards.Values) input.Suspended = false;
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
            _frames.Clear();
            foreach (var f in frames) _frames[f.Monitor.Key] = f;
            holder.Frames = _frames;
            holder.RendererOf = w => _boards.TryGetValue(w, out var b) ? b.Renderer : null;
            holder.FlashOf = w => _boards.TryGetValue(w, out var b) ? b.Input.Flash : null;
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

    /// <summary>Ctrl+S: 모니터별 정지 화면 + 판서 합성 PNG를 설정 폴더에 저장.</summary>
    /// <summary>Ctrl+S / 툴바 저장: 저장 위치를 묻는다. 커서 모니터는 고른 이름 그대로, 다른 모니터는 _2, _3 붙여 저장.</summary>
    public void SaveAll()
    {
        if (_boards.Count == 0) return;
        var cursorWindow = WindowUnderCursor();
        string? chosen;
        foreach (var (_, input) in _boards.Values) { input.Reset(); input.Suspended = true; }
        try
        {
            var dir = settingsStore.Current.SavePath;
            Directory.CreateDirectory(dir);
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "판서 저장",
                Filter = "PNG 이미지 (*.png)|*.png",
                DefaultExt = ".png",
                AddExtension = true,
                InitialDirectory = dir,
                FileName = $"ScreenAnnotation_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            };
            chosen = dlg.ShowDialog(cursorWindow) == true ? dlg.FileName : null;
        }
        finally
        {
            foreach (var (_, input) in _boards.Values) input.Suspended = false;
        }
        if (chosen is null) return;

        using var perf = new PerfScope(log, "Export.Save");
        var folder = Path.GetDirectoryName(chosen)!;
        var stem = Path.GetFileNameWithoutExtension(chosen);
        var n = 1;
        foreach (var (w, (renderer, input)) in _boards.OrderBy(b => b.Key == cursorWindow ? 0 : 1))
        {
            if (!_frames.TryGetValue(w.Monitor.Key, out var frame)) continue;
            var img = BoardExporter.Compose(frame, store.ForMonitor(w.Monitor.Key), renderer.Ephemeral.Strokes);
            var path = n == 1 ? chosen : Path.Combine(folder, $"{stem}_{n}.png");
            n++;
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(img));
            using (var fs = File.Create(path)) enc.Save(fs);
            log.LogInformation("저장 {Path}", path);
            input.Flash("저장됨");
        }
        if (folder != settingsStore.Current.SavePath)
        {
            settingsStore.Update(c => c with { SavePath = folder }); // 마지막 저장 폴더 기억
            settingsRepository.Save(settingsStore.Current);
        }
    }

    private OverlayWindow WindowUnderCursor()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var monitor = CoordinateService.FindMonitor(monitors.Monitors, new PointPx(cursor.X, cursor.Y));
        return overlay.Windows.FirstOrDefault(w => w.Monitor.Key == monitor?.Key) ?? overlay.Windows.First();
    }

    /// <summary>Ctrl+C: 키를 받은 모니터의 합성본을 클립보드로.</summary>
    public void Copy(OverlayWindow w)
    {
        if (!_frames.TryGetValue(w.Monitor.Key, out var frame) || !_boards.TryGetValue(w, out var b)) return;
        using var perf = new PerfScope(log, "Export.Copy");
        var img = BoardExporter.Compose(frame, store.ForMonitor(w.Monitor.Key), b.Renderer.Ephemeral.Strokes);
        try { ClipboardImage.Set(img); b.Input.Flash("복사됨"); }
        catch (Exception ex) { log.LogWarning(ex, "클립보드 복사 실패"); b.Input.Flash("복사 실패"); }
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
        _frames.Clear();
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
