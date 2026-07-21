namespace EunSlip.Core.Sending;

public static class GmailRetryPolicy
{
    public const int MaxAttempts = 3;

    public static TimeSpan Backoff(int attempt, int? retryAfterSeconds)
    {
        if (retryAfterSeconds is int seconds && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return attempt switch
        {
            1 => TimeSpan.FromSeconds(2),
            2 => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(30),
        };
    }
}
