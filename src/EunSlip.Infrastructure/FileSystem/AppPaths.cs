namespace EunSlip.Infrastructure.FileSystem;

public sealed class AppPaths(string root)
{
    public static string DefaultRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "EunSlip");

    public string Root { get; } = root;
    public string DatabaseDirectory => Path.Combine(Root, "database");
    public string StampDirectory => Path.Combine(Root, "stamp");
    public string OAuthDirectory => Path.Combine(Root, "oauth");
    public string TempDirectory => Path.Combine(Root, "temp");
    public string LogsDirectory => Path.Combine(Root, "logs");
    public string RuntimeDirectory => Path.Combine(Root, "runtime");
    public string LockFilePath => Path.Combine(RuntimeDirectory, "eunslip.lock");

    public void EnsureCreated()
    {
        _ = Directory.CreateDirectory(DatabaseDirectory);
        _ = Directory.CreateDirectory(StampDirectory);
        _ = Directory.CreateDirectory(OAuthDirectory);
        _ = Directory.CreateDirectory(TempDirectory);
        _ = Directory.CreateDirectory(LogsDirectory);
        _ = Directory.CreateDirectory(RuntimeDirectory);
    }
}
