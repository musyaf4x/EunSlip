using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Sending;
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
        await RefreshGmailAsync();
        HasStamp = _stampStore.GetActiveStampPath() is not null;
        string? savedLanguage = _repository.GetSetting(SettingUiLanguage);
        if (!string.IsNullOrEmpty(savedLanguage))
        {
            SelectedLanguage = savedLanguage;
        }
        HasOAuthSecret = !string.IsNullOrWhiteSpace(_repository.GetSetting(SettingClientSecret));
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
            OauthClientSecretJson = string.Empty;
            StatusMessage = "Kredensial OAuth disimpan (terenkripsi DPAPI).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth secret save failed");
            StatusMessage = "Gagal menyimpan kredensial OAuth.";
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail refresh failed");
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnectGmail))]
    private async Task ConnectGmailAsync()
    {
        if (!HasOAuthSecret)
        {
            StatusMessage = "Simpan kredensial OAuth di bawah ini dulu, lalu klik Hubungkan.";
            return;
        }

        IsConnecting = true;
        StatusMessage = "Membuka browser untuk otorisasi Gmail…";
        try
        {
            byte[]? secretEnvelope = LoadSecretEnvelope();
            if (secretEnvelope is null)
            {
                StatusMessage = "Kredensial OAuth rusak. Tempel ulang dan simpan.";
                return;
            }

            byte[] secretBytes = DpapiKeyProtector.UnprotectToken(secretEnvelope);
            string clientSecret = Encoding.UTF8.GetString(secretBytes);
            GoogleAccount? account = await _gmail.ConnectAsync(clientSecret, CancellationToken.None);
            ConnectedGmail = account?.Email;
            HasGmailConnection = account is not null;
            StatusMessage = HasGmailConnection
                ? $"Gmail terhubung: {account?.Email}"
                : "Gagal menghubungkan Gmail. Periksa kredensial / jaringan.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail connect failed");
            StatusMessage = "Gagal menghubungkan Gmail: " + ex.Message;
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
