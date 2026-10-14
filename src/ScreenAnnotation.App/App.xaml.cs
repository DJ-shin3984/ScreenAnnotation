using System.Windows;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs = System.Windows.ExitEventArgs;
using Microsoft.Extensions.DependencyInjection;

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
        _services.GetRequiredService<TrayController>().Show();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _services?.Dispose();
        _mutex?.Dispose();
    }
}
