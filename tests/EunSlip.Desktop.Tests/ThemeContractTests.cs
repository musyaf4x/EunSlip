using System.IO;
using System.Text;

namespace EunSlip.Desktop.Tests;

public sealed class ThemeContractTests
{
    private static string ReadRepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EunSlip.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory.FullName, .. parts]), Encoding.UTF8);
    }

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
}
