using EunSlip.Core.Sending;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using MimeKit;

namespace EunSlip.Infrastructure.Gmail;

public sealed class GmailSender(GmailAuthorization authorization, IMimeMessageBuilder mimeBuilder) : IGmailSender
{
    private static readonly string[] Scopes = [GmailService.Scope.GmailSend, "openid", "email"];

    private readonly GmailAuthorization _authorization = authorization;
    private readonly IMimeMessageBuilder _mimeBuilder = mimeBuilder;

    public static IReadOnlyList<string> RequiredScopes => Scopes;

    public async Task<SendOutcome> SendAsync(SendRequest request, CancellationToken cancellationToken)
    {
        if (_authorization.Credential is null)
        {
            return new SendOutcome(SendResult.Failed, null, null, "GmailNotConnected", "Gmail account is not connected.");
        }

        MimeMessage mime = _mimeBuilder.Build(request);
        byte[] raw = Encode(mime);
        Message gmailMessage = new() { Raw = Base64UrlEncode(raw) };

        GmailService service = BuildService(_authorization.Credential);
        try
        {
            UsersResource.MessagesResource.SendRequest sendRequest =
                service.Users.Messages.Send(gmailMessage, "me");

            Message result = await sendRequest.ExecuteAsync(cancellationToken);
            return new SendOutcome(SendResult.Sent, result.Id, null, null, null);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            return new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", "Gmail rate limit reached.");
        }
        catch (Google.GoogleApiException ex)
        {
            return new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", Sanitize(ex.Message));
        }
        catch (Exception ex)
        {
            return new SendOutcome(SendResult.Failed, null, null, "EmailSendFailed", Sanitize(ex.Message));
        }
        finally
        {
            service.Dispose();
        }
    }

    private static GmailService BuildService(Google.Apis.Auth.OAuth2.UserCredential credential)
    {
        BaseClientService.Initializer initializer = new()
        {
            HttpClientInitializer = credential,
            ApplicationName = "EunSlip",
        };
        return new GmailService(initializer);
    }

    private static byte[] Encode(MimeMessage mime)
    {
        using MemoryStream stream = new();
        mime.WriteTo(stream);
        return stream.ToArray();
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string Sanitize(string message) =>
        message.Length > 200 ? message[..200] : message;
}
