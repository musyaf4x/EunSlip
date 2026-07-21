using System.Security.Cryptography;
using EunSlip.Infrastructure.Security;

namespace EunSlip.Infrastructure.Tests.Security;

public sealed class DpapiKeyProtectorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid().ToString("N"));

    private string KeyPath => Path.Combine(_directory, "aes.key");

    [Fact]
    public void LoadOrCreateKey_CreatesFileOnFirstCall()
    {
        byte[] key = DpapiKeyProtector.LoadOrCreateKey(KeyPath);

        Assert.Equal(32, key.Length);
        Assert.True(File.Exists(KeyPath));
    }

    [Fact]
    public void LoadOrCreateKey_ReturnsSameKeyOnSubsequentCalls()
    {
        byte[] first = DpapiKeyProtector.LoadOrCreateKey(KeyPath);
        byte[] second = DpapiKeyProtector.LoadOrCreateKey(KeyPath);

        Assert.Equal(first, second);
    }

    [Fact]
    public void KeyFile_ContainsVersionedEnvelopeNotPlaintext()
    {
        byte[] key = DpapiKeyProtector.LoadOrCreateKey(KeyPath);
        byte[] fileBytes = File.ReadAllBytes(KeyPath);

        Assert.Equal(1, fileBytes[0]);
        Assert.NotEqual(key, fileBytes.AsSpan(1).ToArray());
    }

    [Fact]
    public void ProtectThenUnprotectToken_RoundTrips()
    {
        byte[] token = RandomNumberGenerator.GetBytes(64);

        byte[] envelope = DpapiKeyProtector.ProtectToken(token);
        byte[] result = DpapiKeyProtector.UnprotectToken(envelope);

        Assert.Equal(token, result);
    }

    [Fact]
    public void KeyFileTampered_ThrowsOnLoad()
    {
        _ = DpapiKeyProtector.LoadOrCreateKey(KeyPath);
        byte[] bytes = File.ReadAllBytes(KeyPath);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(KeyPath, bytes);

        Assert.ThrowsAny<CryptographicException>(() => DpapiKeyProtector.LoadOrCreateKey(KeyPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
