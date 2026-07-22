using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Sending;
using EunSlip.Desktop.Localization;
using EunSlip.Infrastructure.Security;
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
    private bool _isLoading;

    [ObservableProperty]
    private string _gmailStatusText = Strings.Get("StatusChecking");

    [ObservableProperty]
    private string _stampStatusText = Strings.Get("StatusChecking");

    [ObservableProperty]
    private string _oauthStatusText = Strings.Get("StatusChecking");

    [ObservableProperty]
    private bool _isRemoveStampConfirmationVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveOAuthSecretCommand))]
    private string _oauthClientSecretJson = string.Empty;

    [ObservableProperty]
    private bool _hasOAuthSecret;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectGmailCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectGmailCommand))]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _languageChangedRequiresRestart;

    [RelayCommand]
    private async Task LoadedAsync()
    {
        IsLoading = true;
        GmailStatusText = Strings.Get("StatusChecking");
        StampStatusText = Strings.Get("StatusChecking");
        OauthStatusText = Strings.Get("StatusChecking");
        try
        {
            await RefreshGmailAsync();
            HasStamp = _stampStore.GetActiveStampPath() is not null;
            StampStatusText = HasStamp ? Strings.Get("StatusReady") : Strings.Get("StatusNotReady");
            string? savedLanguage = _repository.GetSetting(SettingUiLanguage);
            if (!string.IsNullOrEmpty(savedLanguage))
            {
                SelectedLanguage = savedLanguage;
            }
            HasOAuthSecret = !string.IsNullOrWhiteSpace(_repository.GetSetting(SettingClientSecret));
            OauthStatusText = HasOAuthSecret ? Strings.Get("StatusReady") : Strings.Get("StatusNotReady");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveOAuthSecret))]
    private void SaveOAuthSecret()
    {
        try
        {
            byte[] secretBytes = Encoding.UTF8.GetBytes(OauthClientSecretJson);
            byte[] envelope = DpapiKeyProtector.ProtectToken(secretBytes);
            _repository.SetSetting(SettingClientSecret, Convert.ToBase64String(envelope));
            HasOAuthSecret = true;
            OauthStatusText = Strings.Get("StatusReady");
            OauthClientSecretJson = string.Empty;
            StatusMessage = Strings.Get("Settings_OAuthSaved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth secret save failed");
            StatusMessage = Strings.Get("Settings_OAuthSaveFailed");
        }
    }

    private bool CanSaveOAuthSecret() => !string.IsNullOrWhiteSpace(OauthClientSecretJson);

    [RelayCommand]
    private async Task RefreshGmailAsync()
    {
        try
        {
            GoogleAccount? account = await _gmail.RestoreAsync(CancellationToken.None);
            ConnectedGmail = account?.Email;
            HasGmailConnection = account is not null;
            GmailStatusText = HasGmailConnection
                ? Strings.Get("StatusReady")
                : Strings.Get("StatusNotReady");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail refresh failed");
            GmailStatusText = Strings.Get("StatusNotReady");
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnectGmail))]
    private async Task ConnectGmailAsync()
    {
        if (!HasOAuthSecret)
        {
            StatusMessage = Strings.Get("Settings_SaveOAuthFirst");
            return;
        }

        IsConnecting = true;
        StatusMessage = Strings.Get("Settings_OpeningBrowser");
        try
        {
            byte[]? secretEnvelope = LoadSecretEnvelope();
            if (secretEnvelope is null)
            {
                StatusMessage = Strings.Get("Settings_OAuthCorrupt");
                return;
            }

            byte[] secretBytes = DpapiKeyProtector.UnprotectToken(secretEnvelope);
            string clientSecret = Encoding.UTF8.GetString(secretBytes);
            GoogleAccount? account = await _gmail.ConnectAsync(clientSecret, CancellationToken.None);
            ConnectedGmail = account?.Email;
            HasGmailConnection = account is not null;
            GmailStatusText = HasGmailConnection
                ? Strings.Get("StatusReady")
                : Strings.Get("StatusNotReady");
            StatusMessage = HasGmailConnection
                ? Strings.Get("Settings_GmailConnectedFormat")
                    .Replace("{0}", account?.Email, StringComparison.Ordinal)
                : Strings.Get("Settings_GmailConnectFailed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail connect failed");
            StatusMessage = Strings.Get("Settings_GmailConnectErrorFormat")
                .Replace("{0}", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private bool CanConnectGmail() => !IsConnecting;

    private byte[]? LoadSecretEnvelope()
    {
        string? stored = _repository.GetSetting(SettingClientSecret);
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(stored);
        }
        catch (FormatException)
        {
            _logger.LogWarning("OAuth client secret envelope is malformed");
            return null;
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
            GmailStatusText = Strings.Get("StatusNotReady");
            StatusMessage = Strings.Get("Settings_GmailDisconnected");
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
            StampStatusText = Strings.Get("StatusReady");
            StatusMessage = Strings.Get("Settings_StampUpdated");
        }
        catch (StampValidationException ex)
        {
            StatusMessage = ex.Message;
            _logger.LogWarning("Stamp import rejected: {Message}", ex.Message);
        }
    }

    [RelayCommand]
    private void RequestRemoveStamp() => IsRemoveStampConfirmationVisible = true;

    [RelayCommand]
    private void CancelRemoveStamp() => IsRemoveStampConfirmationVisible = false;

    [RelayCommand]
    private void ConfirmRemoveStamp()
    {
        _stampStore.RemoveStamp();
        HasStamp = false;
        StampStatusText = Strings.Get("StatusNotReady");
        IsRemoveStampConfirmationVisible = false;
        StatusMessage = Strings.Get("SettingsStampRemoved");
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        _repository.SetSetting(SettingUiLanguage, value);
        LanguageChangedRequiresRestart = true;
        StatusMessage = Strings.Get("Settings_LanguageSaved");
    }
}
