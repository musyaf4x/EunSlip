using System.Windows.Controls;

namespace EunSlip.Desktop.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ViewModels.HomeViewModel vm)
            {
                await vm.LoadedCommand.ExecuteAsync(null);
            }
        };
    }
}
