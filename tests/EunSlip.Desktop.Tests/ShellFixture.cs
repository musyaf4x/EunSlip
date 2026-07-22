using System.IO;
using EunSlip.Core.Batches;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Recovery;
using EunSlip.Core.Security;
using EunSlip.Core.Sending;
using EunSlip.Desktop.ViewModels;
using EunSlip.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace EunSlip.Desktop.Tests;

internal sealed class ShellFixture
{
    private ShellFixture()
    {
        Repository = new RepositoryFake();
        Recovery = new RecoveryFake(Repository);
        GmailFake gmail = new();
        StampFake stamp = new();
        Home = new HomeViewModel(gmail, stamp, Repository);
        Wizard = new PayrollWizardViewModel(
            new ReaderFake(), new CoordinatorFake(), new PdfFake(), gmail, stamp,
            Repository, new SecretFake(), new TempFake(), Recovery,
            NullLogger<PayrollWizardViewModel>.Instance);
        History = new HistoryViewModel(Repository, NullLogger<HistoryViewModel>.Instance);
        Settings = new SettingsViewModel(gmail, stamp, Repository,
            NullLogger<SettingsViewModel>.Instance);
        About = new AboutViewModel(new AppPaths(
            Path.Combine(Path.GetTempPath(), "eunslip-shell-fixture")));
        Main = new MainViewModel(Home, Wizard, History, Settings, About);
    }

    public static ShellFixture Create() => new();

    public RepositoryFake Repository { get; }
    public RecoveryFake Recovery { get; }
    public HomeViewModel Home { get; }
    public PayrollWizardViewModel Wizard { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }
    public AboutViewModel About { get; }
    public MainViewModel Main { get; }

    public PayrollBatchRecord InterruptedBatch() => new(
        Guid.NewGuid(), "JULY 2025", new DateOnly(2026, 5, 11), "fp", BatchStatus.Interrupted,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, true, 1, 0, 0);

    internal sealed class RepositoryFake : IAppRepository
    {
        public List<PayrollBatchRecord> Batches { get; } = [];
        public List<BatchRecipientRecord> Recipients { get; } = [];
        public void Initialize() { }
        public bool CheckIntegrity() => true;
        public void ResetDatabase() { }
        public string? GetSetting(string key) => null;
        public void SetSetting(string key, string value) { }
        public Guid CreateBatch(PayrollBatchRecord batch) { Batches.Add(batch); return batch.Id; }
        public PayrollBatchRecord? GetBatch(Guid id) => Batches.FirstOrDefault(batch => batch.Id == id);
        public IReadOnlyList<PayrollBatchRecord> ListBatches() => Batches;
        public void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc)
        {
            int index = Batches.FindIndex(batch => batch.Id == id);
            if (index >= 0)
            {
                PayrollBatchRecord batch = Batches[index];
                Batches[index] = batch with
                {
                    Status = status,
                    StartedAtUtc = startedAtUtc ?? batch.StartedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                };
            }
        }
        public Guid AddRecipient(BatchRecipientRecord recipient) { Recipients.Add(recipient); return recipient.Id; }
        public IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId) =>
            [.. Recipients.Where(recipient => recipient.BatchId == batchId)];
        public IReadOnlyList<SendAttemptRecord> ListAttempts(Guid batchId) => [];
        public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAtUtc) { }
        public void AddAttempt(SendAttemptRecord attempt) { }
        public void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAtUtc,
            string? errorCategory, string? errorMessage, string? gmailMessageId)
        { }
        public AttemptStatus? GetLatestAttemptStatus(Guid recipientId) => null;
        public IReadOnlyList<Guid> FindInterruptedBatches() => [];
        public void ResetSendingRecipientsToPending(Guid batchId) { }
        public IReadOnlyList<string> FindPreviouslySentNiks(string period) => [];
        public void DeleteBatch(Guid id) { }
    }

    internal sealed class RecoveryFake(RepositoryFake repository) : IRecoveryService
    {
        public List<Guid> Prepared { get; } = [];
        public IReadOnlyList<Guid> DetectInterruptedBatches() => [];
        public IReadOnlyList<Guid> MarkDetectedBatchesInterrupted() => [];
        public void PrepareForRecovery(Guid batchId) => Prepared.Add(batchId);
        public RecoveryGate VerifyFingerprint(Guid batchId, BatchContext context, IReadOnlyList<PayrollRow> rows) =>
            new(RecoveryGateResult.Match, "fp", "fp");
        public IReadOnlyList<string> SelectRetryFailedNiks(Guid batchId) => [];
        public IReadOnlyList<string> SelectRecoveryResendNiks(Guid batchId) =>
            [.. repository.ListRecipients(batchId).Where(recipient => recipient.Status != RecipientStatus.Sent)
                .Select(recipient => recipient.EncryptedNik)];
    }

    private sealed class ReaderFake : IPayrollWorkbookReader
    {
        public WorkbookReadResult Read(string filePath) => throw new NotSupportedException();
    }

    private sealed class CoordinatorFake : IBatchCoordinator
    {
        public Task<BatchRunResult> RunBatchAsync(BatchRunRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new BatchRunResult(request.BatchId, []));
    }

    private sealed class PdfFake : IPayslipPdfGenerator
    {
        public void Generate(PayslipRequest request, string outputPath) { }
    }

    private sealed class GmailFake : IGmailAuthorization
    {
        public Task<GoogleAccount?> ConnectAsync(string clientSecretJson, CancellationToken cancellationToken) =>
            Task.FromResult<GoogleAccount?>(new GoogleAccount("payroll@example.com"));
        public Task<GoogleAccount?> RestoreAsync(CancellationToken cancellationToken) =>
            Task.FromResult<GoogleAccount?>(new GoogleAccount("payroll@example.com"));
        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class StampFake : ISharedFileStore
    {
        public string? GetActiveStampPath() => "stamp.png";
        public string ImportStamp(string sourcePath) => "stamp.png";
        public void RemoveStamp() { }
    }

    private sealed class SecretFake : ISecretStore
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string envelope) => envelope;
    }

    private sealed class TempFake : ITempFileService
    {
        public string CreateBatchTempDirectory(Guid batchId) => Path.GetTempPath();
        public void DeleteFile(string path) { }
        public void CleanupLeftovers() { }
    }
}
