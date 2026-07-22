using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using EunSlip.Desktop.ViewModels;
using EunSlip.Desktop.Views;
using EunSlip.Infrastructure.FileSystem;

namespace EunSlip.Desktop.Tests;

public sealed class ViewSmokeTests
{
    [Fact]
    public void AllViews_ConstructWithCompiledThemeOnStaThread()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                Application application = Application.Current ?? new Application();
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/EunSlip.Desktop;component/Theme.xaml",
                        UriKind.Absolute),
                });

                _ = new HomeView();
                _ = new WizardView();
                _ = new HistoryView();
                _ = new SettingsView();
                AboutView aboutView = new()
                {
                    DataContext = new AboutViewModel(new AppPaths(Path.GetTempPath())),
                };
                Window aboutHost = new()
                {
                    Content = aboutView,
                    ShowInTaskbar = false,
                    Width = 1024,
                    Height = 768,
                };
                aboutHost.Show();
                aboutHost.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(
                    static () => { },
                    DispatcherPriority.ApplicationIdle);
                aboutHost.Close();

                CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
                try
                {
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("id-ID");
                    EunSlip.Desktop.MainWindow window = new(null!);
                    Assert.Equal("id-ID", window.Language.IetfLanguageTag, ignoreCase: true);
                }
                finally
                {
                    CultureInfo.CurrentUICulture = originalUiCulture;
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WPF view construction timed out.");
        Assert.Null(failure);
    }
}
