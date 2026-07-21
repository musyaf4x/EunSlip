using System.Security.Cryptography;
using EunSlip.Infrastructure.Security;

namespace EunSlip.Infrastructure.Tests.Security;

public sealed class AesGcmSecretStoreTests
{
    private static AesGcmSecretStore Create() => new(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void ProtectThenUnprotect_RoundTripsValue()
    {
        AesGcmSecretStore store = Create();

        string envelope = store.Protect("NIK0001");
        string result = store.Unprotect(envelope);

        Assert.Equal("NIK0001", result);
    }

    [Fact]
    public void Protect_UniqueNonceEveryCall()
    {
        AesGcmSecretStore store = Create();

        string first = store.Protect("same-value");
        string second = store.Protect("same-value");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Unprotect_WrongKey_ThrowsCryptographicException()
    {
        AesGcmSecretStore storeA = new(RandomNumberGenerator.GetBytes(32));
        AesGcmSecretStore storeB = new(RandomNumberGenerator.GetBytes(32));

        string envelope = storeA.Protect("secret");

        Assert.ThrowsAny<CryptographicException>(() => storeB.Unprotect(envelope));
    }

    [Fact]
    public void Unprotect_TamperedCiphertext_Throws()
    {
        AesGcmSecretStore store = Create();
        byte[] envelope = Convert.FromBase64String(store.Protect("secret"));

        envelope[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => store.Unprotect(Convert.ToBase64String(envelope)));
    }

    [Fact]
    public void Unprotect_TamperedTag_Throws()
    {
        AesGcmSecretStore store = Create();
        byte[] envelope = Convert.FromBase64String(store.Protect("secret"));

        envelope[5] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => store.Unprotect(Convert.ToBase64String(envelope)));
    }

    [Fact]
    public void Protect_UnicodeValue_RoundTrips()
    {
        AesGcmSecretStore store = Create();

        string envelope = store.Protect("Budi Santoso — Café");
        string result = store.Unprotect(envelope);

        Assert.Equal("Budi Santoso — Café", result);
    }

    [Fact]
    public void Protect_EmptyValue_RoundTrips()
    {
        AesGcmSecretStore store = Create();

        string envelope = store.Protect(string.Empty);
        string result = store.Unprotect(envelope);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Constructor_InvalidKeySize_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AesGcmSecretStore(new byte[16]));
    }
}
