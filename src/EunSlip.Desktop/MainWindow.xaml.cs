using System.Windows;
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
