using System.IO;
using System.Text;

namespace EunSlip.Desktop.Tests;

public sealed class ThemeContractTests
{
    private static string RepositoryPath(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EunSlip.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }

    private static string ReadRepositoryFile(params string[] parts) =>
        File.ReadAllText(RepositoryPath(parts), Encoding.UTF8);

    [Fact]
    public void Theme_DefinesRedesignResourcesAndBindsButtonBorders()
    {
        string xaml = ReadRepositoryFile("src", "EunSlip.Desktop", "Theme.xaml");

        foreach (string key in new[]
        {
            "PageFrame", "PageTitle", "PageSubtitle", "StatusBadge", "DangerButton",
            "NavButton", "PrimaryButton", "OutlinedButton", "FieldInput", "FieldPicker",
            "FieldCombo", "Card", "EditorialDataGrid",
        })
        {
            Assert.Contains($"x:Key=\"{key}\"", xaml, StringComparison.Ordinal);
        }

        Assert.Contains("BorderBrush=\"{TemplateBinding BorderBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=\"{TemplateBinding BorderThickness}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Property=\"MinHeight\" Value=\"40\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DropShadowEffect", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("LinearGradientBrush", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticPages_UseSharedPageHierarchyAndNoRawBooleanFormatting()
    {
        foreach (string relative in new[]
        {
            Path.Combine("src", "EunSlip.Desktop", "Views", "HomeView.xaml"),
            Path.Combine("src", "EunSlip.Desktop", "Views", "SettingsView.xaml"),
            Path.Combine("src", "EunSlip.Desktop", "Views", "AboutView.xaml"),
        })
        {
            string xaml = ReadRepositoryFile(relative.Split(Path.DirectorySeparatorChar));
            Assert.Contains("{StaticResource PageTitle}", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("StringFormat='Terkoneksi: {0}'", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("StringFormat='Aktif: {0}'", xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShellAndStaticPages_DefineStableAutomationIds()
    {
        string combined = string.Join("\n",
            ReadRepositoryFile("src", "EunSlip.Desktop", "MainWindow.xaml"),
            ReadRepositoryFile("src", "EunSlip.Desktop", "Views", "HomeView.xaml"),
            ReadRepositoryFile("src", "EunSlip.Desktop", "Views", "SettingsView.xaml"));

        foreach (string id in new[]
        {
            "NavHome", "NavPayroll", "NavHistory", "NavSettings", "NavAbout",
            "StartPayroll", "ConnectGmail", "DisconnectGmail", "OAuthSecretInput",
            "SaveOAuthSecret", "PickStamp", "RequestRemoveStamp", "LanguageSelector",
        })
        {
            Assert.Contains($"AutomationProperties.AutomationId=\"{id}\"", combined, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MainWindow_StartsMaximized()
    {
        string xaml = ReadRepositoryFile("src", "EunSlip.Desktop", "MainWindow.xaml");

        Assert.Contains("WindowState=\"Maximized\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsView_KeepsCompleteGoogleCloudSetupTutorial()
    {
        string xaml = ReadRepositoryFile("src", "EunSlip.Desktop", "Views", "SettingsView.xaml");

        foreach (string instruction in new[]
        {
            "PANDUAN SETUP GOOGLE CLOUD &amp; OAUTH",
            "1. Buka ",
            "2. APIs &amp; Services",
            "3. OAuth consent screen",
            "4. Credentials",
            "5. Download ",
            "6. Buka isi file client_secret.json",
            "7. Klik ",
        })
        {
            Assert.Contains(instruction, xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Branding_UsesSuppliedEunSlipLogoInShellAndAbout()
    {
        string project = ReadRepositoryFile("src", "EunSlip.Desktop", "EunSlip.Desktop.csproj");
        string shell = ReadRepositoryFile("src", "EunSlip.Desktop", "MainWindow.xaml");
        string about = ReadRepositoryFile("src", "EunSlip.Desktop", "Views", "AboutView.xaml");

        Assert.Contains(
            "<Resource Include=\"..\\..\\logo\\eunslip-logo-black-01.png\" Link=\"Assets\\eunslip-logo-black-01.png\" />",
            project,
            StringComparison.Ordinal);
        Assert.Contains("Source=\"/Assets/eunslip-logo-black-01.png\"", shell, StringComparison.Ordinal);
        Assert.Contains("Source=\"/Assets/eunslip-logo-black-01.png\"", about, StringComparison.Ordinal);
        Assert.True(File.Exists(RepositoryPath("logo", "eunslip-logo-black-01.png")));
    }

    [Fact]
    public void DesktopExecutable_UsesMultiSizeIconDerivedFromBackgroundLogo()
    {
        string project = ReadRepositoryFile("src", "EunSlip.Desktop", "EunSlip.Desktop.csproj");
        string shell = ReadRepositoryFile("src", "EunSlip.Desktop", "MainWindow.xaml");
        string sourceLogo = RepositoryPath("logo", "eunslip-logo-bg-01.png");
        string iconPath = RepositoryPath("src", "EunSlip.Desktop", "Assets", "eunslip.ico");

        Assert.Contains("<ApplicationIcon>Assets\\eunslip.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains(
            "<Resource Include=\"..\\..\\logo\\eunslip-logo-bg-01.png\" Link=\"Assets\\eunslip-logo-bg-01.png\" />",
            project,
            StringComparison.Ordinal);
        Assert.Contains("Icon=\"/Assets/eunslip-logo-bg-01.png\"", shell, StringComparison.Ordinal);
        Assert.True(File.Exists(sourceLogo));
        Assert.True(File.Exists(iconPath));

        byte[] icon = File.ReadAllBytes(iconPath);
        Assert.True(icon.Length > 6);
        Assert.Equal(new byte[] { 0, 0, 1, 0 }, icon[..4]);
        Assert.True(BitConverter.ToUInt16(icon, 4) >= 5);
    }
}
