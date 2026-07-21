using EunSlip.Core.Sending;
using EunSlip.Infrastructure.Gmail;

namespace EunSlip.Infrastructure.Tests.Gmail;

public sealed class GmailRetrySenderTests
{
    private static SendRequest Request() => new(
        "to@example.com", "Slip Gaji Karyawan", "body", "Slip.pdf", "Slip_Gaji.pdf", "PT. EUNSUNG INDONESIA");

    private sealed class NoDelay : IRetryDelay
    {
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubSender(params SendOutcome[] outcomes) : IGmailSender
    {
        private readonly Queue<SendOutcome> _outcomes = new(outcomes);
        public int Calls { get; private set; }

        public Task<SendOutcome> SendAsync(SendRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_outcomes.Dequeue());
        }
    }

    private static GmailRetrySender Sender(IGmailSender inner) => new(inner, new NoDelay());

    [Fact]
    public async Task SendsOnFirstAttempt_ReturnsSentImmediately()
    {
        StubSender inner = new(new SendOutcome(SendResult.Sent, "msg-1", null, null, null));
        GmailRetrySender sender = Sender(inner);

        RetrySendOutcome result = await sender.SendWithRetryAsync(Request(), CancellationToken.None);

        Assert.Equal(SendResult.Sent, result.Result);
        Assert.Equal("msg-1", result.GmailMessageId);
        Assert.Equal(1, result.AttemptsMade);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task RetriesUntilSuccess_WithinMaxAttempts()
    {
        StubSender inner = new(
            new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", "transient"),
            new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", "transient"),
            new SendOutcome(SendResult.Sent, "msg-1", null, null, null));
        GmailRetrySender sender = Sender(inner);

        RetrySendOutcome result = await sender.SendWithRetryAsync(Request(), CancellationToken.None);

        Assert.Equal(SendResult.Sent, result.Result);
        Assert.Equal(3, result.AttemptsMade);
        Assert.Equal(3, inner.Calls);
    }

    [Fact]
    public async Task FailsAfterMaxAttempts()
    {
        StubSender inner = new(
            new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", "fail"),
            new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", "fail"),
            new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", "fail"));
        GmailRetrySender sender = Sender(inner);

        RetrySendOutcome result = await sender.SendWithRetryAsync(Request(), CancellationToken.None);

        Assert.Equal(SendResult.Failed, result.Result);
        Assert.Equal(3, result.AttemptsMade);
        Assert.Equal("EmailSendFailed", result.ErrorCategory);
    }

    [Fact]
    public async Task DoesNotRetry_WhenGmailNotConnected()
    {
        StubSender inner = new(new SendOutcome(SendResult.Failed, null, null, "GmailNotConnected", "no account"));
        GmailRetrySender sender = Sender(inner);

        RetrySendOutcome result = await sender.SendWithRetryAsync(Request(), CancellationToken.None);

        Assert.Equal(SendResult.Failed, result.Result);
        Assert.Equal(1, result.AttemptsMade);
        Assert.Equal("GmailNotConnected", result.ErrorCategory);
    }

    [Fact]
    public async Task NeverExceedsThreeAttempts()
    {
        StubSender inner = new(
            new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", "x"),
            new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", "x"),
            new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", "x"),
            new SendOutcome(SendResult.Sent, "msg", null, null, null));
        GmailRetrySender sender = Sender(inner);

        RetrySendOutcome result = await sender.SendWithRetryAsync(Request(), CancellationToken.None);

        Assert.Equal(SendResult.Failed, result.Result);
        Assert.Equal(3, result.AttemptsMade);
    }
}
