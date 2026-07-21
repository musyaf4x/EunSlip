using EunSlip.Infrastructure.FileSystem;

namespace EunSlip.Infrastructure.Tests.FileSystem;

public sealed class SingleInstanceLockTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid().ToString("N"));
    private string LockFile => Path.Combine(_directory, "eunslip.lock");

    [Fact]
    public void TryAcquire_ReturnsHandle_WhenLockIsFree()
    {
        using IDisposable? handle = SingleInstanceLock.TryAcquire(LockFile);

        Assert.NotNull(handle);
    }

    [Fact]
    public void TryAcquire_ReturnsNull_WhenLockIsHeld()
    {
        using IDisposable? first = SingleInstanceLock.TryAcquire(LockFile);

        using IDisposable? second = SingleInstanceLock.TryAcquire(LockFile);

        Assert.Null(second);
    }

    [Fact]
    public void TryAcquire_SucceedsAgain_AfterHandleIsReleased()
    {
        IDisposable? first = SingleInstanceLock.TryAcquire(LockFile);
        Assert.NotNull(first);
        first.Dispose();

        using IDisposable? second = SingleInstanceLock.TryAcquire(LockFile);

        Assert.NotNull(second);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
