namespace EunSlip.Core.Sending;

public interface IGmailRetrySender
{
    Task<RetrySendOutcome> SendWithRetryAsync(
        SendRequest request, CancellationToken cancellationToken);
}

public interface IRetryDelay
{
    Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class RealRetryDelay : IRetryDelay
{
    public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}

public sealed record AttemptDetail(
    int AttemptNumber,
    SendResult Result,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string? GmailMessageId,
    string? ErrorCategory,
    string? ErrorMessage);

public sealed record RetrySendOutcome(
    SendResult Result,
    string? GmailMessageId,
    int AttemptsMade,
    string? ErrorCategory,
    string? ErrorMessage,
    IReadOnlyList<AttemptDetail> Attempts)
{
    public RetrySendOutcome(
        SendResult Result,
        string? GmailMessageId,
        int AttemptsMade,
        string? ErrorCategory,
        string? ErrorMessage)
        : this(Result, GmailMessageId, AttemptsMade, ErrorCategory, ErrorMessage, [])
    {
    }
}
