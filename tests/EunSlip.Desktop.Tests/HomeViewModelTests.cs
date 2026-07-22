using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Sending;
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop.Tests;

public sealed class HomeViewModelTests
{
    private sealed class Repository : IAppRepository
    {
        public List<PayrollBatchRecord> Batches { get; } = [];
        public Dictionary<string, string> Settings { get; } = [];
        public void Initialize() { }
        public bool CheckIntegrity() => true;
        public void ResetDatabase() { }
        public string? GetSetting(string key) => Settings.GetValueOrDefault(key);
        public void SetSetting(string key, string value) => Settings[key] = value;
        public Guid CreateBatch(PayrollBatchRecord batch) { Batches.Add(batch); return batch.Id; }
        public PayrollBatchRecord? GetBatch(Guid id) => Batches.FirstOrDefault(x => x.Id == id);
        public IReadOnlyList<PayrollBatchRecord> ListBatches() =>
            [.. Batches.OrderByDescending(x => x.CreatedAtUtc)];
        public void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc) { }
        public Guid AddRecipient(BatchRecipientRecord recipient) => recipient.Id;
        public IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId) => [];
        public IReadOnlyList<SendAttemptRecord> ListAttempts(Guid batchId) => [];
        public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAtUtc) { }
        public void AddAttempt(SendAttemptRecord attempt) { }
        public void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAtUtc,
            string? errorCategory, string? errorMessage, string? gmailMessageId)
        {
        }
        public AttemptStatus? GetLatestAttemptStatus(Guid recipientId) => null;
        public IReadOnlyList<Guid> FindInterruptedBatches() => [];
        public void ResetSendingRecipientsToPending(Guid batchId) { }
        public IReadOnlyList<string> FindPreviouslySentNiks(string period) => [];
        public void DeleteBatch(Guid id) { }
    }

    private sealed class Gmail : IGmailAuthorization
    {
        public Task<GoogleAccount?> ConnectAsync(string clientSecretJson, CancellationToken cancellationToken) =>
            Task.FromResult<GoogleAccount?>(new GoogleAccount("payroll@example.com"));
        public Task<GoogleAccount?> RestoreAsync(CancellationToken cancellationToken) =>
            Task.FromResult<GoogleAccount?>(new GoogleAccount("payroll@example.com"));
        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class Stamp : ISharedFileStore
    {
        public string? GetActiveStampPath() => "stamp.png";
        public string ImportStamp(string sourcePath) => "stamp.png";
        public void RemoveStamp() { }
    }

    [Fact]
    public async Task Loaded_UsesLocalizedStatusesAndLatestBatch()
    {
        Repository repository = new();
        repository.Settings["UiLanguage"] = "id-ID";
        repository.Batches.Add(new PayrollBatchRecord(Guid.NewGuid(), "JULI 2026", new DateOnly(2026, 7, 22),
            "fp", BatchStatus.Completed, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, true, 2, 2, 0));
        HomeViewModel vm = new(new Gmail(), new Stamp(), repository);

        await vm.LoadedCommand.ExecuteAsync(null);

        Assert.Equal(EunSlip.Desktop.Localization.Strings.Get("StatusReady"), vm.GmailStatusText);
        Assert.Equal(EunSlip.Desktop.Localization.Strings.Get("StatusReady"), vm.StampStatusText);
        Assert.Equal("id-ID", vm.ActiveLanguage);
        Assert.Contains("JULI 2026", vm.RecentBatchSummary);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Loaded_ExposesInterruptedBatchNotice()
    {
        Repository repository = new();
        repository.Batches.Add(new PayrollBatchRecord(Guid.NewGuid(), "JUNI 2026", new DateOnly(2026, 6, 22),
            "fp", BatchStatus.Interrupted, DateTimeOffset.UtcNow, null, null, true, 2, 1, 0));
        HomeViewModel vm = new(new Gmail(), new Stamp(), repository);

        await vm.LoadedCommand.ExecuteAsync(null);

        Assert.True(vm.HasInterruptedBatch);
        Assert.Contains("JUNI 2026", vm.InterruptedBatchNotice);
    }
}
