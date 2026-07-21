using EunSlip.Core.Sending;

namespace EunSlip.Infrastructure.Gmail;

public sealed class GmailRetrySender(IGmailSender sender, IRetryDelay? delay = null) : IGmailRetrySender
{
    private readonly IGmailSender _sender = sender;
    private readonly IRetryDelay _delay = delay ?? new RealRetryDelay();

    public async Task<RetrySendOutcome> SendWithRetryAsync(
        SendRequest request, CancellationToken cancellationToken)
    {
        int attempts = 0;
        string? lastErrorCategory = null;
        string? lastErrorMessage = null;

        while (attempts < GmailRetryPolicy.MaxAttempts)
        {
            attempts++;
            SendOutcome outcome = await _sender.SendAsync(request, cancellationToken);

            if (outcome.Result == SendResult.Sent)
            {
                return new RetrySendOutcome(
                    SendResult.Sent, outcome.GmailMessageId, attempts, null, null);
            }

            lastErrorCategory = outcome.ErrorCategory;
            lastErrorMessage = outcome.ErrorMessage;

            if (outcome.ErrorCategory == "GmailNotConnected")
            {
                break;
            }

            if (attempts < GmailRetryPolicy.MaxAttempts)
            {
                TimeSpan wait = GmailRetryPolicy.Backoff(attempts, outcome.RetryAfterSeconds);
                try
                {
                    await _delay.WaitAsync(wait, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        return new RetrySendOutcome(
            SendResult.Failed, null, attempts, lastErrorCategory, lastErrorMessage);
    }
}
