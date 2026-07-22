using System.IO;

namespace EunSlip.Desktop.Tests;

public sealed class InstallerContractTests
{
    private static string ReadRepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EunSlip.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory.FullName, .. parts]));
    }

    [Fact]
    public void ProductionInstaller_IsAdminX64AndPreservesSharedData()
    {
        string script = ReadRepositoryFile("installer", "EunSlip.iss");

        Assert.Contains("AppId={{A56A07F0-3CFA-4DDC-AE5B-0F298EE5B609}", script, StringComparison.Ordinal);
        Assert.Contains("PrivilegesRequired={#InstallerPrivileges}", script, StringComparison.Ordinal);
        Assert.Contains("#define InstallerPrivileges \"admin\"", script, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesAllowed=x64compatible", script, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesInstallIn64BitMode=x64compatible", script, StringComparison.Ordinal);
        Assert.Contains("{commonappdata}\\EunSlip", script, StringComparison.Ordinal);
        Assert.Contains("Permissions: users-modify", script, StringComparison.Ordinal);
        Assert.Contains("OutputBaseFilename={#InstallerBaseName}", script, StringComparison.Ordinal);
        Assert.Contains("#define InstallerBaseName \"EunSlip-Setup-x64\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Type: filesandordirs; Name: \"{commonappdata}\\EunSlip", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildScript_PublishesSelfContainedWinX64AndCompilesExpectedSetup()
    {
        string script = ReadRepositoryFile("scripts", "Build-Installer.ps1");

        Assert.Contains("dotnet", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("publish", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("win-x64", script, StringComparison.Ordinal);
        Assert.Contains("--self-contained", script, StringComparison.Ordinal);
        Assert.Contains("ISCC.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EunSlip-Setup-x64.exe", script, StringComparison.Ordinal);
        Assert.Contains("$env:LOCALAPPDATA", script, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopProject_PublishesRequiredExecutableNameAndVersion()
    {
        string project = ReadRepositoryFile("src", "EunSlip.Desktop", "EunSlip.Desktop.csproj");

        Assert.Contains("<AssemblyName>EunSlip</AssemblyName>", project, StringComparison.Ordinal);
        Assert.Contains("<Version>1.0.0</Version>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void LifecycleTest_UsesSandboxAndChecksPreservationAcrossUpgradeAndUninstall()
    {
        string script = ReadRepositoryFile("scripts", "Test-InstallerLifecycle.ps1");

        Assert.Contains("SandboxRoot", script, StringComparison.Ordinal);
        Assert.Contains("SharedDataRoot", script, StringComparison.Ordinal);
        Assert.Contains("1.0.0", script, StringComparison.Ordinal);
        Assert.Contains("1.0.1", script, StringComparison.Ordinal);
        Assert.Contains("unins000.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("database", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("oauth", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stamp", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Assert-Preserved", script, StringComparison.Ordinal);
        Assert.Contains("$env:LOCALAPPDATA", script, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalGuides_CoverOAuthDeploymentAndDataPreservation()
    {
        string oauth = ReadRepositoryFile("docs", "google-cloud-setup.md");
        string deployment = ReadRepositoryFile("docs", "deployment-guide.md");

        Assert.Contains("Gmail API", oauth, StringComparison.Ordinal);
        Assert.Contains("Google Auth Platform", oauth, StringComparison.Ordinal);
        Assert.Contains("Internal", oauth, StringComparison.Ordinal);
        Assert.Contains("Desktop app", oauth, StringComparison.Ordinal);
        Assert.Contains("gmail.send", oauth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("client_secret.json", oauth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jangan", oauth, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("EunSlip-Setup-x64.exe", deployment, StringComparison.Ordinal);
        Assert.Contains("Windows 10", deployment, StringComparison.Ordinal);
        Assert.Contains("Windows 11", deployment, StringComparison.Ordinal);
        Assert.Contains("administrator", deployment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SmartScreen", deployment, StringComparison.Ordinal);
        Assert.Contains("upgrade", deployment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uninstall", deployment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C:\\ProgramData\\EunSlip", deployment, StringComparison.Ordinal);
    }
}
