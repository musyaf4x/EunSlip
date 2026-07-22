using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
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
            "Pengiriman sedang berlangsung. Tunggu sampai proses selesai.",
            "EunSlip",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
