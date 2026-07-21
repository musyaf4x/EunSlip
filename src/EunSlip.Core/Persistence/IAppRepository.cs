namespace EunSlip.Core.Persistence;

public interface IAppRepository
{
    void Initialize();
    bool CheckIntegrity();
    void ResetDatabase();

    string? GetSetting(string key);
    void SetSetting(string key, string value);

    Guid CreateBatch(PayrollBatchRecord batch);
    PayrollBatchRecord? GetBatch(Guid id);
    IReadOnlyList<PayrollBatchRecord> ListBatches();
    void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc);
    Guid AddRecipient(BatchRecipientRecord recipient);
    IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId);
    void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAtUtc);
    void AddAttempt(SendAttemptRecord attempt);
    void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAtUtc, string? errorCategory, string? errorMessage, string? gmailMessageId);
    AttemptStatus? GetLatestAttemptStatus(Guid recipientId);

    IReadOnlyList<Guid> FindInterruptedBatches();
    void ResetSendingRecipientsToPending(Guid batchId);
    IReadOnlyList<string> FindPreviouslySentNiks(string period);
    void DeleteBatch(Guid id);
}

public enum BatchStatus { Draft, Ready, Sending, Completed, Interrupted }
public enum RecipientStatus { Pending, Sending, Sent, Failed }
public enum AttemptType { Normal, FailedRetry, RecoveryRetry }

public sealed record PayrollBatchRecord(
    Guid Id,
    string Period,
    DateOnly PaymentDate,
    string Fingerprint,
    BatchStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool WarningConfirmed,
    int RecipientCount,
    int SentCount,
    int FailedCount);

public sealed record BatchRecipientRecord(
    Guid Id,
    Guid BatchId,
    string EncryptedNik,
    string EncryptedEmail,
    string? NikHint,
    RecipientStatus Status,
    DateTimeOffset LastUpdatedAtUtc);

public sealed record SendAttemptRecord(
    Guid Id,
    Guid RecipientId,
    int AttemptNumber,
    AttemptType AttemptType,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    AttemptStatus Status,
    string? ErrorCategory,
    string? ErrorMessage,
    string? GmailMessageId);

public enum AttemptStatus { Pending, Sent, Failed }
