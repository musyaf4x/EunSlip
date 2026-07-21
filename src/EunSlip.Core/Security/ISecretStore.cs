namespace EunSlip.Core.Security;

public interface ISecretStore
{
    string Protect(string plaintext);
    string Unprotect(string envelopeBase64);
}

public sealed class SecretStoreUnavailableException(
    string message, Exception? innerException = null) : Exception(message, innerException);
