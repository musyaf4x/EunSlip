namespace EunSlip.Core.Payroll;

public interface ISharedFileStore
{
    string? GetActiveStampPath();
    string ImportStamp(string sourcePath);
    void RemoveStamp();
}

public sealed class StampValidationException(string message) : Exception(message);
