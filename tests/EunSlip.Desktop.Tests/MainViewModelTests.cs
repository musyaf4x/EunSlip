using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void NavigationCommands_UpdatePersistentActiveSection()
    {
        MainViewModel vm = new(null!, null!, null!, null!, null!);

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
        MainViewModel vm = new(null!, null!, null!, null!, null!);

        vm.GoSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsActive);

        vm.GoAboutCommand.Execute(null);
        Assert.True(vm.IsAboutActive);
        Assert.False(vm.IsSettingsActive);
    }
}
