using System.Windows;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs = System.Windows.ExitEventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Input;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Settings;
using ScreenAnnotation.Core.Windows;

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
            Shutdown();
            return;
        }

        _services = AppBootstrapper.Build();
        _services.GetRequiredService<ILogger<App>>().LogInformation("ScreenAnnotation started");
        _services.GetRequiredService<IMonitorService>(); // UI 스레드에서 생성: 메시지 창 + 모니터 목록 로그
        var tray = _services.GetRequiredService<TrayController>();
        tray.AddItem("창 목록 로그", () => _services.GetRequiredService<IWindowSnapshotService>().Take());
        tray.Show();

        var settings = _services.GetRequiredService<Settings>();
        var gesture = HotkeyGesture.TryParse(settings.Hotkey, out var g) ? g : HotkeyGesture.Default;
        var hotkey = _services.GetRequiredService<IHotkeyService>();
        hotkey.HotkeyPressed += (_, _) => _services.GetRequiredService<ILogger<App>>().LogInformation("HotkeyPressed {Gesture}", gesture);
        if (!hotkey.Register(gesture))
            tray.ShowBalloon("단축키 등록 실패", $"{gesture}를 다른 프로그램이 사용 중입니다. 설정에서 변경하세요.");
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _services?.GetRequiredService<ILogger<App>>().LogInformation("ScreenAnnotation stopped");
        _services?.Dispose();
        _mutex?.Dispose();
    }
}
