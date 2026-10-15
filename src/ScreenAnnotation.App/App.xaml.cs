using System.IO;
using System.Windows;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs = System.Windows.ExitEventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Input;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Rendering;
using ScreenAnnotation.Windows.Capture;
using ScreenAnnotation.Windows.Input;
using ScreenAnnotation.Core.Settings;
using ScreenAnnotation.Core.Tools;
using ScreenAnnotation.Storage;
using ScreenAnnotation.Core.Windows;
using ScreenAnnotation.Windows.Watchdog;

namespace ScreenAnnotation.App;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\ScreenAnnotation.SingleInstance";
    private Mutex? _mutex;
    private ServiceProvider? _services;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // 두 번째 실행: `--toggle`이면 실행 중인 인스턴스에 켜기/끄기만 전달하고 종료
            if (e.Args.Contains("--toggle")) HotkeyService.PostToggle();
            Shutdown();
            return;
        }

        _services = AppBootstrapper.Build();
        var appLog = _services.GetRequiredService<ILogger<App>>();
        DispatcherUnhandledException += (_, ex) =>
        {
            appLog.LogError(ex.Exception, "처리되지 않은 예외");
            ex.Handled = true; // 앱을 죽이지 않는다. 보드가 켜져 있으면 안전하게 끈다
            _services?.GetRequiredService<BoardSession>().Stop();
        };
        _services.GetRequiredService<ILogger<App>>().LogInformation("ScreenAnnotation started");
        _services.GetRequiredService<IMonitorService>(); // UI 스레드에서 생성: 메시지 창 + 모니터 목록 로그
        var tray = _services.GetRequiredService<TrayController>();
        tray.AddItem("설정", () => new SettingsWindow(_services.GetRequiredService<SettingsStore>(), _services.GetRequiredService<SettingsRepository>(),
            _services.GetRequiredService<IHotkeyService>(), _services.GetRequiredService<ToolState>()).Show());
        tray.AddItem("창 목록 로그", () => _services.GetRequiredService<IWindowSnapshotService>().Take());
        tray.AddItem("UI 스레드 정지 (디버그)", () => Thread.Sleep(10000));
        tray.Show();
        _services.GetRequiredService<HeartbeatClient>().Start(SynchronizationContext.Current!);

        var settings = _services.GetRequiredService<Settings>();
        var gesture = HotkeyGesture.TryParse(settings.Hotkey, out var g) ? g : HotkeyGesture.Default;
        var hotkey = _services.GetRequiredService<IHotkeyService>();
        var capture = _services.GetRequiredService<ICaptureService>();
        ((CaptureService)capture).Warmup(); // DXGI duplication 미리 생성: 첫 켜기 지연 단축
        _services.GetRequiredService<OverlayHost>().Prepare(_services.GetRequiredService<IMonitorService>().Monitors);
        tray.AddItem("테스트 캡처 (PNG 저장)", async () =>
        {
            var frames = await capture.CaptureAllAsync(_services.GetRequiredService<IMonitorService>().Monitors);
            var dir = Path.Combine(Path.GetTempPath(), "ScreenAnnotation");
            foreach (var f in frames)
                PngWriter.Save(f, Path.Combine(dir, $"capture_{f.Monitor.DeviceName.TrimStart('\\', '.')}.png"));
        });
        var session = _services.GetRequiredService<BoardSession>();
        hotkey.HotkeyPressed += async (_, _) => await session.OnHotkeyAsync();
        tray.AddItem("오버레이 테스트 (2초)", async () =>
        {
            var monitors = _services.GetRequiredService<IMonitorService>().Monitors;
            var frames = await _services.GetRequiredService<ICaptureService>().CaptureAllAsync(monitors);
            var host = _services.GetRequiredService<OverlayHost>();
            host.ShowAll(frames);
            await Task.Delay(2000);
            host.HideAll();
        });
        if (!hotkey.Register(gesture))
            tray.ShowBalloon("단축키 등록 실패", $"{gesture}를 다른 프로그램이 사용 중입니다. 설정에서 변경하세요.");
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _services?.GetRequiredService<BoardSession>().Stop();
        _services?.GetRequiredService<ILogger<App>>().LogInformation("ScreenAnnotation stopped");
        _services?.Dispose();
        _mutex?.Dispose();
    }
}
