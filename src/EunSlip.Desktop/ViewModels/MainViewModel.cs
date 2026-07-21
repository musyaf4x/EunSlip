using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Desktop.Localization;

namespace EunSlip.Desktop.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly HomeViewModel _home;
    private readonly PayrollWizardViewModel _wizard;
    private readonly HistoryViewModel _history;
    private readonly SettingsViewModel _settings;
    private readonly AboutViewModel _about;

    [ObservableProperty]
    private ViewModelBase _current;

    public MainViewModel(
        HomeViewModel home,
        PayrollWizardViewModel wizard,
        HistoryViewModel history,
        SettingsViewModel settings,
        AboutViewModel about)
    {
        _home = home;
        _wizard = wizard;
        _history = history;
        _settings = settings;
        _about = about;
        _current = home;
    }

    public string NavHome => Strings.Get("Nav_Home");
    public string NavPayrollProcess => Strings.Get("Nav_PayrollProcess");
    public string NavHistory => Strings.Get("Nav_History");
    public string NavSettings => Strings.Get("Nav_Settings");
    public string NavAbout => Strings.Get("Nav_About");

    [RelayCommand]
    private void GoHome() => Current = _home;

    [RelayCommand]
    private void GoPayroll()
    {
        _wizard.Reset();
        Current = _wizard;
    }

    [RelayCommand]
    private void GoHistory() => Current = _history;

    [RelayCommand]
    private void GoSettings() => Current = _settings;

    [RelayCommand]
    private void GoAbout() => Current = _about;
}
