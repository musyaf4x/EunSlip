using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EunSlip.Desktop.Localization;

namespace EunSlip.Desktop.Tests;

public sealed class LocalizationContractTests
{
    private static readonly string[] UserFacingXamlFiles =
    [
        "src/EunSlip.Desktop/MainWindow.xaml",
        "src/EunSlip.Desktop/Views/HomeView.xaml",
        "src/EunSlip.Desktop/Views/WizardView.xaml",
        "src/EunSlip.Desktop/Views/HistoryView.xaml",
        "src/EunSlip.Desktop/Views/SettingsView.xaml",
        "src/EunSlip.Desktop/Views/AboutView.xaml",
    ];

    private static string RepositoryPath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EunSlip.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    [Fact]
    public void LocalizationResources_HaveMatchingKeys()
    {
        HashSet<string> defaultKeys = [.. ResourceEntries("src/EunSlip.Desktop/Localization/Strings.resx").Keys];
        HashSet<string> englishKeys = [.. ResourceEntries("src/EunSlip.Desktop/Localization/Strings.en.resx").Keys];

        Assert.Equal(defaultKeys.Order(), englishKeys.Order());
    }

    [Fact]
    public void UserFacingXaml_UsesBindingsOrLocalizedMarkup()
    {
        HashSet<string> languageNeutralCopy =
        [
            "EunSlip", "EUNSLIP", "PAYROLL DELIVERY", "EUNSLIP PAYROLL DELIVERY",
            "GMAIL", "STAMP", "NIK", "EMAIL", "OAUTH CLIENT", "OAuth Client",
            "Gmail API", "Internal", "Desktop app", "client_secret.json",
            "console.cloud.google.com", "PT. EUNSUNG INDONESIA", ".", "/",
        ];
        HashSet<string> userFacingAttributes =
        [
            "Text", "Content", "Header", "ToolTip", "Title", "AutomationProperties.Name",
        ];
        List<string> violations = [];

        foreach (string relativePath in UserFacingXamlFiles)
        {
            XDocument document = XDocument.Load(RepositoryPath(relativePath), LoadOptions.SetLineInfo);
            foreach (XAttribute attribute in document.Descendants().Attributes())
            {
                if (!userFacingAttributes.Contains(attribute.Name.LocalName))
                {
                    continue;
                }

                string value = attribute.Value.Trim();
                if (value.Length == 0 || value.StartsWith('{') || languageNeutralCopy.Contains(value))
                {
                    continue;
                }

                violations.Add($"{relativePath}: {attribute.Name.LocalName}=\"{value}\"");
            }

            string xaml = File.ReadAllText(RepositoryPath(relativePath));
            foreach (string localizedFormat in new[] { "StringFormat='Versi", "StringFormat='Untuk", "StringFormat='Folder log" })
            {
                if (xaml.Contains(localizedFormat, StringComparison.Ordinal))
                {
                    violations.Add($"{relativePath}: {localizedFormat}");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void CodeBehindAndViewModels_DoNotContainKnownIndonesianUiCopy()
    {
        Dictionary<string, string[]> prohibitedCopy = new()
        {
            ["src/EunSlip.Desktop/Views/WizardView.xaml.cs"] = ["Pilih file payroll"],
            ["src/EunSlip.Desktop/Views/SettingsView.xaml.cs"] = ["Gambar|", "Pilih gambar stamp"],
            ["src/EunSlip.Desktop/ViewModels/PayrollWizardViewModel.cs"] = ["Slip Gaji Karyawan", "Yth. Bapak/Ibu"],
            ["src/EunSlip.Desktop/ViewModels/HistoryViewModel.cs"] = ["Batch dihapus permanen", "Gagal menghapus batch"],
            ["src/EunSlip.Desktop/ViewModels/SettingsViewModel.cs"] =
            [
                "Kredensial OAuth disimpan", "Gagal menyimpan kredensial", "Simpan kredensial OAuth",
                "Membuka browser", "Kredensial OAuth rusak", "Gmail terhubung", "Gagal menghubungkan Gmail",
                "Gmail diputus", "Stamp diperbarui", "Bahasa disimpan",
            ],
            ["src/EunSlip.Desktop/ViewModels/AboutViewModel.cs"] =
            [
                "EunSlip adalah perangkat lunak", "Perangkat lunak ini disediakan",
            ],
        };
        List<string> violations = [];

        foreach ((string relativePath, string[] phrases) in prohibitedCopy)
        {
            string source = File.ReadAllText(RepositoryPath(relativePath));
            violations.AddRange(phrases
                .Where(phrase => source.Contains(phrase, StringComparison.Ordinal))
                .Select(phrase => $"{relativePath}: {phrase}"));
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void EnglishResources_DoNotContainKnownIndonesianWords()
    {
        Regex indonesianWord = new(
            @"\b(Beranda|Riwayat|Pengaturan|Tentang|Langkah|Pilih|Kembali|Lanjut|Gagal|Belum|Siap|Mengirim|Terkirim|Penerima|Periode|Bahasa|Hapus|Batal|Simpan|Hubungkan|Putuskan|Dikembangkan|Lisensi)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        IReadOnlyDictionary<string, string> english = ResourceEntries(
            "src/EunSlip.Desktop/Localization/Strings.en.resx");
        List<string> violations =
        [
            .. english
                .Where(entry => indonesianWord.IsMatch(entry.Value))
                .Select(entry => $"{entry.Key}={entry.Value}"),
        ];

        Assert.Empty(violations);
    }

    [Fact]
    public void LocExtension_ResolvesCopyForCurrentUiCulture()
    {
        Type? extensionType = typeof(Strings).Assembly.GetType("EunSlip.Desktop.Localization.LocExtension");
        Assert.NotNull(extensionType);

        object extension = Activator.CreateInstance(extensionType, "Home_Title")!;
        MethodInfo provideValue = extensionType.GetMethod("ProvideValue")!;
        CultureInfo originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("EunSlip Payroll Delivery", provideValue.Invoke(extension, [null]));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    private static IReadOnlyDictionary<string, string> ResourceEntries(string relativePath) =>
        XDocument.Load(RepositoryPath(relativePath))
            .Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);
}
