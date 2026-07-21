using System.Windows;
using EunSlip.Desktop.Localization;
using EunSlip.Infrastructure.FileSystem;
using EunSlip.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace EunSlip.Desktop;

public partial class App : Application
{
    private IDisposable? _instanceLock;
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppPaths paths = new(AppPaths.DefaultRoot);

        try
        {
            paths.EnsureCreated();
            _instanceLock = SingleInstanceLock.TryAcquire(paths.LockFilePath);
        }
        catch (Exception)
        {
            _ = MessageBox.Show(
                Strings.Get("StartupErrorMessage"),
                Strings.Get("AppName"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(2);
            return;
        }

        if (_instanceLock is null)
        {
            _ = MessageBox.Show(
                Strings.Get("AlreadyRunningMessage"),
                Strings.Get("AppName"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(1);
            return;
        }

        ServiceCollection services = new();
        _ = services.AddSingleton(paths);
        _ = services.AddSingleton(EunSlipLogging.CreateLoggerFactory(paths));
        _ = services.AddLogging();
        _services = services.BuildServiceProvider();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        _instanceLock?.Dispose();
        base.OnExit(e);
    }
}
