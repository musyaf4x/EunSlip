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
    public string LicenseSummary =>
        "EunSlip adalah perangkat lunak berpemilik yang dilisensikan kepada " +
        "PT. EUNSUNG INDONESIA untuk penggunaan internal. Hak cipta 2026 Vierth Labs. " +
        "Dilarang menyebarluaskan, memodifikasi, atau menjual ulang tanpa izin tertulis.";
    public string Disclaimer =>
        "Perangkat lunak ini disediakan \"sebagaimana adanya\". Vierth Labs tidak bertanggung jawab " +
        "atas kerugian akibat penggunaan tidak langsung. Pengguna menanggung risiko penggunaan.";
    public string LogFolder => _paths.LogsDirectory;
}
