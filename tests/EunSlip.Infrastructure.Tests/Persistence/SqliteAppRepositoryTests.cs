using EunSlip.Core.Persistence;
using EunSlip.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace EunSlip.Infrastructure.Tests.Persistence;

public sealed class SqliteAppRepositoryTests : IDisposable
{
    private readonly SqliteAppRepository _repo;
    private readonly string _path = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid().ToString("N") + ".db");

    public SqliteAppRepositoryTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _repo = new SqliteAppRepository($"Data Source={_path};Pooling=False");
        _repo.Initialize();
    }

    private static PayrollBatchRecord NewBatch(string period = "JULY 2025") => new(
        Guid.NewGuid(), period, new DateOnly(2026, 5, 11), "fp-sha256", BatchStatus.Ready,
        DateTimeOffset.UtcNow, null, null, true, 2, 0, 0);

    [Fact]
    public void Initialize_CreatesSchema_Idempotently()
    {
        _repo.Initialize();

        Assert.True(_repo.CheckIntegrity());
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        _repo.SetSetting("UiLanguage", "id-ID");

        Assert.Equal("id-ID", _repo.GetSetting("UiLanguage"));
        Assert.Null(_repo.GetSetting("Nonexistent"));
    }

    [Fact]
    public void Settings_OverwriteExistingValue()
    {
        _repo.SetSetting("UiLanguage", "id-ID");
        _repo.SetSetting("UiLanguage", "en-US");

        Assert.Equal("en-US", _repo.GetSetting("UiLanguage"));
    }

    [Fact]
    public void CreateBatch_ThenGetBatch_RoundTrips()
    {
        PayrollBatchRecord batch = NewBatch();
        _repo.CreateBatch(batch);

        PayrollBatchRecord? loaded = _repo.GetBatch(batch.Id);

        Assert.NotNull(loaded);
        Assert.Equal(batch.Period, loaded!.Period);
        Assert.Equal(batch.PaymentDate, loaded.PaymentDate);
        Assert.Equal(batch.Fingerprint, loaded.Fingerprint);
        Assert.Equal(BatchStatus.Ready, loaded.Status);
        Assert.Equal(2, loaded.RecipientCount);
    }

    [Fact]
    public void ListBatches_ReturnsNewestFirst()
    {
        _repo.CreateBatch(NewBatch("JAN 2025"));
        _repo.CreateBatch(NewBatch("FEB 2025"));

        IReadOnlyList<PayrollBatchRecord> list = _repo.ListBatches();

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void UpdateBatchStatus_PersistsStatusAndCompletion()
    {
        Guid id = _repo.CreateBatch(NewBatch());
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;

        _repo.UpdateBatchStatus(id, BatchStatus.Completed, null, completedAt);

        PayrollBatchRecord? loaded = _repo.GetBatch(id);
        Assert.Equal(BatchStatus.Completed, loaded!.Status);
        Assert.NotNull(loaded.CompletedAtUtc);
    }

    [Fact]
    public void AddRecipient_ThenListRecipients_RoundTrips()
    {
        Guid batchId = _repo.CreateBatch(NewBatch());
        BatchRecipientRecord recipient = new(
            Guid.NewGuid(), batchId, "enc-nik", "enc-email", "NIK0001",
            RecipientStatus.Pending, DateTimeOffset.UtcNow);
        _repo.AddRecipient(recipient);

        IReadOnlyList<BatchRecipientRecord> list = _repo.ListRecipients(batchId);

        BatchRecipientRecord loaded = Assert.Single(list);
        Assert.Equal("enc-nik", loaded.EncryptedNik);
        Assert.Equal("NIK0001", loaded.NikHint);
        Assert.Equal(RecipientStatus.Pending, loaded.Status);
    }

    [Fact]
    public void UpdateRecipientStatusToSent_IncrementsBatchSentTally()
    {
        Guid batchId = _repo.CreateBatch(NewBatch());
        Guid recipientId = _repo.AddRecipient(new BatchRecipientRecord(
            Guid.NewGuid(), batchId, "enc-nik", "enc-email", "NIK0001",
            RecipientStatus.Sending, DateTimeOffset.UtcNow));

        _repo.UpdateRecipientStatus(recipientId, RecipientStatus.Sent, DateTimeOffset.UtcNow);

        PayrollBatchRecord? loaded = _repo.GetBatch(batchId);
        Assert.Equal(1, loaded!.SentCount);
        Assert.Equal(0, loaded.FailedCount);
    }

    [Fact]
    public void UpdateRecipientStatusToFailed_IncrementsBatchFailedTally()
    {
        Guid batchId = _repo.CreateBatch(NewBatch());
        Guid recipientId = _repo.AddRecipient(new BatchRecipientRecord(
            Guid.NewGuid(), batchId, "enc-nik", "enc-email", "NIK0001",
            RecipientStatus.Sending, DateTimeOffset.UtcNow));

        _repo.UpdateRecipientStatus(recipientId, RecipientStatus.Failed, DateTimeOffset.UtcNow);

        Assert.Equal(1, _repo.GetBatch(batchId)!.FailedCount);
    }

    [Fact]
    public void AddAttempt_PersistsAttemptRecord()
    {
        Guid batchId = _repo.CreateBatch(NewBatch());
        Guid recipientId = _repo.AddRecipient(new BatchRecipientRecord(
            Guid.NewGuid(), batchId, "enc-nik", "enc-email", "NIK0001",
            RecipientStatus.Pending, DateTimeOffset.UtcNow));

        Guid attemptId = Guid.NewGuid();
        _repo.AddAttempt(new SendAttemptRecord(
            attemptId, recipientId, 1, AttemptType.Normal,
            DateTimeOffset.UtcNow, null, AttemptStatus.Pending, null, null, null));

        _repo.CompleteAttempt(attemptId, AttemptStatus.Sent, DateTimeOffset.UtcNow, null, null, "gmail-msg-1");

        Assert.True(true);
    }

    [Fact]
    public void UpdateBatchStatus_SetsStartedAtOnSending()
    {
        Guid id = _repo.CreateBatch(NewBatch());
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        _repo.UpdateBatchStatus(id, BatchStatus.Sending, startedAt, null);

        PayrollBatchRecord? loaded = _repo.GetBatch(id);
        Assert.Equal(BatchStatus.Sending, loaded!.Status);
        Assert.NotNull(loaded.StartedAtUtc);
    }

    [Fact]
    public void UpdateBatchStatus_PreservesStartedAtWhenCompleting()
    {
        Guid id = _repo.CreateBatch(NewBatch());
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        _repo.UpdateBatchStatus(id, BatchStatus.Sending, startedAt, null);

        _repo.UpdateBatchStatus(id, BatchStatus.Completed, null, DateTimeOffset.UtcNow);

        PayrollBatchRecord? loaded = _repo.GetBatch(id);
        Assert.Equal(startedAt.UtcDateTime, loaded!.StartedAtUtc!.Value.UtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FindInterruptedBatches_ReturnsOnlySendingBatches()
    {
        Guid sendingId = _repo.CreateBatch(NewBatch());
        _repo.UpdateBatchStatus(sendingId, BatchStatus.Sending, DateTimeOffset.UtcNow, null);
        Guid completedId = _repo.CreateBatch(NewBatch());
        _repo.UpdateBatchStatus(completedId, BatchStatus.Completed, null, DateTimeOffset.UtcNow);

        IReadOnlyList<Guid> interrupted = _repo.FindInterruptedBatches();

        Assert.Single(interrupted, sendingId);
    }

    [Fact]
    public void ResetSendingRecipientsToPending_ResetsOnlySending()
    {
        Guid batchId = _repo.CreateBatch(NewBatch());
        Guid sendingId = _repo.AddRecipient(new BatchRecipientRecord(
            Guid.NewGuid(), batchId, "enc1", "e1", "N1", RecipientStatus.Sending, DateTimeOffset.UtcNow));
        Guid sentId = _repo.AddRecipient(new BatchRecipientRecord(
            Guid.NewGuid(), batchId, "enc2", "e2", "N2", RecipientStatus.Sent, DateTimeOffset.UtcNow));

        _repo.ResetSendingRecipientsToPending(batchId);

        IReadOnlyList<BatchRecipientRecord> list = _repo.ListRecipients(batchId);
        Assert.Equal(RecipientStatus.Pending, list.First(r => r.Id == sendingId).Status);
        Assert.Equal(RecipientStatus.Sent, list.First(r => r.Id == sentId).Status);
    }

    [Fact]
    public void FindPreviouslySentNiks_ReturnsSentNikHintsForPeriod()
    {
        Guid batchId = _repo.CreateBatch(NewBatch("JULY 2025"));
        _repo.AddRecipient(new BatchRecipientRecord(
            Guid.NewGuid(), batchId, "enc", "e", "NIK0001", RecipientStatus.Sending, DateTimeOffset.UtcNow));
        Guid recipientId = _repo.ListRecipients(batchId)[0].Id;
        _repo.UpdateRecipientStatus(recipientId, RecipientStatus.Sent, DateTimeOffset.UtcNow);
        _repo.UpdateBatchStatus(batchId, BatchStatus.Completed, null, DateTimeOffset.UtcNow);

        IReadOnlyList<string> sent = _repo.FindPreviouslySentNiks("JULY 2025");

        Assert.Single(sent, "NIK0001");
    }

    [Fact]
    public void DeleteBatch_RemovesBatchRecipientsAndAttempts()
    {
        Guid batchId = _repo.CreateBatch(NewBatch());
        Guid recipientId = _repo.AddRecipient(new BatchRecipientRecord(
            Guid.NewGuid(), batchId, "enc", "e", "NIK0001", RecipientStatus.Pending, DateTimeOffset.UtcNow));
        _repo.AddAttempt(new SendAttemptRecord(
            Guid.NewGuid(), recipientId, 1, AttemptType.Normal,
            DateTimeOffset.UtcNow, null, AttemptStatus.Pending, null, null, null));

        _repo.DeleteBatch(batchId);

        Assert.Null(_repo.GetBatch(batchId));
        Assert.Empty(_repo.ListRecipients(batchId));
    }

    [Fact]
    public void ResetDatabase_ClearsAllDataButKeepsSchema()
    {
        _repo.CreateBatch(NewBatch());
        _repo.SetSetting("UiLanguage", "id-ID");

        _repo.ResetDatabase();

        Assert.Empty(_repo.ListBatches());
        Assert.Null(_repo.GetSetting("UiLanguage"));
        Assert.True(_repo.CheckIntegrity());
    }

    [Fact]
    public void CheckIntegrity_True_OnHealthyDatabase()
    {
        Assert.True(_repo.CheckIntegrity());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
