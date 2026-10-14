using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Diagnostics;
using ScreenAnnotation.Core.Input;
using ScreenAnnotation.Core.Monitors;
using ScreenAnnotation.Core.Recognition;
using ScreenAnnotation.Core.Watchdog;
using ScreenAnnotation.Recognition;
using ScreenAnnotation.Core.Windows;
using ScreenAnnotation.Storage;
using ScreenAnnotation.Windows.Input;
using ScreenAnnotation.Windows.Monitors;
using ScreenAnnotation.Windows.Watchdog;
using ScreenAnnotation.Windows.Windows;

namespace ScreenAnnotation.App;

public static class AppBootstrapper
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug().AddProvider(new FileLoggerProvider()));
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton(sp => sp.GetRequiredService<SettingsRepository>().Load());
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IWindowSnapshotService, WindowSnapshotService>();
        services.AddSingleton<IUiaElementLocator, UiaElementLocator>();
        services.AddSingleton<StaticHeartbeatSource>();
        services.AddSingleton<IHeartbeatSource>(sp => sp.GetRequiredService<StaticHeartbeatSource>());
        services.AddSingleton<HeartbeatClient>();
        services.AddSingleton<TrayController>();
        return services.BuildServiceProvider();
    }
}
