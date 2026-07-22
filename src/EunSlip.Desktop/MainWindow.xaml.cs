using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using EunSlip.Desktop.Localization;
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        DataContext = viewModel;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel { CanClose: false })
        {
            return;
        }

        e.Cancel = true;
        _ = MessageBox.Show(
            Strings.Get("SendingInProgress"),
            Strings.Get("AppName"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
