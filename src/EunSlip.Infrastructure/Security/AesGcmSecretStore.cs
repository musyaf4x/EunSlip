using System.Security.Cryptography;
using System.Text;
using EunSlip.Core.Security;

namespace EunSlip.Infrastructure.Security;

public sealed class AesGcmSecretStore : ISecretStore, IDisposable
{
    private const byte Version = 1;
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly AesGcm _aes;
    private readonly byte[] _key;
    private bool _disposed;

    public AesGcmSecretStore(byte[] key)
    {
        if (key.Length != KeySizeBytes)
        {
            throw new ArgumentException($"AES key must be {KeySizeBytes} bytes.", nameof(key));
        }

        _key = key;
        _aes = new AesGcm(_key, TagSizeBytes);
    }

    public string Protect(string plaintext)
    {
        ThrowIfDisposed();
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[TagSizeBytes];

        _aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        byte[] envelope =
        [
            Version,
            .. nonce,
            .. tag,
            .. ciphertext,
        ];
        return Convert.ToBase64String(envelope);
    }

    public string Unprotect(string envelopeBase64)
    {
        ThrowIfDisposed();
        byte[] envelope = Convert.FromBase64String(envelopeBase64);
        if (envelope.Length < 1 + NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Envelope is too short.");
        }

        if (envelope[0] != Version)
        {
            throw new CryptographicException($"Unsupported envelope version {envelope[0]}.");
        }

        byte[] nonce = new byte[NonceSizeBytes];
        byte[] tag = new byte[TagSizeBytes];
        int ciphertextLength = envelope.Length - 1 - NonceSizeBytes - TagSizeBytes;
        byte[] ciphertext = new byte[ciphertextLength];

        Buffer.BlockCopy(envelope, 1, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(envelope, 1 + NonceSizeBytes, tag, 0, TagSizeBytes);
        Buffer.BlockCopy(envelope, 1 + NonceSizeBytes + TagSizeBytes, ciphertext, 0, ciphertextLength);

        byte[] plaintext = new byte[ciphertextLength];
        _aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _aes.Dispose();
        CryptographicOperations.ZeroMemory(_key);
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
