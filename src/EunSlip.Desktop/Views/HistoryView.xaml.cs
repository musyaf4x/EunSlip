using System.Windows.Controls;

namespace EunSlip.Desktop.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is ViewModels.HistoryViewModel vm)
            {
                vm.LoadedCommand.Execute(null);
            }
        };
    }
}
