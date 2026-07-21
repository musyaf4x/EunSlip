using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Persistence;
using EunSlip.Core.Recovery;
using Microsoft.Extensions.Logging;

namespace EunSlip.Desktop.ViewModels;

public sealed partial class HistoryViewModel(
    IAppRepository repository, IRecoveryService recovery, ILogger<HistoryViewModel> logger) : ViewModelBase
{
    private readonly IAppRepository _repository = repository;
    private readonly IRecoveryService _recovery = recovery;
    private readonly ILogger<HistoryViewModel> _logger = logger;

    public ObservableCollection<PayrollBatchRecord> Batches { get; } = [];
    public ObservableCollection<BatchRecipientRecord> SelectedRecipients { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryFailedCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecoverCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeleteCommand))]
    private PayrollBatchRecord? _selectedBatch;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _confirmDelete;

    [RelayCommand]
    private void Loaded()
    {
        try
        {
            Batches.Clear();
            foreach (PayrollBatchRecord batch in _repository.ListBatches())
            {
                Batches.Add(batch);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history");
        }
    }

    partial void OnSelectedBatchChanged(PayrollBatchRecord? value)
    {
        SelectedRecipients.Clear();
        if (value is null)
        {
            return;
        }

        try
        {
            foreach (BatchRecipientRecord recipient in _repository.ListRecipients(value.Id))
            {
                SelectedRecipients.Add(recipient);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load batch recipients");
        }
    }

    [RelayCommand(CanExecute = nameof(CanActOnSelectedBatch))]
    private void RetryFailed(PayrollBatchRecord batch)
    {
        try
        {
            IReadOnlyList<string> failedNiks = _recovery.SelectRetryFailedNiks(batch.Id);
            StatusMessage = failedNiks.Count == 0
                ? "Tidak ada penerima gagal untuk dikirim ulang."
                : $"{failedNiks.Count} penerima gagal siap dikirim ulang. Pilih file payroll yang sama untuk melanjutkan.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retry-failed selection failed");
            StatusMessage = "Gagal menyiapkan kirim ulang.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanActOnSelectedBatch))]
    private void Recover(PayrollBatchRecord batch)
    {
        try
        {
            _recovery.PrepareForRecovery(batch.Id);
            StatusMessage = "Batch disiapkan untuk pemulihan. Pilih file payroll yang sama untuk melanjutkan.";
            Loaded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recovery preparation failed");
            StatusMessage = "Gagal menyiapkan pemulihan.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanActOnSelectedBatch))]
    private void RequestDelete(PayrollBatchRecord batch)
    {
        ConfirmDelete = true;
    }

    [RelayCommand]
    private void ConfirmDeleteBatch()
    {
        if (SelectedBatch is null)
        {
            return;
        }

        try
        {
            _repository.DeleteBatch(SelectedBatch.Id);
            _ = Batches.Remove(SelectedBatch);
            SelectedBatch = null;
            SelectedRecipients.Clear();
            ConfirmDelete = false;
            StatusMessage = "Batch dihapus permanen.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete batch failed");
            StatusMessage = "Gagal menghapus batch.";
        }
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ConfirmDelete = false;
    }

    private bool CanActOnSelectedBatch(PayrollBatchRecord batch) => batch is not null;
}
