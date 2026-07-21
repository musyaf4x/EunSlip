using EunSlip.Infrastructure.FileSystem;
using EunSlip.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace EunSlip.Infrastructure.Tests.Logging;

public sealed class EunSlipLoggingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoggerFactory_WritesLogFile_UnderLogsDirectory()
    {
        AppPaths paths = new(_root);
        paths.EnsureCreated();

        using (ILoggerFactory factory = EunSlipLogging.CreateLoggerFactory(paths))
        {
            ILogger logger = factory.CreateLogger("test");
            logger.LogInformation("startup probe {Operation}", "boot");
        }

        string[] files = Directory.GetFiles(paths.LogsDirectory, "*.log");
        Assert.NotEmpty(files);
        string content = File.ReadAllText(files[0]);
        Assert.Contains("startup probe", content);
        Assert.Contains("boot", content);
    }

    [Fact]
    public void LoggerFactory_RedactsSensitivePropertyValues()
    {
        AppPaths paths = new(_root);
        paths.EnsureCreated();

        using (ILoggerFactory factory = EunSlipLogging.CreateLoggerFactory(paths))
        {
            ILogger logger = factory.CreateLogger("test");
            logger.LogInformation("send result {Nik} {Email} {Token}", "12345", "a@b.c", "secret-token");
        }

        string[] files = Directory.GetFiles(paths.LogsDirectory, "*.log");
        string content = File.ReadAllText(files[0]);
        Assert.DoesNotContain("12345", content);
        Assert.DoesNotContain("a@b.c", content);
        Assert.DoesNotContain("secret-token", content);
    }

    [Fact]
    public void LoggerFactory_RedactsNestedDestructuredValues()
    {
        AppPaths paths = new(_root);
        paths.EnsureCreated();

        using (ILoggerFactory factory = EunSlipLogging.CreateLoggerFactory(paths))
        {
            ILogger logger = factory.CreateLogger("test");
            logger.LogInformation("payload {@Recipient}", new { Name = "Budi", Nik = "99999", Email = "x@y.z" });
        }

        string[] files = Directory.GetFiles(paths.LogsDirectory, "*.log");
        string content = File.ReadAllText(files[0]);
        Assert.Contains("Budi", content);
        Assert.DoesNotContain("99999", content);
        Assert.DoesNotContain("x@y.z", content);
    }

    [Fact]
    public void LoggerFactory_RedactsPluralAndPrefixedEmailProperty()
    {
        AppPaths paths = new(_root);
        paths.EnsureCreated();

        using (ILoggerFactory factory = EunSlipLogging.CreateLoggerFactory(paths))
        {
            ILogger logger = factory.CreateLogger("test");
            logger.LogInformation("send {ToEmail} {Emails}", "a@b.c", "x@y.z");
        }

        string[] files = Directory.GetFiles(paths.LogsDirectory, "*.log");
        string content = File.ReadAllText(files[0]);
        Assert.DoesNotContain("a@b.c", content);
        Assert.DoesNotContain("x@y.z", content);
    }

    [Fact]
    public void LoggerFactory_RedactsExceptionMessage()
    {
        AppPaths paths = new(_root);
        paths.EnsureCreated();

        using (ILoggerFactory factory = EunSlipLogging.CreateLoggerFactory(paths))
        {
            ILogger logger = factory.CreateLogger("test");
            logger.LogError(new System.Exception("failed for a@b.co"), "send error");
        }

        string[] files = Directory.GetFiles(paths.LogsDirectory, "*.log");
        string content = File.ReadAllText(files[0]);
        Assert.DoesNotContain("a@b.c", content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
