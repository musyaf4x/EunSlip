using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Persistence;
using EunSlip.Desktop.Localization;
using Microsoft.Extensions.Logging;

namespace EunSlip.Desktop.ViewModels;

public sealed partial class HistoryViewModel(
    IAppRepository repository, ILogger<HistoryViewModel> logger) : ViewModelBase
{
    private readonly IAppRepository _repository = repository;
    private readonly ILogger<HistoryViewModel> _logger = logger;

    public ObservableCollection<PayrollBatchRecord> Batches { get; } = [];
    public ObservableCollection<HistoryRecipientViewModel> SelectedRecipients { get; } = [];

    public event Action<PayrollWizardEntry>? ResumeRequested;

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
            Guid? selectedBatchId = SelectedBatch?.Id;
            Batches.Clear();
            foreach (PayrollBatchRecord batch in _repository.ListBatches())
            {
                Batches.Add(batch);
            }

            if (selectedBatchId is Guid id)
            {
                SelectedBatch = Batches.FirstOrDefault(batch => batch.Id == id);
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
            Dictionary<Guid, SendAttemptRecord> latestAttempts = _repository.ListAttempts(value.Id)
                .GroupBy(attempt => attempt.RecipientId)
                .ToDictionary(group => group.Key, group => group
                    .OrderByDescending(attempt => attempt.StartedAtUtc)
                    .ThenByDescending(attempt => attempt.AttemptNumber)
                    .First());

            foreach (BatchRecipientRecord recipient in _repository.ListRecipients(value.Id))
            {
                _ = latestAttempts.TryGetValue(recipient.Id, out SendAttemptRecord? latest);
                SelectedRecipients.Add(new HistoryRecipientViewModel(recipient, latest));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load batch recipient details");
            StatusMessage = Strings.Get("HistoryDetailLoadFailed");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRetryFailed))]
    private void RetryFailed(PayrollBatchRecord? batch)
    {
        if (batch is not null)
        {
            ResumeRequested?.Invoke(PayrollWizardEntry.FailedRetry(batch.Id));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRecover))]
    private void Recover(PayrollBatchRecord? batch)
    {
        if (batch is not null)
        {
            ResumeRequested?.Invoke(PayrollWizardEntry.RecoveryRetry(batch.Id));
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
            StatusMessage = Strings.Get("History_BatchDeleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete batch failed");
            StatusMessage = Strings.Get("History_BatchDeleteFailed");
        }
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ConfirmDelete = false;
    }

    private bool CanActOnSelectedBatch(PayrollBatchRecord batch) => batch is not null;

    private static bool CanRetryFailed(PayrollBatchRecord? batch) =>
        batch is { Status: BatchStatus.Completed, FailedCount: > 0 };

    private static bool CanRecover(PayrollBatchRecord? batch) =>
        batch is { Status: BatchStatus.Interrupted };
}
