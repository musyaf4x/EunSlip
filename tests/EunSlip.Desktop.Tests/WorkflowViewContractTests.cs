using System.IO;
using System.Text;

namespace EunSlip.Desktop.Tests;

public sealed class WorkflowViewContractTests
{
    private static string ReadFile(params string[] parts)
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
    public void WizardView_InvokesLoadedCommand()
    {
        string code = ReadFile("src", "EunSlip.Desktop", "Views", "WizardView.xaml.cs");

        Assert.Contains("LoadedCommand.ExecuteAsync", code, StringComparison.Ordinal);
    }
}
