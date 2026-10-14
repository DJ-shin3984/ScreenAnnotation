using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Diagnostics;
using ScreenAnnotation.Core.Input;
using ScreenAnnotation.Core.Windows;
using ScreenAnnotation.Storage;
using ScreenAnnotation.Windows.Input;
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
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IWindowSnapshotService, WindowSnapshotService>();
        services.AddSingleton<TrayController>();
        return services.BuildServiceProvider();
    }
}
