using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Payroll;
using EunSlip.Core.Sending;

namespace EunSlip.Desktop.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly IGmailAuthorization _gmail;
    private readonly ISharedFileStore _stampStore;

    [ObservableProperty]
    private string? _connectedGmail;

    [ObservableProperty]
    private bool _hasGmailConnection;

    [ObservableProperty]
    private bool _hasStamp;

    [ObservableProperty]
    private string? _recentBatchSummary;

    [ObservableProperty]
    private string? _interruptedBatchNotice;

    public HomeViewModel(IGmailAuthorization gmail, ISharedFileStore stampStore)
    {
        _gmail = gmail;
        _stampStore = stampStore;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        GoogleAccount? account = await _gmail.RestoreAsync(cancellationToken);
        ConnectedGmail = account?.Email;
        HasGmailConnection = account is not null;
        HasStamp = _stampStore.GetActiveStampPath() is not null;
    }

    [RelayCommand]
    private async Task LoadedAsync()
    {
        await RefreshAsync(CancellationToken.None);
    }
}
