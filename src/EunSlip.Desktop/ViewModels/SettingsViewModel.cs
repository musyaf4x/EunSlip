using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Sending;
using Microsoft.Extensions.Logging;

namespace EunSlip.Desktop.ViewModels;

public sealed partial class SettingsViewModel(
    IGmailAuthorization gmail,
    ISharedFileStore stampStore,
    IAppRepository repository,
    ILogger<SettingsViewModel> logger) : ViewModelBase
{
    private const string SettingUiLanguage = "UiLanguage";
    private const string SettingClientSecret = "OAuthClientSecret";

    private readonly IGmailAuthorization _gmail = gmail;
    private readonly ISharedFileStore _stampStore = stampStore;
    private readonly IAppRepository _repository = repository;
    private readonly ILogger<SettingsViewModel> _logger = logger;

    [ObservableProperty]
    private string? _connectedGmail;

    [ObservableProperty]
    private bool _hasGmailConnection;

    [ObservableProperty]
    private bool _hasStamp;

    [ObservableProperty]
    private string _selectedLanguage = "id-ID";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _languageChangedRequiresRestart;

    [RelayCommand]
    private async Task LoadedAsync()
    {
        await RefreshGmailAsync();
        HasStamp = _stampStore.GetActiveStampPath() is not null;
        string? savedLanguage = _repository.GetSetting(SettingUiLanguage);
        if (!string.IsNullOrEmpty(savedLanguage))
        {
            SelectedLanguage = savedLanguage;
        }
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
    private async Task ConnectGmailAsync()
    {
        string? clientSecret = _repository.GetSetting(SettingClientSecret);
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            StatusMessage = "Kredensial OAuth belum dikonfigurasi. Hubungi IT.";
            return;
        }

        try
        {
            GoogleAccount? account = await _gmail.ConnectAsync(clientSecret, CancellationToken.None);
            ConnectedGmail = account?.Email;
            HasGmailConnection = account is not null;
            StatusMessage = HasGmailConnection ? "Gmail terhubung." : "Gagal menghubungkan Gmail.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail connect failed");
            StatusMessage = "Gagal menghubungkan Gmail.";
        }
    }

    [RelayCommand]
    private async Task DisconnectGmailAsync()
    {
        try
        {
            await _gmail.DisconnectAsync(CancellationToken.None);
            HasGmailConnection = false;
            ConnectedGmail = null;
            StatusMessage = "Gmail diputus. Riwayat tetap dipertahankan.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail disconnect failed");
        }
    }

    [RelayCommand]
    private void PickStamp(string filePath)
    {
        try
        {
            _ = _stampStore.ImportStamp(filePath);
            HasStamp = true;
            StatusMessage = "Stamp diperbarui.";
        }
        catch (StampValidationException ex)
        {
            StatusMessage = ex.Message;
            _logger.LogWarning("Stamp import rejected: {Message}", ex.Message);
        }
    }

    [RelayCommand]
    private void RemoveStamp()
    {
        _stampStore.RemoveStamp();
        HasStamp = false;
        StatusMessage = "Stamp dihapus.";
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        _repository.SetSetting(SettingUiLanguage, value);
        LanguageChangedRequiresRestart = true;
        StatusMessage = "Bahasa disimpan. Mulai ulang aplikasi untuk menerapkan.";
    }
}
