using EunSlip.Core.Sending;
using Google.Apis.Auth.OAuth2;

namespace EunSlip.Infrastructure.Gmail;

public sealed class GmailAuthorization(
    DpapiTokenDataStore tokenStore,
    Func<CancellationToken, Task<string?>> clientSecretProvider) : IGmailAuthorization
{
    private const string UserIdentifier = "user";

    internal Google.Apis.Auth.OAuth2.UserCredential? Credential { get; private set; }

    public Task<GoogleAccount?> ConnectAsync(string clientSecretJson, CancellationToken cancellationToken)
    {
        ClientSecrets secrets = LoadSecrets(clientSecretJson);
        return AuthorizeAsync(secrets, cancellationToken);
    }

    public async Task<GoogleAccount?> RestoreAsync(CancellationToken cancellationToken)
    {
        string? secretJson = await clientSecretProvider(cancellationToken);
        if (string.IsNullOrWhiteSpace(secretJson))
        {
            return null;
        }

        ClientSecrets secrets = LoadSecrets(secretJson);
        return await AuthorizeAsync(secrets, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        Credential = null;
        await tokenStore.DeleteAsync<Google.Apis.Auth.OAuth2.Responses.TokenResponse>($"oauth_{UserIdentifier}");
    }

    public Task<bool> IsConnectedAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Credential is not null);
    }

    private static ClientSecrets LoadSecrets(string clientSecretJson)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(clientSecretJson);
        using MemoryStream stream = new(bytes);
        return GoogleClientSecrets.FromStream(stream).Secrets;
    }

    private async Task<GoogleAccount?> AuthorizeAsync(ClientSecrets secrets, CancellationToken cancellationToken)
    {
        Credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            GmailSender.RequiredScopes,
            UserIdentifier,
            cancellationToken,
            tokenStore);

        if (Credential?.Token?.IsStale == true)
        {
            bool refreshed = await Credential.RefreshTokenAsync(cancellationToken);
            if (!refreshed)
            {
                Credential = null;
                return null;
            }
        }

        if (Credential is null)
        {
            return null;
        }

        return new GoogleAccount(Credential.UserId);
    }
}
