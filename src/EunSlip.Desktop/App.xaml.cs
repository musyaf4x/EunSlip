using System.IO;
using System.Windows;
using EunSlip.Core.Batches;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Recovery;
using EunSlip.Core.Security;
using EunSlip.Core.Sending;
using EunSlip.Desktop.Localization;
using EunSlip.Desktop.ViewModels;
using EunSlip.Infrastructure.Batches;
using EunSlip.Infrastructure.Excel;
using EunSlip.Infrastructure.FileSystem;
using EunSlip.Infrastructure.Gmail;
using EunSlip.Infrastructure.Logging;
using EunSlip.Infrastructure.Persistence;
using EunSlip.Infrastructure.Pdf;
using EunSlip.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace EunSlip.Desktop;

public partial class App : Application
{
    private IDisposable? _instanceLock;
    private ServiceProvider? _services;

    public static IServiceProvider Services { get; private set; } = null!;

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

        _services = BuildServices(paths);
        Services = _services;

        _services.GetRequiredService<IAppRepository>().Initialize();
        _services.GetRequiredService<ITempFileService>().CleanupLeftovers();

        MainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow.Show();

        base.OnStartup(e);
    }

    private static ServiceProvider BuildServices(AppPaths paths)
    {
        ServiceCollection services = new();

        _ = services.AddSingleton(paths);
        _ = services.AddSingleton(EunSlipLogging.CreateLoggerFactory(paths));
        _ = services.AddLogging();

        services.AddSingleton<ISecretStore>(sp =>
        {
            byte[] key = DpapiKeyProtector.LoadOrCreateKey(Path.Combine(paths.OAuthDirectory, "aes.key"));
            return new AesGcmSecretStore(key);
        });

        services.AddSingleton<IAppRepository>(_ => new SqliteAppRepository(
            $"Data Source={Path.Combine(paths.DatabaseDirectory, "eunslip.db")}"));

        services.AddSingleton<IPayrollWorkbookReader, OpenXmlWorkbookReader>();
        services.AddSingleton<IPayslipPdfGenerator, PayslipPdfGenerator>();
        services.AddSingleton<ISharedFileStore, SharedFileStore>();
        services.AddSingleton<ITempFileService, TempFileService>();
        services.AddSingleton<IGmailAuthorization>(sp =>
        {
            IAppRepository repo = sp.GetRequiredService<IAppRepository>();
            return new GmailAuthorization(
                new DpapiTokenDataStore(paths.OAuthDirectory),
                ResolveClientSecretAsync);

            async Task<string?> ResolveClientSecretAsync(CancellationToken ct)
            {
                string? stored = repo.GetSetting("OAuthClientSecret");
                if (string.IsNullOrWhiteSpace(stored))
                {
                    return null;
                }

                try
                {
                    byte[] envelope = Convert.FromBase64String(stored);
                    byte[] secret = DpapiKeyProtector.UnprotectToken(envelope);
                    return System.Text.Encoding.UTF8.GetString(secret);
                }
                catch
                {
                    return null;
                }
            }
        });
        _ = services.AddSingleton<IMimeMessageBuilder, MimeMessageBuilder>();
        _ = services.AddSingleton<IGmailSender, GmailSender>();
        _ = services.AddSingleton<IGmailRetrySender, GmailRetrySender>();
        _ = services.AddSingleton<IBatchCoordinator, BatchCoordinator>();
        _ = services.AddSingleton<IRecoveryService, RecoveryService>();

        _ = services.AddSingleton<HomeViewModel>();
        _ = services.AddSingleton<PayrollWizardViewModel>();
        _ = services.AddSingleton<HistoryViewModel>();
        _ = services.AddSingleton<SettingsViewModel>();
        _ = services.AddSingleton<AboutViewModel>();
        _ = services.AddSingleton<MainViewModel>();
        _ = services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        _instanceLock?.Dispose();
        base.OnExit(e);
    }
}
