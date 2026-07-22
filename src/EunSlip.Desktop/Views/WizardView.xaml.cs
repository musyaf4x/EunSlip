using System.Windows.Controls;
using EunSlip.Desktop.ViewModels;
using Microsoft.Win32;

namespace EunSlip.Desktop.Views;

public partial class WizardView : UserControl
{
    public WizardView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is PayrollWizardViewModel vm)
            {
                await vm.LoadedCommand.ExecuteAsync(null);
            }
        };
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

    private void OpenPreview_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not PayrollWizardViewModel vm)
        {
            return;
        }

        string? path = vm.GeneratePreviewPdf();
        if (path is null)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            vm.ReportPreviewOpenFailure();
        }
    }
}
