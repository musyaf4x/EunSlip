using EunSlip.Infrastructure.FileSystem;

namespace EunSlip.Infrastructure.Tests.FileSystem;

public sealed class AppPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AllPaths_AreUnderRoot_WithExpectedSubdirectories()
    {
        AppPaths paths = new(_root);

        Assert.Equal(_root, paths.Root);
        Assert.Equal(Path.Combine(_root, "database"), paths.DatabaseDirectory);
        Assert.Equal(Path.Combine(_root, "stamp"), paths.StampDirectory);
        Assert.Equal(Path.Combine(_root, "oauth"), paths.OAuthDirectory);
        Assert.Equal(Path.Combine(_root, "temp"), paths.TempDirectory);
        Assert.Equal(Path.Combine(_root, "logs"), paths.LogsDirectory);
        Assert.Equal(Path.Combine(_root, "runtime"), paths.RuntimeDirectory);
        Assert.Equal(Path.Combine(_root, "runtime", "eunslip.lock"), paths.LockFilePath);
    }

    [Fact]
    public void DefaultRoot_IsProgramDataEunSlip()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EunSlip");

        Assert.Equal(expected, AppPaths.DefaultRoot);
    }

    [Fact]
    public void EnsureCreated_CreatesAllDirectories()
    {
        AppPaths paths = new(_root);

        paths.EnsureCreated();

        Assert.True(Directory.Exists(paths.DatabaseDirectory));
        Assert.True(Directory.Exists(paths.StampDirectory));
        Assert.True(Directory.Exists(paths.OAuthDirectory));
        Assert.True(Directory.Exists(paths.TempDirectory));
        Assert.True(Directory.Exists(paths.LogsDirectory));
        Assert.True(Directory.Exists(paths.RuntimeDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
