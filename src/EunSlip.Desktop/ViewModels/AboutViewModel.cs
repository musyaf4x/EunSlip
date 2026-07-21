using EunSlip.Desktop.Localization;
using EunSlip.Infrastructure.FileSystem;

namespace EunSlip.Desktop.ViewModels;

public sealed partial class AboutViewModel(AppPaths paths) : ViewModelBase
{
    private readonly AppPaths _paths = paths;

    public string AppName => Strings.Get("AppName");
    public string Version => "1.0.0";
    public string Company => "PT. EUNSUNG INDONESIA";
    public string LogFolder => _paths.LogsDirectory;
}
