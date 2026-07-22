using EunSlip.Desktop.Localization;
using EunSlip.Infrastructure.FileSystem;

namespace EunSlip.Desktop.ViewModels;

public sealed partial class AboutViewModel(AppPaths paths) : ViewModelBase
{
    private readonly AppPaths _paths = paths;

    public string AppName => Strings.Get("AppName");
    public string Version => "1.0.0";
    public string Company => "PT. EUNSUNG INDONESIA";
    public string Developer => "Vierth Labs";
    public string ContactEmail => "vierthlabs@gmail.com";
    public string LicenseSummary => Strings.Get("About_LicenseSummary");
    public string Disclaimer => Strings.Get("About_Disclaimer");
    public string LogFolder => _paths.LogsDirectory;
}
