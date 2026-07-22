using System.ComponentModel;
using System.Windows;
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
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
