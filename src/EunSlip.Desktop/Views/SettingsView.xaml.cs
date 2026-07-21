using System.Windows.Controls;
using Microsoft.Win32;

namespace EunSlip.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ViewModels.SettingsViewModel vm)
            {
                await vm.LoadedCommand.ExecuteAsync(null);
            }
        };
    }

    private void PickStamp_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.SettingsViewModel vm)
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Filter = "Gambar|*.png;*.jpg;*.jpeg",
            Title = "Pilih gambar stamp",
        };

        if (dialog.ShowDialog() == true)
        {
            vm.PickStampCommand.Execute(dialog.FileName);
        }
    }
}
