using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Payroll;
using EunSlip.Core.Sending;
using Microsoft.Extensions.Logging;

namespace EunSlip.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IGmailAuthorization _gmail;
    private readonly ISharedFileStore _stampStore;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private string? _connectedGmail;

    [ObservableProperty]
    private bool _hasGmailConnection;

    [ObservableProperty]
    private bool _hasStamp;

    [ObservableProperty]
    private string _selectedLanguage = "id-ID";

    public SettingsViewModel(IGmailAuthorization gmail, ISharedFileStore stampStore, ILogger<SettingsViewModel> logger)
    {
        _gmail = gmail;
        _stampStore = stampStore;
        _logger = logger;
    }

    [RelayCommand]
    private async Task RefreshGmailAsync()
    {
        try
        {
            GoogleAccount? account = await _gmail.RestoreAsync(CancellationToken.None);
            ConnectedGmail = account?.Email;
            HasGmailConnection = account is not null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail refresh failed");
        }
    }

    [RelayCommand]
    private void PickStamp(string filePath)
    {
        try
        {
            _ = _stampStore.ImportStamp(filePath);
            HasStamp = true;
        }
        catch (StampValidationException ex)
        {
            _logger.LogWarning("Stamp import rejected: {Message}", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DisconnectGmailAsync()
    {
        await _gmail.DisconnectAsync(CancellationToken.None);
        HasGmailConnection = false;
        ConnectedGmail = null;
    }
}
