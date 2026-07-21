using EunSlip.Core.Batches;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Security;
using EunSlip.Core.Sending;
using Microsoft.Extensions.Logging.Abstractions;

namespace EunSlip.Core.Tests.Batches;

public sealed class BatchCoordinatorTests
{
    private static PayrollRow Row(string nik, string email = "emp@example.com") => new(
        nik, "Employee " + nik, "Finance", "Staff", new DateOnly(2020, 1, 15), "Monthly", email,
        5_000_000L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 100_000L, 100_000L, 50_000L, 50_000L, 0L,
        300_000L, 5_000_000L, 4_700_000L, 0m);

    private sealed class FakePdfGenerator : IPayslipPdfGenerator
    {
        public void Generate(PayslipRequest request, string outputPath) =>
            File.WriteAllBytes(outputPath, [1, 2, 3]);
    }

    private sealed class FakeSender(Func<SendRequest, RetrySendOutcome> behavior) : IGmailRetrySender
    {
        public Task<RetrySendOutcome> SendWithRetryAsync(SendRequest request, CancellationToken cancellationToken)
            => Task.FromResult(behavior(request));
    }

    private sealed class PassThroughSecretStore : ISecretStore
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string envelope) => envelope;
    }

    private sealed class FakeStampStore(string stampPath) : ISharedFileStore
    {
        public string? GetActiveStampPath() => stampPath;
        public string ImportStamp(string sourcePath) => throw new NotImplementedException();
        public void RemoveStamp() { }
    }

    private sealed class RealTempFiles : ITempFileService
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid().ToString("N"));
        public string CreateBatchTempDirectory(Guid batchId)
        {
            string dir = Path.Combine(_root, batchId.ToString("N"));
            _ = Directory.CreateDirectory(dir);
            return dir;
        }
        public void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }
        public void CleanupLeftovers() { }
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }

    private sealed class FakeRepository : IAppRepository
    {
        public List<PayrollBatchRecord> Batches { get; } = [];
        public List<BatchRecipientRecord> Recipients { get; } = [];
        public List<SendAttemptRecord> Attempts { get; } = [];
        public Dictionary<string, string> Settings { get; } = [];

        public void Initialize() { }
        public bool CheckIntegrity() => true;
        public void ResetDatabase() { Batches.Clear(); Recipients.Clear(); Attempts.Clear(); Settings.Clear(); }
        public string? GetSetting(string key) => Settings.GetValueOrDefault(key);
        public void SetSetting(string key, string value) => Settings[key] = value;
        public Guid CreateBatch(PayrollBatchRecord batch) { Batches.Add(batch); return batch.Id; }
        public PayrollBatchRecord? GetBatch(Guid id) => Batches.FirstOrDefault(b => b.Id == id);
        public IReadOnlyList<PayrollBatchRecord> ListBatches() => Batches;
        public void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAt, DateTimeOffset? completedAt)
        {
            int i = Batches.FindIndex(b => b.Id == id);
            if (i >= 0) Batches[i] = Batches[i] with { Status = status, StartedAtUtc = startedAt ?? Batches[i].StartedAtUtc, CompletedAtUtc = completedAt };
        }
        public Guid AddRecipient(BatchRecipientRecord recipient) { Recipients.Add(recipient); return recipient.Id; }
        public IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId) => [.. Recipients.Where(r => r.BatchId == batchId)];
        public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAt)
        {
            int i = Recipients.FindIndex(r => r.Id == recipientId);
            if (i >= 0) Recipients[i] = Recipients[i] with { Status = status, LastUpdatedAtUtc = updatedAt };
        }
        public void AddAttempt(SendAttemptRecord attempt) => Attempts.Add(attempt);
        public void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAt, string? errorCategory, string? errorMessage, string? gmailMessageId) { }
        public AttemptStatus? GetLatestAttemptStatus(Guid recipientId) => Attempts.LastOrDefault(a => a.RecipientId == recipientId)?.Status;
        public IReadOnlyList<Guid> FindInterruptedBatches() => [.. Batches.Where(b => b.Status == BatchStatus.Sending).Select(b => b.Id)];
        public void ResetSendingRecipientsToPending(Guid batchId) { }
        public IReadOnlyList<string> FindPreviouslySentNiks(string period) => [];
        public void DeleteBatch(Guid id) { Recipients.RemoveAll(r => r.BatchId == id); Batches.RemoveAll(b => b.Id == id); }
    }

    private static (BatchCoordinator coordinator, FakeRepository repo, FakeStampStore stamp, RealTempFiles temp) Setup(
        IGmailRetrySender sender)
    {
        FakeRepository repo = new();
        FakeStampStore stamp = new("stamp.png");
        RealTempFiles temp = new();
        BatchCoordinator coordinator = new(
            new FakePdfGenerator(), sender, repo, new PassThroughSecretStore(), stamp, temp,
            NullLogger<BatchCoordinator>.Instance);
        return (coordinator, repo, stamp, temp);
    }

    private static BatchRunRequest Request(Guid batchId, IReadOnlyList<PayrollRow> rows) => new(
        new BatchContext("JULY 2025", new DateOnly(2026, 5, 11)), rows,
        "Slip Gaji Karyawan", "body", "PT. EUNSUNG INDONESIA", batchId,
        AttemptType.Normal, new Progress<BatchProgress>());

    private static Guid SeedBatch(FakeRepository repo, IReadOnlyList<PayrollRow> rows)
    {
        Guid batchId = Guid.NewGuid();
        repo.CreateBatch(new PayrollBatchRecord(batchId, "JULY 2025", new DateOnly(2026, 5, 11),
            "fp", BatchStatus.Ready, DateTimeOffset.UtcNow, null, null, true, rows.Count, 0, 0));
        foreach (PayrollRow row in rows)
        {
            repo.AddRecipient(new BatchRecipientRecord(
                Guid.NewGuid(), batchId, row.Nik, row.Email, NikHint.LastFour(row.Nik),
                RecipientStatus.Pending, DateTimeOffset.UtcNow));
        }
        return batchId;
    }

    private static RetrySendOutcome Sent(string gmailId) =>
        new(SendResult.Sent, gmailId, 1, null, null,
            [new(1, SendResult.Sent, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, gmailId, null, null)]);

    private static RetrySendOutcome Failed(int attempts = 3, string category = "EmailSendFailed", string message = "bounce") =>
        new(SendResult.Failed, null, attempts, category, message,
            [.. Enumerable.Range(1, attempts).Select(n =>
                new AttemptDetail(n, SendResult.Failed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, category, message))]);

    [Fact]
    public async Task AllSucceed_MarksBatchCompletedAndAttemptsRecorded()
    {
        PayrollRow[] rows = [Row("NIK0001"), Row("NIK0002")];
        var (coordinator, repo, _, _) = Setup(new FakeSender(_ => Sent("msg-1")));
        Guid batchId = SeedBatch(repo, rows);

        BatchRunResult result = await coordinator.RunBatchAsync(Request(batchId, rows), CancellationToken.None);

        Assert.Equal(2, result.SentCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, repo.Attempts.Count);
        Assert.All(repo.Recipients, r => Assert.Equal(RecipientStatus.Sent, r.Status));
        Assert.Equal(BatchStatus.Completed, repo.GetBatch(batchId)!.Status);
    }

    [Fact]
    public async Task OneFails_ContinuesOthersAndMarksFailed()
    {
        PayrollRow[] rows = [Row("NIK0001"), Row("NIK0002")];
        var (coordinator, repo, _, _) = Setup(
            new FakeSender(req => req.AttachmentFileName.Contains("NIK0001")
                ? Failed(3, "EmailSendFailed", "bounce")
                : Sent("msg-2")));
        Guid batchId = SeedBatch(repo, rows);

        BatchRunResult result = await coordinator.RunBatchAsync(Request(batchId, rows), CancellationToken.None);

        Assert.Equal(1, result.SentCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(RecipientStatus.Failed, repo.Recipients.First(r => r.EncryptedNik == "NIK0001").Status);
        Assert.Equal(RecipientStatus.Sent, repo.Recipients.First(r => r.EncryptedNik == "NIK0002").Status);
    }

    [Fact]
    public async Task RecordsAttempt_PerAttemptRow()
    {
        PayrollRow[] rows = [Row("NIK0001")];
        AttemptDetail[] details =
        [
            new(1, SendResult.Failed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "EmailSendFailed", "timeout"),
            new(2, SendResult.Failed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "EmailSendFailed", "timeout"),
            new(3, SendResult.Failed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "EmailSendFailed", "timeout"),
        ];
        var (coordinator, repo, _, _) = Setup(
            new FakeSender(_ => new RetrySendOutcome(SendResult.Failed, null, 3, "EmailSendFailed", "timeout", details)));
        Guid batchId = SeedBatch(repo, rows);

        await coordinator.RunBatchAsync(Request(batchId, rows), CancellationToken.None);

        Assert.Equal(3, repo.Attempts.Count);
        Assert.Equal(1, repo.Attempts[0].AttemptNumber);
        Assert.Equal(3, repo.Attempts[2].AttemptNumber);
        Assert.All(repo.Attempts, a => Assert.Equal(AttemptStatus.Failed, a.Status));
        Assert.Equal("EmailSendFailed", repo.Attempts[0].ErrorCategory);
    }

    [Fact]
    public async Task MissingStamp_Throws()
    {
        PayrollRow[] rows = [Row("NIK0001")];
        FakeRepository repo = new();
        BatchCoordinator coordinator = new(
            new FakePdfGenerator(),
            new FakeSender(_ => Sent("m")),
            repo, new PassThroughSecretStore(),
            new FakeStampStore(null!),
            new RealTempFiles(), NullLogger<BatchCoordinator>.Instance);
        Guid batchId = SeedBatch(repo, rows);

        await Assert.ThrowsAsync<BatchCoordinatorException>(() =>
            coordinator.RunBatchAsync(Request(batchId, rows), CancellationToken.None));
    }

    [Fact]
    public async Task EmptyRows_ReturnsEmptyResult()
    {
        var (coordinator, repo, _, _) = Setup(new FakeSender(_ => Sent("m")));
        Guid batchId = SeedBatch(repo, []);

        BatchRunResult result = await coordinator.RunBatchAsync(Request(batchId, []), CancellationToken.None);

        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task TempPdfDeletedAfterEachRecipient()
    {
        PayrollRow[] rows = [Row("NIK0001")];
        string? capturedPath = null;
        FakeSender sender = new(req =>
        {
            capturedPath = req.AttachmentPath;
            return Sent("m");
        });
        var (coordinator, repo, _, _) = Setup(sender);
        Guid batchId = SeedBatch(repo, rows);

        await coordinator.RunBatchAsync(Request(batchId, rows), CancellationToken.None);

        Assert.NotNull(capturedPath);
        Assert.False(File.Exists(capturedPath!));
    }
}
