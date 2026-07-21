namespace EunSlip.Core.Sending;

public interface IGmailAuthorization
{
    Task<GoogleAccount?> ConnectAsync(string clientSecretJson, CancellationToken cancellationToken);
    Task<GoogleAccount?> RestoreAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken);
}

public interface IGmailSender
{
    Task<SendOutcome> SendAsync(SendRequest request, CancellationToken cancellationToken);
}

public sealed record GoogleAccount(string Email);

public sealed record SendRequest(
    string ToEmail,
    string Subject,
    string Body,
    string AttachmentPath,
    string AttachmentFileName,
    string SenderDisplayName);

public enum SendResult { Sent, Failed }

public sealed record SendOutcome(
    SendResult Result,
    string? GmailMessageId,
    int? RetryAfterSeconds,
    string? ErrorCategory,
    string? ErrorMessage);
