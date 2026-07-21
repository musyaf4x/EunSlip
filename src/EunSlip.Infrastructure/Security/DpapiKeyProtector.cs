using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace EunSlip.Infrastructure.Security;

public static class DpapiKeyProtector
{
    private const byte Version = 1;

    [SupportedOSPlatform("windows")]
    public static byte[] LoadOrCreateKey(string keyFilePath)
    {
        string? directory = Path.GetDirectoryName(keyFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        if (File.Exists(keyFilePath))
        {
            return UnprotectKey(File.ReadAllBytes(keyFilePath));
        }

        byte[] key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(keyFilePath, ProtectKey(key));
        return key;
    }

    [SupportedOSPlatform("windows")]
    public static byte[] ProtectToken(byte[] tokenBytes)
    {
        byte[] protectedBytes = System.Security.Cryptography.ProtectedData.Protect(
            tokenBytes, optionalEntropy: null, DataProtectionScope.LocalMachine);
        return [Version, .. protectedBytes];
    }

    [SupportedOSPlatform("windows")]
    public static byte[] UnprotectToken(byte[] envelope)
    {
        if (envelope.Length < 1 || envelope[0] != Version)
        {
            throw new CryptographicException("Unsupported token envelope version.");
        }

        byte[] payload = new byte[envelope.Length - 1];
        Buffer.BlockCopy(envelope, 1, payload, 0, payload.Length);
        return System.Security.Cryptography.ProtectedData.Unprotect(
            payload, optionalEntropy: null, DataProtectionScope.LocalMachine);
    }

    [SupportedOSPlatform("windows")]
    private static byte[] ProtectKey(byte[] key)
    {
        byte[] protectedBytes = System.Security.Cryptography.ProtectedData.Protect(
            key, optionalEntropy: null, DataProtectionScope.LocalMachine);
        return [Version, .. protectedBytes];
    }

    [SupportedOSPlatform("windows")]
    private static byte[] UnprotectKey(byte[] envelope)
    {
        if (envelope.Length < 1 || envelope[0] != Version)
        {
            throw new CryptographicException("Unsupported key envelope version.");
        }

        byte[] payload = new byte[envelope.Length - 1];
        Buffer.BlockCopy(envelope, 1, payload, 0, payload.Length);
        return System.Security.Cryptography.ProtectedData.Unprotect(
            payload, optionalEntropy: null, DataProtectionScope.LocalMachine);
    }
}
