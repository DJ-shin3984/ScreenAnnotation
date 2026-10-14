using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Diagnostics;
using ScreenAnnotation.Storage;

namespace ScreenAnnotation.App;

public static class AppBootstrapper
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug().AddProvider(new FileLoggerProvider()));
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton(sp => sp.GetRequiredService<SettingsRepository>().Load());
        services.AddSingleton<TrayController>();
        return services.BuildServiceProvider();
    }
}
