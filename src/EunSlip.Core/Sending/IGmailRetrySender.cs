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

public sealed record RetrySendOutcome(
    SendResult Result,
    string? GmailMessageId,
    int AttemptsMade,
    string? ErrorCategory,
    string? ErrorMessage);
