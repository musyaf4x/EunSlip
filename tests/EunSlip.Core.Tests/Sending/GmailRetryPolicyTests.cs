using EunSlip.Core.Sending;

namespace EunSlip.Core.Tests.Sending;

public sealed class GmailRetryPolicyTests
{
    [Fact]
    public void MaxAttempts_IsThree()
    {
        Assert.Equal(3, GmailRetryPolicy.MaxAttempts);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 10)]
    [InlineData(3, 30)]
    public void Backoff_UsesBoundedSchedule_WhenNoRetryAfter(int attempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), GmailRetryPolicy.Backoff(attempt, null));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(120)]
    public void Backoff_HonorsRetryAfter_WhenPositive(int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), GmailRetryPolicy.Backoff(1, seconds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(null)]
    public void Backoff_IgnoresRetryAfter_WhenNotPositive(int? seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(2), GmailRetryPolicy.Backoff(1, seconds));
    }
}
