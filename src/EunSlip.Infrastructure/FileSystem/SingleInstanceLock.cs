namespace EunSlip.Infrastructure.FileSystem;

public static class SingleInstanceLock
{
    public static IDisposable? TryAcquire(string lockFilePath)
    {
        string? directory = Path.GetDirectoryName(lockFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        try
        {
            return new FileStream(
                lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException ex) when (IsSharingViolation(ex))
        {
            return null;
        }
    }

    private static bool IsSharingViolation(IOException ex)
    {
        const int ErrorSharingViolation = 32;
        const int ErrorLockViolation = 33;
        int code = ex.HResult & 0xFFFF;
        return code is ErrorSharingViolation or ErrorLockViolation;
    }
}
