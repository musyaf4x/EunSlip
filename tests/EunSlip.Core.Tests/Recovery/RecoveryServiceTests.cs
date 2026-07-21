using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Recovery;
using EunSlip.Core.Security;

namespace EunSlip.Core.Tests.Recovery;

public sealed class RecoveryServiceTests
{
    private static PayrollRow Row(string nik) => new(
        nik, "Name " + nik, null, null, new DateOnly(2020, 1, 15), null, nik.ToLowerInvariant() + "@e.co",
        5_000_000L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 100_000L, 100_000L, 50_000L, 50_000L, 0L,
        300_000L, 5_000_000L, 4_700_000L, 0m);

    private static BatchContext Context() => new("JULY 2025", new DateOnly(2026, 5, 11));

    private sealed class FakeRepository : IAppRepository
    {
        public List<PayrollBatchRecord> Batches { get; } = [];
        public List<BatchRecipientRecord> Recipients { get; } = [];
        public List<SendAttemptRecord> Attempts { get; } = [];
        public Dictionary<Guid, int> SendingResets { get; } = [];

        public void Initialize() { }
        public bool CheckIntegrity() => true;
        public void ResetDatabase() { }
        public string? GetSetting(string key) => null;
        public void SetSetting(string key, string value) { }
        public Guid CreateBatch(PayrollBatchRecord batch) { Batches.Add(batch); return batch.Id; }
        public PayrollBatchRecord? GetBatch(Guid id) => Batches.FirstOrDefault(b => b.Id == id);
        public IReadOnlyList<PayrollBatchRecord> ListBatches() => Batches;
        public void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAt, DateTimeOffset? completedAt)
        {
            int i = Batches.FindIndex(b => b.Id == id);
            if (i >= 0) Batches[i] = Batches[i] with { Status = status };
        }
        public Guid AddRecipient(BatchRecipientRecord r) { Recipients.Add(r); return r.Id; }
        public IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId) =>
            [.. Recipients.Where(r => r.BatchId == batchId)];
        public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAt)
        {
            int i = Recipients.FindIndex(r => r.Id == recipientId);
            if (i >= 0) Recipients[i] = Recipients[i] with { Status = status };
        }
        public void AddAttempt(SendAttemptRecord attempt) { Attempts.Add(attempt); }
        public void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAt, string? errorCategory, string? errorMessage, string? gmailMessageId) { }
        public AttemptStatus? GetLatestAttemptStatus(Guid recipientId) => Attempts.LastOrDefault(a => a.RecipientId == recipientId)?.Status;
        public IReadOnlyList<Guid> FindInterruptedBatches() =>
            [.. Batches.Where(b => b.Status == BatchStatus.Sending).Select(b => b.Id)];
        public void ResetSendingRecipientsToPending(Guid batchId)
        {
            for (int i = 0; i < Recipients.Count; i++)
            {
                if (Recipients[i].BatchId == batchId && Recipients[i].Status == RecipientStatus.Sending)
                {
                    Recipients[i] = Recipients[i] with { Status = RecipientStatus.Pending };
                }
            }
            SendingResets[batchId] = 1;
        }
        public IReadOnlyList<string> FindPreviouslySentNiks(string period) => [];
        public void DeleteBatch(Guid id) { }
    }

    private sealed class PassThroughSecretStore : ISecretStore
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string envelope) => envelope;
    }

    private static (RecoveryService svc, FakeRepository repo) Setup()
    {
        FakeRepository repo = new();
        return (new RecoveryService(repo, new PassThroughSecretStore()), repo);
    }

    private static Guid SeedBatch(FakeRepository repo, string fingerprint, params (string Nik, RecipientStatus Status)[] recipients)
    {
        Guid batchId = Guid.NewGuid();
        repo.CreateBatch(new PayrollBatchRecord(batchId, "JULY 2025", new DateOnly(2026, 5, 11),
            fingerprint, BatchStatus.Sending, DateTimeOffset.UtcNow, null, null, true, recipients.Length, 0, 0));
        foreach ((string nik, RecipientStatus status) in recipients)
        {
            repo.AddRecipient(new BatchRecipientRecord(
                Guid.NewGuid(), batchId, nik, nik.ToLowerInvariant() + "@e.co",
                NikHint.LastFour(nik), status, DateTimeOffset.UtcNow));
        }
        return batchId;
    }

    [Fact]
    public void DetectInterruptedBatches_ReturnsSendingBatches()
    {
        var (svc, repo) = Setup();
        Guid sendingId = SeedBatch(repo, "fp", ("NIK1", RecipientStatus.Sending));
        Guid completedId = Guid.NewGuid();
        repo.CreateBatch(new PayrollBatchRecord(completedId, "JULY 2025", new DateOnly(2026, 5, 11),
            "fp2", BatchStatus.Completed, DateTimeOffset.UtcNow, null, null, true, 0, 0, 0));

        IReadOnlyList<Guid> result = svc.DetectInterruptedBatches();

        Assert.Single(result, sendingId);
    }

    [Fact]
    public void PrepareForRecovery_MarksInterruptedAndResetsSendingRecipients()
    {
        var (svc, repo) = Setup();
        Guid batchId = SeedBatch(repo, "fp", ("NIK1", RecipientStatus.Sending));

        svc.PrepareForRecovery(batchId);

        Assert.Equal(BatchStatus.Interrupted, repo.GetBatch(batchId)!.Status);
        Assert.Contains(batchId, repo.SendingResets.Keys);
    }

    [Fact]
    public void VerifyFingerprint_Match_ReturnsCanProceed()
    {
        var (svc, repo) = Setup();
        PayrollRow[] rows = [Row("NIK0001"), Row("NIK0002")];
        string fingerprint = PayrollFingerprint.Compute(Context(), rows);
        Guid batchId = SeedBatch(repo, fingerprint, ("NIK0001", RecipientStatus.Sent));

        RecoveryGate gate = svc.VerifyFingerprint(batchId, Context(), rows);

        Assert.True(gate.CanProceed);
        Assert.Equal(fingerprint, gate.ComputedFingerprint);
    }

    [Fact]
    public void VerifyFingerprint_Mismatch_BlocksProceeding()
    {
        var (svc, repo) = Setup();
        Guid batchId = SeedBatch(repo, "original-fingerprint", ("NIK0001", RecipientStatus.Sent));
        PayrollRow[] rows = [Row("NIK0001")];

        RecoveryGate gate = svc.VerifyFingerprint(batchId, Context(), rows);

        Assert.False(gate.CanProceed);
        Assert.Equal("original-fingerprint", gate.StoredFingerprint);
        Assert.NotEqual("original-fingerprint", gate.ComputedFingerprint);
    }

    [Fact]
    public void VerifyFingerprint_ReorderedRows_StillMatches()
    {
        var (svc, repo) = Setup();
        PayrollRow[] original = [Row("NIK0001"), Row("NIK0002"), Row("NIK0003")];
        string fingerprint = PayrollFingerprint.Compute(Context(), original);
        Guid batchId = SeedBatch(repo, fingerprint, ("NIK0001", RecipientStatus.Sent));
        PayrollRow[] reordered = [original[2], original[0], original[1]];

        RecoveryGate gate = svc.VerifyFingerprint(batchId, Context(), reordered);

        Assert.True(gate.CanProceed);
    }

    [Fact]
    public void VerifyFingerprint_ChangedField_BlocksProceeding()
    {
        var (svc, repo) = Setup();
        PayrollRow[] original = [Row("NIK0001")];
        string fingerprint = PayrollFingerprint.Compute(Context(), original);
        Guid batchId = SeedBatch(repo, fingerprint, ("NIK0001", RecipientStatus.Sent));
        PayrollRow[] changed = [Row("NIK0001") with { Basic = 9_999_999L }];

        RecoveryGate gate = svc.VerifyFingerprint(batchId, Context(), changed);

        Assert.False(gate.CanProceed);
    }

    [Fact]
    public void PrepareForRecovery_ReconcilesCommittedSends_SendingRecipientWithSentAttemptBecomesSent()
    {
        var (svc, repo) = Setup();
        Guid batchId = SeedBatch(repo, "fp", ("NIK0001", RecipientStatus.Sending));
        BatchRecipientRecord recipient = repo.Recipients[0];
        repo.AddAttempt(new SendAttemptRecord(
            Guid.NewGuid(), recipient.Id, 1, AttemptType.Normal,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, AttemptStatus.Sent, null, null, "msg-1"));

        svc.PrepareForRecovery(batchId);

        Assert.Equal(RecipientStatus.Sent, repo.Recipients[0].Status);
    }

    [Fact]
    public void PrepareForRecovery_DoesNotReconcile_SendingRecipientWithoutSentAttempt()
    {
        var (svc, repo) = Setup();
        Guid batchId = SeedBatch(repo, "fp",
            ("NIK0001", RecipientStatus.Sending),
            ("NIK0002", RecipientStatus.Sending));
        repo.AddAttempt(new SendAttemptRecord(
            Guid.NewGuid(), repo.Recipients[0].Id, 1, AttemptType.Normal,
            DateTimeOffset.UtcNow, null, AttemptStatus.Pending, null, null, null));

        svc.PrepareForRecovery(batchId);

        Assert.Equal(RecipientStatus.Pending, repo.Recipients[0].Status);
        Assert.Equal(RecipientStatus.Pending, repo.Recipients[1].Status);
    }

    [Fact]
    public void SelectRetryFailedNiks_ReturnsOnlyFailedRecipients()
    {
        var (svc, repo) = Setup();
        Guid batchId = SeedBatch(repo, "fp",
            ("NIK0001", RecipientStatus.Sent),
            ("NIK0002", RecipientStatus.Failed),
            ("NIK0003", RecipientStatus.Failed),
            ("NIK0004", RecipientStatus.Pending));

        IReadOnlyList<string> niks = svc.SelectRetryFailedNiks(batchId);

        Assert.Equal(["NIK0002", "NIK0003"], niks);
    }

    [Fact]
    public void SelectRecoveryResendNiks_ReturnsAllNonSent()
    {
        var (svc, repo) = Setup();
        Guid batchId = SeedBatch(repo, "fp",
            ("NIK0001", RecipientStatus.Sent),
            ("NIK0002", RecipientStatus.Failed),
            ("NIK0003", RecipientStatus.Pending));

        IReadOnlyList<string> niks = svc.SelectRecoveryResendNiks(batchId);

        Assert.Equal(["NIK0002", "NIK0003"], niks);
    }
}
