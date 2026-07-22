using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void NavigationCommands_UpdatePersistentActiveSection()
    {
        ShellFixture fixture = ShellFixture.Create();
        MainViewModel vm = fixture.Main;

        Assert.Equal(NavigationSection.Home, vm.CurrentSection);
        Assert.True(vm.IsHomeActive);

        vm.GoHistoryCommand.Execute(null);

        Assert.Equal(NavigationSection.History, vm.CurrentSection);
        Assert.True(vm.IsHistoryActive);
        Assert.False(vm.IsHomeActive);
    }

    [Fact]
    public void SettingsAndAboutCommands_UpdateTheirActiveFlags()
    {
        ShellFixture fixture = ShellFixture.Create();
        MainViewModel vm = fixture.Main;

        vm.GoSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsActive);

        vm.GoAboutCommand.Execute(null);
        Assert.True(vm.IsAboutActive);
        Assert.False(vm.IsSettingsActive);
    }

    [Fact]
    public void HistoryResumeRequest_OpensWizardWithOriginalModeAndBatch()
    {
        ShellFixture fixture = ShellFixture.Create();
        Guid batchId = fixture.Repository.CreateBatch(fixture.InterruptedBatch());

        fixture.History.RecoverCommand.Execute(fixture.Repository.GetBatch(batchId));

        Assert.Equal(NavigationSection.Payroll, fixture.Main.CurrentSection);
        Assert.Equal(PayrollRunMode.RecoveryRetry, fixture.Wizard.RunMode);
    }

    [Fact]
    public void SendingState_DisablesNavigationAndClose()
    {
        ShellFixture fixture = ShellFixture.Create();
        fixture.Wizard.CurrentStep = WizardStep.Send;
        fixture.Wizard.IsBusy = true;

        Assert.False(fixture.Main.GoHomeCommand.CanExecute(null));
        Assert.False(fixture.Main.GoHistoryCommand.CanExecute(null));
        Assert.False(fixture.Main.CanClose);
    }
}
