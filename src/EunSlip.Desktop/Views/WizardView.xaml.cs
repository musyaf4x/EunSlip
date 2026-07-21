using System.Windows.Controls;
using EunSlip.Desktop.ViewModels;
using Microsoft.Win32;

namespace EunSlip.Desktop.Views;

public partial class WizardView : UserControl
{
    public WizardView()
    {
        InitializeComponent();
    }

    private void PickFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not PayrollWizardViewModel vm)
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Filter = "Excel Workbook|*.xlsx",
            Title = "Pilih file payroll",
        };

        if (dialog.ShowDialog() == true)
        {
            vm.SelectedFilePath = dialog.FileName;
        }
    }
}
