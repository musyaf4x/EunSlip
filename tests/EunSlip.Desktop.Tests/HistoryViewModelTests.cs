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
        public IReadOnlyList<SendAttemptRecord> ListAttempts(Guid batchId) => [];
        public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAt) { }
        public void AddAttempt(SendAttemptRecord attempt) { }
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
        return new HistoryViewModel(repo, recovery, NullLogger<HistoryViewModel>.Instance);
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
    public void RetryFailed_ReportsFailedCount()
    {
        HistoryViewModel vm = Create(out FakeRepository repo, out _);
        PayrollBatchRecord batch = Batch(Guid.NewGuid());
        repo.CreateBatch(batch);
        vm.LoadedCommand.Execute(null);
        vm.SelectedBatch = batch;

        vm.RetryFailedCommand.Execute(batch);

        Assert.Contains("1 penerima gagal", vm.StatusMessage);
    }

    [Fact]
    public void Recover_PrepareForRecoveryAndReloads()
    {
        HistoryViewModel vm = Create(out FakeRepository repo, out FakeRecovery recovery);
        PayrollBatchRecord batch = Batch(Guid.NewGuid());
        repo.CreateBatch(batch);
        vm.LoadedCommand.Execute(null);
        vm.SelectedBatch = batch;

        vm.RecoverCommand.Execute(batch);

        Assert.Contains(batch.Id, recovery.Prepared);
        Assert.Contains("pemulihan", vm.StatusMessage);
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
