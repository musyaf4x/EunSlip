using System.Text;
using EunSlip.Core.Batches;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Sending;
using EunSlip.Infrastructure.Batches;
using EunSlip.Infrastructure.FileSystem;
using EunSlip.Infrastructure.Gmail;
using EunSlip.Infrastructure.Persistence;
using EunSlip.Infrastructure.Pdf;
using EunSlip.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace EunSlip.Infrastructure.Tests.Gmail;

public sealed class RealGmailUatTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SendSingleDummyPayslip_ToAuthenticatedOwnerAccount()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("EUNSLIP_RUN_REAL_GMAIL_UAT"),
            "1",
            StringComparison.Ordinal))
        {
            output.WriteLine("Real Gmail UAT not requested; set EUNSLIP_RUN_REAL_GMAIL_UAT=1 to run it.");
            return;
        }

        AppPaths paths = new(AppPaths.DefaultRoot);
        paths.EnsureCreated();
        SqliteAppRepository repository = new(
            $"Data Source={Path.Combine(paths.DatabaseDirectory, "eunslip.db")}");
        repository.Initialize();

        string storedSecret = Assert.IsType<string>(repository.GetSetting("OAuthClientSecret"));
        byte[] secretEnvelope = Convert.FromBase64String(storedSecret);
        string clientSecretJson = Encoding.UTF8.GetString(
            DpapiKeyProtector.UnprotectToken(secretEnvelope));

        GmailAuthorization authorization = new(
            new DpapiTokenDataStore(paths.OAuthDirectory),
            _ => Task.FromResult<string?>(clientSecretJson));
        GoogleAccount account = Assert.IsType<GoogleAccount>(
            await authorization.RestoreAsync(CancellationToken.None));
        Assert.Contains('@', account.Email);

        string stampPath = Assert.IsType<string>(new SharedFileStore(paths).GetActiveStampPath());
        Guid batchId = Guid.NewGuid();
        const string dummyNik = "UAT-SELF";
        PayrollRow row = new(
            dummyNik, "UAT Dummy Employee", "Quality Assurance", "Release Candidate",
            new DateOnly(2026, 7, 22), "UAT", account.Email,
            5_000_000L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L,
            100_000L, 100_000L, 50_000L, 50_000L, 0L,
            300_000L, 5_000_000L, 4_700_000L, 0m);

        byte[] key = DpapiKeyProtector.LoadOrCreateKey(Path.Combine(paths.OAuthDirectory, "aes.key"));
        using AesGcmSecretStore secretStore = new(key);
        repository.CreateBatch(new PayrollBatchRecord(
            batchId, "TASK 16 UAT", new DateOnly(2026, 7, 22),
            $"TASK-016-{batchId:N}", BatchStatus.Ready, DateTimeOffset.UtcNow,
            null, null, true, 1, 0, 0));
        repository.AddRecipient(new BatchRecipientRecord(
            Guid.NewGuid(), batchId, secretStore.Protect(dummyNik), secretStore.Protect(account.Email),
            "SELF", RecipientStatus.Pending, DateTimeOffset.UtcNow));

        TempFileService tempFiles = new(paths);
        BatchCoordinator coordinator = new(
            new PayslipPdfGenerator(),
            new GmailRetrySender(new GmailSender(authorization, new MimeMessageBuilder())),
            repository,
            secretStore,
            new SharedFileStore(paths),
            tempFiles,
            NullLogger<BatchCoordinator>.Instance);
        List<BatchProgress> progress = [];
        BatchRunResult result = await coordinator.RunBatchAsync(
            new BatchRunRequest(
                new BatchContext("TASK 16 UAT", new DateOnly(2026, 7, 22)),
                [row],
                "[UAT] EunSlip release candidate",
                "Dummy one-recipient UAT for EunSlip Task 16. No production payroll data is included.",
                "PT. EUNSUNG INDONESIA",
                batchId,
                AttemptType.Normal,
                new Progress<BatchProgress>(progress.Add)),
            CancellationToken.None);

        RecipientResult sent = Assert.Single(result.Results);
        Assert.True(sent.Succeeded, $"Gmail UAT failed: {sent.ErrorCategory}");
        Assert.Equal(1, result.SentCount);
        Assert.Single(repository.ListAttempts(batchId));
        Assert.Equal(RecipientStatus.Sent, Assert.Single(repository.ListRecipients(batchId)).Status);
        Assert.Empty(Directory.EnumerateFiles(
            Path.Combine(paths.TempDirectory, batchId.ToString("N")), "*.pdf"));
        output.WriteLine("TASK-016 real Gmail UAT PASS: one dummy payslip sent to the authenticated owner account.");
    }
}
