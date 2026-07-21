using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;

namespace EunSlip.Core.Batches;

public interface IBatchCoordinator
{
    Task<BatchRunResult> RunBatchAsync(BatchRunRequest request, CancellationToken cancellationToken);
}

public sealed record BatchRunRequest(
    BatchContext Context,
    IReadOnlyList<PayrollRow> Rows,
    string Subject,
    string Body,
    string SenderDisplayName,
    Guid BatchId,
    AttemptType AttemptKind,
    IProgress<BatchProgress> Progress);

public sealed record BatchProgress(
    int Current,
    int Total,
    string Nik,
    string Name,
    int Succeeded,
    int Failed,
    int CurrentAttempt);

public sealed record RecipientResult(
    string Nik,
    string Name,
    string Email,
    bool Succeeded,
    int AttemptsMade,
    string? ErrorCategory,
    string? ErrorMessage);

public sealed record BatchRunResult(
    Guid BatchId,
    IReadOnlyList<RecipientResult> Results)
{
    public int SentCount => Results.Count(r => r.Succeeded);
    public int FailedCount => Results.Count(r => !r.Succeeded);
}

public sealed class BatchCoordinatorException(
    string message, Exception? innerException = null) : Exception(message, innerException);
