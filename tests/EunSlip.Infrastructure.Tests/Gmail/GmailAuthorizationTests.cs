using EunSlip.Core.Sending;
using EunSlip.Infrastructure.Gmail;

namespace EunSlip.Infrastructure.Tests.Gmail;

public sealed class GmailAuthorizationTests
{
    [Fact]
    public async Task ResolveAccount_UsesAuthenticatedGmailProfileEmail()
    {
        GoogleAccount? account = await GmailAuthorization.ResolveAccountAsync(
            _ => Task.FromResult<string?>("owner@example.test"),
            CancellationToken.None);

        Assert.NotNull(account);
        Assert.Equal("owner@example.test", account.Email);
    }

    [Fact]
    public async Task ResolveAccount_BlankProfileEmail_IsNotConnected()
    {
        GoogleAccount? account = await GmailAuthorization.ResolveAccountAsync(
            _ => Task.FromResult<string?>("  "),
            CancellationToken.None);

        Assert.Null(account);
    }

    [Fact]
    public void ParseUserInfoEmail_ReadsOpenIdEmailClaim()
    {
        string? email = GmailAuthorization.ParseUserInfoEmail(
            "{\"sub\":\"opaque-id\",\"email\":\"owner@example.test\"}");

        Assert.Equal("owner@example.test", email);
    }

    [Fact]
    public void ParseUserInfoEmail_MissingEmailClaim_ReturnsNull()
    {
        Assert.Null(GmailAuthorization.ParseUserInfoEmail("{\"sub\":\"opaque-id\"}"));
    }
}
