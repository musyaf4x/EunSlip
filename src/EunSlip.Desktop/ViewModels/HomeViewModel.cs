using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Sending;
using EunSlip.Desktop.Localization;

namespace EunSlip.Desktop.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly IGmailAuthorization _gmail;
    private readonly ISharedFileStore _stampStore;
    private readonly IAppRepository _repository;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _connectedGmail;

    [ObservableProperty]
    private bool _hasGmailConnection;

    [ObservableProperty]
    private bool _hasStamp;

    [ObservableProperty]
    private string _gmailStatusText = Strings.Get("StatusChecking");

    [ObservableProperty]
    private string _stampStatusText = Strings.Get("StatusChecking");

    [ObservableProperty]
    private string _activeLanguage = "id-ID";

    [ObservableProperty]
    private string? _recentBatchSummary;

    [ObservableProperty]
    private string? _interruptedBatchNotice;

    public bool HasInterruptedBatch => !string.IsNullOrEmpty(InterruptedBatchNotice);

    public HomeViewModel(
        IGmailAuthorization gmail,
        ISharedFileStore stampStore,
        IAppRepository repository)
    {
        _gmail = gmail;
        _stampStore = stampStore;
        _repository = repository;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        GmailStatusText = Strings.Get("StatusChecking");
        StampStatusText = Strings.Get("StatusChecking");
        try
        {
            GoogleAccount? account = await _gmail.RestoreAsync(cancellationToken);
            ConnectedGmail = account?.Email;
            HasGmailConnection = account is not null;
            HasStamp = _stampStore.GetActiveStampPath() is not null;
            GmailStatusText = HasGmailConnection
                ? Strings.Get("StatusReady")
                : Strings.Get("StatusNotReady");
            StampStatusText = HasStamp
                ? Strings.Get("StatusReady")
                : Strings.Get("StatusNotReady");
            ActiveLanguage = _repository.GetSetting("UiLanguage") ?? "id-ID";

            IReadOnlyList<PayrollBatchRecord> batches = _repository.ListBatches();
            PayrollBatchRecord? latest = batches
                .OrderByDescending(batch => batch.CreatedAtUtc)
                .FirstOrDefault();
            RecentBatchSummary = latest is null
                ? Strings.Get("HomeNoRecentBatch")
                : $"{latest.Period} · {latest.Status} · {latest.SentCount}/{latest.RecipientCount}";

            PayrollBatchRecord? interrupted = batches
                .Where(batch => batch.Status == BatchStatus.Interrupted)
                .OrderByDescending(batch => batch.CreatedAtUtc)
                .FirstOrDefault();
            InterruptedBatchNotice = interrupted is null
                ? null
                : Strings.Get("HomeInterruptedBatch")
                    .Replace("{0}", interrupted.Period, StringComparison.Ordinal);
            OnPropertyChanged(nameof(HasInterruptedBatch));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadedAsync()
    {
        await RefreshAsync(CancellationToken.None);
    }
}
