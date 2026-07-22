using System.Windows;
using EunSlip.Desktop.Views;

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
                _ = new AboutView();
                _ = new EunSlip.Desktop.MainWindow(null!);
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
