using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Desktop.Localization;

namespace EunSlip.Desktop.ViewModels;

public enum NavigationSection { Home, Payroll, History, Settings, About }

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly HomeViewModel _home;
    private readonly PayrollWizardViewModel _wizard;
    private readonly HistoryViewModel _history;
    private readonly SettingsViewModel _settings;
    private readonly AboutViewModel _about;

    [ObservableProperty]
    private ViewModelBase _current;

    [ObservableProperty]
    private NavigationSection _currentSection = NavigationSection.Home;

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

        _history.ResumeRequested += OpenWizard;
        _wizard.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PayrollWizardViewModel.IsSending))
            {
                OnPropertyChanged(nameof(CanClose));
                GoHomeCommand.NotifyCanExecuteChanged();
                GoPayrollCommand.NotifyCanExecuteChanged();
                GoHistoryCommand.NotifyCanExecuteChanged();
                GoSettingsCommand.NotifyCanExecuteChanged();
                GoAboutCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public string NavHome => Strings.Get("Nav_Home");
    public string NavPayrollProcess => Strings.Get("Nav_PayrollProcess");
    public string NavHistory => Strings.Get("Nav_History");
    public string NavSettings => Strings.Get("Nav_Settings");
    public string NavAbout => Strings.Get("Nav_About");
    public string ActiveLanguage => CultureInfo.CurrentUICulture.Name;
    public string VersionText => "v1.0.0";
    public bool IsHomeActive => CurrentSection == NavigationSection.Home;
    public bool IsPayrollActive => CurrentSection == NavigationSection.Payroll;
    public bool IsHistoryActive => CurrentSection == NavigationSection.History;
    public bool IsSettingsActive => CurrentSection == NavigationSection.Settings;
    public bool IsAboutActive => CurrentSection == NavigationSection.About;
    public bool CanClose => !_wizard.IsSending;

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void GoHome() => Navigate(_home, NavigationSection.Home);

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void GoPayroll()
    {
        OpenWizard(PayrollWizardEntry.Normal());
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void GoHistory() => Navigate(_history, NavigationSection.History);

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void GoSettings() => Navigate(_settings, NavigationSection.Settings);

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void GoAbout() => Navigate(_about, NavigationSection.About);

    private bool CanNavigate() => !_wizard.IsSending;

    private void OpenWizard(PayrollWizardEntry entry)
    {
        if (_wizard.Begin(entry))
        {
            Navigate(_wizard, NavigationSection.Payroll);
        }
    }

    private void Navigate(ViewModelBase target, NavigationSection section)
    {
        Current = target;
        CurrentSection = section;
    }

    partial void OnCurrentSectionChanged(NavigationSection value)
    {
        OnPropertyChanged(nameof(IsHomeActive));
        OnPropertyChanged(nameof(IsPayrollActive));
        OnPropertyChanged(nameof(IsHistoryActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsAboutActive));
    }
}
