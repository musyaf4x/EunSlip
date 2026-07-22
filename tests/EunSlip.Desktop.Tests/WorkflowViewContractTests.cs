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

    [Fact]
    public void WizardView_OpensOnlyPathGeneratedByViewModel()
    {
        string code = ReadFile("src", "EunSlip.Desktop", "Views", "WizardView.xaml.cs");
        Assert.Contains("vm.GeneratePreviewPdf()", code, StringComparison.Ordinal);
        Assert.Contains("UseShellExecute = true", code, StringComparison.Ordinal);
        string viewModel = ReadFile("src", "EunSlip.Desktop", "ViewModels", "PayrollWizardViewModel.cs");
        Assert.DoesNotContain("Process.Start", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void WizardView_HasSixStepLabelsAndNoRawBooleanBindings()
    {
        string xaml = ReadFile("src", "EunSlip.Desktop", "Views", "WizardView.xaml");
        foreach (string resourceKey in new[]
        {
            "Wizard_StepSelect", "Wizard_StepValidate", "Wizard_StepEmail",
            "Wizard_StepConfirm", "Wizard_StepSend", "Wizard_StepResults",
        })
        {
            Assert.Contains($"{{loc:Loc {resourceKey}}}", xaml, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Text=\"{Binding HasGmailConnection", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding HasStamp", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding GmailReady", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding StampReady", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("WizardView.xaml")]
    [InlineData("HistoryView.xaml")]
    public void RedesignedViews_ApplyPageFrameAsStyle(string fileName)
    {
        string xaml = ReadFile("src", "EunSlip.Desktop", "Views", fileName);

        Assert.DoesNotContain("Margin=\"{StaticResource PageFrame}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource PageFrame}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WizardView_ReadOnlyRunBindingsAreOneWay()
    {
        string xaml = ReadFile("src", "EunSlip.Desktop", "Views", "WizardView.xaml");

        Assert.DoesNotContain("Run Text=\"{Binding RecipientCount}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Run Text=\"{Binding GmailStatusText}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Run Text=\"{Binding StampStatusText}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WizardPreview_UsesWrappedSupportingCopy()
    {
        string xaml = ReadFile("src", "EunSlip.Desktop", "Views", "WizardView.xaml");

        Assert.Matches(
            "Text=\"\\{loc:Loc Wizard_PreviewDescription\\}\"[\\s\\S]{0,160}TextWrapping=\"Wrap\"",
            xaml);
    }

    [Fact]
    public void HistoryView_SeparatesActionsAndFitsDetailColumns()
    {
        string xaml = ReadFile("src", "EunSlip.Desktop", "Views", "HistoryView.xaml");

        Assert.Contains("<ColumnDefinition Width=\"2*\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"3*\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<StackPanel Grid.Row=\"1\" Orientation=\"Horizontal\"", xaml, StringComparison.Ordinal);
        Assert.Equal(2, xaml.Split("ScrollViewer.HorizontalScrollBarVisibility=\"Disabled\"").Length - 1);
        Assert.Contains("Header=\"{loc:Loc Grid_Period}\" Binding=\"{Binding Period}\" Width=\"14*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"{loc:Loc Grid_Status}\" Binding=\"{Binding Status, Converter={StaticResource StatusText}}\" Width=\"10*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"{loc:Loc Grid_Sent}\" Binding=\"{Binding SentCount}\" Width=\"7*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"{loc:Loc Grid_Failed}\" Binding=\"{Binding FailedCount}\" Width=\"9*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"{loc:Loc Grid_Time}\" Binding=\"{Binding LatestAttemptCompletedAtUtc}\" Width=\"14*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"{loc:Loc Grid_Detail}\" Binding=\"{Binding ErrorSummary}\" Width=\"16*\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowViews_DefineStableAutomationIds()
    {
        string combined = string.Join("\n",
            ReadFile("src", "EunSlip.Desktop", "Views", "WizardView.xaml"),
            ReadFile("src", "EunSlip.Desktop", "Views", "HistoryView.xaml"));

        foreach (string id in new[]
        {
            "PayrollFilePath", "PickPayrollFile", "PayrollPeriod", "PaymentDate",
            "ValidationGrid", "PreviewPdf", "ConfirmSend", "WizardBack", "WizardNext", "ResultsGrid",
            "BatchGrid", "RecipientGrid", "RetryFailed", "RecoverInterrupted",
            "RequestDelete", "ConfirmDelete", "CancelDelete",
        })
        {
            Assert.Contains($"AutomationProperties.AutomationId=\"{id}\"", combined, StringComparison.Ordinal);
        }
    }
}
