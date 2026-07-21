using EunSlip.Core.Sending;

namespace EunSlip.Infrastructure.Gmail;

public sealed class GmailRetrySender(IGmailSender sender, IRetryDelay? delay = null) : IGmailRetrySender
{
    private readonly IGmailSender _sender = sender;
    private readonly IRetryDelay _delay = delay ?? new RealRetryDelay();

    public async Task<RetrySendOutcome> SendWithRetryAsync(
        SendRequest request, CancellationToken cancellationToken)
    {
        List<AttemptDetail> details = [];
        string? lastErrorCategory = null;
        string? lastErrorMessage = null;

        for (int attempt = 1; attempt <= GmailRetryPolicy.MaxAttempts; attempt++)
        {
            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            SendOutcome outcome = await _sender.SendAsync(request, cancellationToken);
            DateTimeOffset completedAt = DateTimeOffset.UtcNow;

            details.Add(new AttemptDetail(
                attempt, outcome.Result, startedAt, completedAt,
                outcome.GmailMessageId, outcome.ErrorCategory, outcome.ErrorMessage));

            if (outcome.Result == SendResult.Sent)
            {
                return new RetrySendOutcome(
                    SendResult.Sent, outcome.GmailMessageId, attempt, null, null, details);
            }

            lastErrorCategory = outcome.ErrorCategory;
            lastErrorMessage = outcome.ErrorMessage;

            if (outcome.ErrorCategory == "GmailNotConnected")
            {
                break;
            }

            if (attempt < GmailRetryPolicy.MaxAttempts)
            {
                TimeSpan wait = GmailRetryPolicy.Backoff(attempt, outcome.RetryAfterSeconds);
                await _delay.WaitAsync(wait, cancellationToken);
            }
        }

        return new RetrySendOutcome(
            SendResult.Failed, null, details.Count, lastErrorCategory, lastErrorMessage, details);
    }
}
