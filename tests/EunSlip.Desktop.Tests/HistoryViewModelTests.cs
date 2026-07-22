using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Recovery;
using EunSlip.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace EunSlip.Desktop.Tests;

public sealed class HistoryViewModelTests
{
    private sealed class FakeRepository : IAppRepository
    {
        public List<PayrollBatchRecord> Batches { get; } = [];
        public List<BatchRecipientRecord> Recipients { get; } = [];
        public List<SendAttemptRecord> Attempts { get; } = [];
        public HashSet<Guid> Deleted { get; } = [];
        public void Initialize() { }
        public bool CheckIntegrity() => true;
        public void ResetDatabase() { }
        public string? GetSetting(string key) => null;
        public void SetSetting(string key, string value) { }
        public Guid CreateBatch(PayrollBatchRecord batch) { Batches.Add(batch); return batch.Id; }
        public PayrollBatchRecord? GetBatch(Guid id) => Batches.FirstOrDefault(b => b.Id == id);
        public IReadOnlyList<PayrollBatchRecord> ListBatches() => Batches;
        public void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAt, DateTimeOffset? completedAt) { }
        public Guid AddRecipient(BatchRecipientRecord r) { Recipients.Add(r); return r.Id; }
        public IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId) =>
            [.. Recipients.Where(r => r.BatchId == batchId)];
        public IReadOnlyList<SendAttemptRecord> ListAttempts(Guid batchId)
        {
            HashSet<Guid> recipientIds = [.. Recipients.Where(recipient => recipient.BatchId == batchId)
                .Select(recipient => recipient.Id)];
            return [.. Attempts.Where(attempt => recipientIds.Contains(attempt.RecipientId))
                .OrderByDescending(attempt => attempt.StartedAtUtc)
                .ThenByDescending(attempt => attempt.AttemptNumber)];
        }
        public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAt) { }
        public void AddAttempt(SendAttemptRecord attempt) => Attempts.Add(attempt);
        public void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAt, string? errorCategory, string? errorMessage, string? gmailMessageId) { }
        public AttemptStatus? GetLatestAttemptStatus(Guid recipientId) => null;
        public IReadOnlyList<Guid> FindInterruptedBatches() => [];
        public void ResetSendingRecipientsToPending(Guid batchId) { }
        public IReadOnlyList<string> FindPreviouslySentNiks(string period) => [];
        public void DeleteBatch(Guid id) { _ = Deleted.Add(id); _ = Batches.RemoveAll(b => b.Id == id); }
    }

    private sealed class FakeRecovery : IRecoveryService
    {
        public List<Guid> Prepared { get; } = [];
        public List<Guid> FailedNikQueries { get; } = [];
        public IReadOnlyList<Guid> DetectInterruptedBatches() => [];
        public IReadOnlyList<Guid> MarkDetectedBatchesInterrupted() => [];
        public void PrepareForRecovery(Guid batchId) => Prepared.Add(batchId);
        public RecoveryGate VerifyFingerprint(Guid batchId, BatchContext context, IReadOnlyList<PayrollRow> rows) =>
            new(RecoveryGateResult.Match, "fp", "fp");
        public IReadOnlyList<string> SelectRetryFailedNiks(Guid batchId)
        {
            FailedNikQueries.Add(batchId);
            return ["NIK0002"];
        }
        public IReadOnlyList<string> SelectRecoveryResendNiks(Guid batchId) => ["NIK0001"];
    }

    private static HistoryViewModel Create(out FakeRepository repo, out FakeRecovery recovery)
    {
        repo = new FakeRepository();
        recovery = new FakeRecovery();
        return new HistoryViewModel(repo, NullLogger<HistoryViewModel>.Instance);
    }

    private static PayrollBatchRecord Batch(Guid id) => new(
        id, "JULY 2025", new DateOnly(2026, 5, 11), "fp", BatchStatus.Completed,
        DateTimeOffset.UtcNow, null, null, true, 2, 1, 1);

    [Fact]
    public void Loaded_PopulatesBatches()
    {
        HistoryViewModel vm = Create(out FakeRepository repo, out _);
        repo.CreateBatch(Batch(Guid.NewGuid()));

        vm.LoadedCommand.Execute(null);

        Assert.Single(vm.Batches);
    }

    [Fact]
    public void SelectingBatch_LoadsRecipients()
    {
        HistoryViewModel vm = Create(out FakeRepository repo, out _);
        Guid batchId = Guid.NewGuid();
        repo.CreateBatch(Batch(batchId));
        repo.AddRecipient(new BatchRecipientRecord(Guid.NewGuid(), batchId, "enc", "enc", "N1",
            RecipientStatus.Sent, DateTimeOffset.UtcNow));
        vm.LoadedCommand.Execute(null);

        vm.SelectedBatch = vm.Batches[0];

        Assert.Single(vm.SelectedRecipients);
    }

    [Fact]
    public void SelectingBatch_ProjectsLatestAttemptWithoutExposingEncryptedValues()
    {
        HistoryViewModel vm = Create(out FakeRepository repository, out _);
        PayrollBatchRecord batch = Batch(Guid.NewGuid());
        repository.CreateBatch(batch);
        Guid recipientId = repository.AddRecipient(new BatchRecipientRecord(Guid.NewGuid(), batch.Id,
            "encrypted-nik", "encrypted-email", "0004", RecipientStatus.Failed, DateTimeOffset.UtcNow));
        repository.Attempts.Add(new SendAttemptRecord(Guid.NewGuid(), recipientId, 1, AttemptType.Normal,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, AttemptStatus.Failed, "Network", "technical", null));
        vm.LoadedCommand.Execute(null);

        vm.SelectedBatch = batch;

        HistoryRecipientViewModel detail = Assert.Single(vm.SelectedRecipients);
        Assert.Equal("0004", detail.NikHint);
        Assert.Equal(AttemptType.Normal, detail.LatestAttemptType);
        Assert.Equal("Network", detail.ErrorCategory);
        Assert.DoesNotContain("encrypted", detail.ErrorSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Actions_AreEnabledOnlyForMatchingBatchStates()
    {
        HistoryViewModel vm = Create(out _, out _);
        PayrollBatchRecord retryable = Batch(Guid.NewGuid());
        PayrollBatchRecord interrupted = retryable with { Id = Guid.NewGuid(), Status = BatchStatus.Interrupted };
        PayrollBatchRecord clean = retryable with { Id = Guid.NewGuid(), FailedCount = 0 };

        Assert.True(vm.RetryFailedCommand.CanExecute(retryable));
        Assert.False(vm.RetryFailedCommand.CanExecute(clean));
        Assert.False(vm.RecoverCommand.CanExecute(retryable));
        Assert.True(vm.RecoverCommand.CanExecute(interrupted));
    }

    [Fact]
    public void RecoveryAction_RequestsWizardNavigationWithoutPreparingBatch()
    {
        HistoryViewModel vm = Create(out _, out FakeRecovery recovery);
        PayrollBatchRecord interrupted = Batch(Guid.NewGuid()) with { Status = BatchStatus.Interrupted };
        PayrollWizardEntry? requested = null;
        vm.ResumeRequested += entry => requested = entry;

        vm.RecoverCommand.Execute(interrupted);

        Assert.Equal(PayrollRunMode.RecoveryRetry, requested!.Mode);
        Assert.Equal(interrupted.Id, requested.BatchId);
        Assert.Empty(recovery.Prepared);
    }

    [Fact]
    public void RequestDelete_SetsConfirmFlag()
    {
        HistoryViewModel vm = Create(out FakeRepository repo, out _);
        PayrollBatchRecord batch = Batch(Guid.NewGuid());
        repo.CreateBatch(batch);
        vm.LoadedCommand.Execute(null);
        vm.SelectedBatch = batch;

        vm.RequestDeleteCommand.Execute(batch);

        Assert.True(vm.ConfirmDelete);
    }

    [Fact]
    public void ConfirmDelete_RemovesBatchAndClearsFlag()
    {
        HistoryViewModel vm = Create(out FakeRepository repo, out _);
        PayrollBatchRecord batch = Batch(Guid.NewGuid());
        repo.CreateBatch(batch);
        vm.LoadedCommand.Execute(null);
        vm.SelectedBatch = batch;
        vm.RequestDeleteCommand.Execute(batch);

        vm.ConfirmDeleteBatchCommand.Execute(null);

        Assert.Contains(batch.Id, repo.Deleted);
        Assert.Empty(vm.Batches);
        Assert.False(vm.ConfirmDelete);
    }

    [Fact]
    public void CancelDelete_ClearsFlag()
    {
        HistoryViewModel vm = Create(out _, out _);
        vm.ConfirmDelete = true;

        vm.CancelDeleteCommand.Execute(null);

        Assert.False(vm.ConfirmDelete);
    }
}
