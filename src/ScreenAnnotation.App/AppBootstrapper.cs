using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ScreenAnnotation.App;

public static class AppBootstrapper
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<TrayController>();
        return services.BuildServiceProvider();
    }
}
