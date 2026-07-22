using System.IO;
using EunSlip.Core.Batches;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Recovery;
using EunSlip.Core.Security;
using EunSlip.Core.Sending;
using EunSlip.Core.Validation;
using EunSlip.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace EunSlip.Desktop.Tests;

public sealed class PayrollWizardViewModelTests
{
    private static PayrollRowInput ValidInput(int n) => new(
        n + 1, $"NIK{n:D4}", "Employee", "Fin", "Staff", new DateOnly(2020, 1, 15),
        "Monthly", $"e{n}@e.co", 5_000_000m, null, null, null, null, null, null, null,
        null, null, null, 100_000m, 100_000m, 50_000m, 50_000m, null,
        300_000m, 5_000_000m, 4_700_000m, null);

    private sealed class FakeReader(Func<string, WorkbookReadResult> behavior) : IPayrollWorkbookReader
    {
        public WorkbookReadResult Read(string filePath) => behavior(filePath);
    }

    private sealed class FakePdfGenerator : IPayslipPdfGenerator
    {
        public void Generate(PayslipRequest request, string outputPath) =>
            System.IO.File.WriteAllBytes(outputPath, [1, 2, 3]);
    }

    private sealed class FakeTempFiles : ITempFileService
    {
        public string CreateBatchTempDirectory(Guid batchId)
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eunslip-test", Guid.NewGuid().ToString("N"));
            _ = System.IO.Directory.CreateDirectory(dir);
            return dir;
        }
        public void DeleteFile(string path) { }
        public void CleanupLeftovers() { }
    }

    private sealed class FakeCoordinator : IBatchCoordinator
    {
        public bool WasCalled { get; private set; }
        public BatchRunRequest? LastRequest { get; private set; }
        public Task<BatchRunResult> RunBatchAsync(BatchRunRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastRequest = request;
            List<RecipientResult> results = [.. request.Rows
                .Select(r => new RecipientResult(r.Nik, r.Nama, r.Email, true, 1, null, null))];
            return Task.FromResult(new BatchRunResult(request.BatchId, results));
        }
    }

    private sealed class FakeGmail(bool connected, string? email = null) : IGmailAuthorization
    {
        public bool Connected { get; set; } = connected;
        public Task<GoogleAccount?> ConnectAsync(string clientSecretJson, CancellationToken cancellationToken)
            => Task.FromResult<GoogleAccount?>(Connected ? new GoogleAccount(email ?? "g@e.co") : null);
        public Task<GoogleAccount?> RestoreAsync(CancellationToken cancellationToken)
            => Task.FromResult<GoogleAccount?>(Connected ? new GoogleAccount(email ?? "g@e.co") : null);
        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            Connected = false;
            return Task.CompletedTask;
        }
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken) => Task.FromResult(Connected);
    }

    private sealed class FakeStampStore(bool hasStamp) : ISharedFileStore
    {
        public bool HasStamp { get; set; } = hasStamp;
        public string? GetActiveStampPath() => HasStamp ? "stamp.png" : null;
        public string ImportStamp(string sourcePath)
        {
            HasStamp = true;
            return "stamp.png";
        }
        public void RemoveStamp() => HasStamp = false;
    }

    private sealed class FakeRecovery : IRecoveryService
    {
        public List<Guid> Prepared { get; } = [];
        public IReadOnlyList<string> FailedNiks { get; init; } = ["NIK0001"];
        public IReadOnlyList<string> RecoveryNiks { get; init; } = ["NIK0001"];
        public bool FingerprintMatches { get; init; } = true;
        public IReadOnlyList<Guid> DetectInterruptedBatches() => [];
        public IReadOnlyList<Guid> MarkDetectedBatchesInterrupted() => [];
        public void PrepareForRecovery(Guid batchId) => Prepared.Add(batchId);
        public RecoveryGate VerifyFingerprint(Guid batchId, BatchContext context, IReadOnlyList<PayrollRow> rows) =>
            FingerprintMatches
                ? new RecoveryGate(RecoveryGateResult.Match, "stored", "stored")
                : new RecoveryGate(RecoveryGateResult.Mismatch, "stored", "different");
        public IReadOnlyList<string> SelectRetryFailedNiks(Guid batchId) => FailedNiks;
        public IReadOnlyList<string> SelectRecoveryResendNiks(Guid batchId) => RecoveryNiks;
    }

    private sealed class FakeRepository : IAppRepository
    {
        public List<PayrollBatchRecord> Batches { get; } = [];
        public List<BatchRecipientRecord> Recipients { get; } = [];
        public void Initialize() { }
        public bool CheckIntegrity() => true;
        public void ResetDatabase() { }
        public string? GetSetting(string key) => null;
        public void SetSetting(string key, string value) { }
        public Guid CreateBatch(PayrollBatchRecord batch) { Batches.Add(batch); return batch.Id; }
        public PayrollBatchRecord? GetBatch(Guid id) => Batches.FirstOrDefault(b => b.Id == id);
        public IReadOnlyList<PayrollBatchRecord> ListBatches() => Batches;
        public void UpdateBatchStatus(Guid id, BatchStatus status, DateTimeOffset? startedAt, DateTimeOffset? completedAt) { }
        public Guid AddRecipient(BatchRecipientRecord recipient) { Recipients.Add(recipient); return recipient.Id; }
        public IReadOnlyList<BatchRecipientRecord> ListRecipients(Guid batchId) => [.. Recipients.Where(r => r.BatchId == batchId)];
        public IReadOnlyList<SendAttemptRecord> ListAttempts(Guid batchId) => [];
        public void UpdateRecipientStatus(Guid recipientId, RecipientStatus status, DateTimeOffset updatedAt) { }
        public void AddAttempt(SendAttemptRecord attempt) { }
        public void CompleteAttempt(Guid attemptId, AttemptStatus status, DateTimeOffset completedAt, string? errorCategory, string? errorMessage, string? gmailMessageId) { }
        public AttemptStatus? GetLatestAttemptStatus(Guid recipientId) => null;
        public IReadOnlyList<Guid> FindInterruptedBatches() => [];
        public void ResetSendingRecipientsToPending(Guid batchId) { }
        public IReadOnlyList<string> FindPreviouslySentNiks(string period) => [];
        public void DeleteBatch(Guid id) { }
    }

    private sealed class PassThroughSecretStore : ISecretStore
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string envelope) => envelope;
    }

    private static PayrollWizardViewModel Create(
        IPayrollWorkbookReader? reader = null,
        bool gmailConnected = true,
        bool hasStamp = true,
        IBatchCoordinator? coordinator = null,
        FakeRecovery? recovery = null,
        FakeGmail? gmail = null,
        FakeStampStore? stampStore = null) =>
        Create(out _, reader, gmailConnected, hasStamp, coordinator, recovery, gmail, stampStore);

    private static PayrollWizardViewModel Create(
        out FakeRepository repository,
        IPayrollWorkbookReader? reader = null,
        bool gmailConnected = true,
        bool hasStamp = true,
        IBatchCoordinator? coordinator = null,
        FakeRecovery? recovery = null,
        FakeGmail? gmail = null,
        FakeStampStore? stampStore = null)
    {
        repository = new FakeRepository();
        return new PayrollWizardViewModel(
            reader ?? new FakeReader(_ => new WorkbookReadResult(PayrollContract.Headers, [ValidInput(1)], [])),
            coordinator ?? new FakeCoordinator(),
            new FakePdfGenerator(),
            gmail ?? new FakeGmail(gmailConnected),
            stampStore ?? new FakeStampStore(hasStamp),
            repository,
            new PassThroughSecretStore(),
            new FakeTempFiles(),
            recovery ?? new FakeRecovery(),
            NullLogger<PayrollWizardViewModel>.Instance);
    }

    private static void FillSelectStep(PayrollWizardViewModel vm, string file = "payroll.xlsx")
    {
        vm.SelectedFilePath = file;
        vm.Period = "JULY 2025";
        vm.PaymentDate = new DateTime(2026, 5, 11);
    }

    private static FakeReader ReaderWithRows(params PayrollRowInput[] rows) =>
        new(_ => new WorkbookReadResult(PayrollContract.Headers, rows, []));

    private static PayrollBatchRecord BatchForRows(Guid id, string period, params PayrollRowInput[] rows)
    {
        DateOnly paymentDate = new(2026, 5, 11);
        ValidationResult validation = PayrollValidator.Validate(PayrollContract.Headers, rows, null);
        string fingerprint = PayrollFingerprint.Compute(new BatchContext(period, paymentDate), validation.ValidRows);
        return new PayrollBatchRecord(id, period, paymentDate, fingerprint, BatchStatus.Completed,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true,
            validation.ValidRows.Count, validation.ValidRows.Count - 1, 1);
    }

    private static void SeedRecipients(FakeRepository repository, Guid batchId, params string[] niks)
    {
        foreach (string nik in niks)
        {
            repository.AddRecipient(new BatchRecipientRecord(Guid.NewGuid(), batchId, nik,
                $"{nik}@example.com", NikHint.LastFour(nik), RecipientStatus.Failed, DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public void NextCommand_Disabled_WhenSelectStepIncomplete()
    {
        PayrollWizardViewModel vm = Create();

        Assert.False(vm.NextCommand.CanExecute(null));
    }

    [Fact]
    public void NextCommand_Enabled_WhenSelectStepComplete()
    {
        PayrollWizardViewModel vm = Create();
        FillSelectStep(vm);

        Assert.True(vm.NextCommand.CanExecute(null));
    }

    [Fact]
    public async Task Next_OnSelect_RunsValidation_AndAdvancesOnSuccess()
    {
        PayrollWizardViewModel vm = Create();
        FillSelectStep(vm);

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Validate, vm.CurrentStep);
        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.ValidationRows);
    }

    [Fact]
    public async Task Next_OnSelect_UnreadableWorkbook_StaysOnSelect_WithErrorMessage()
    {
        PayrollWizardViewModel vm = Create(
            new FakeReader(_ => throw new WorkbookUnreadableException("bad")));
        FillSelectStep(vm);

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Select, vm.CurrentStep);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task Next_OnSelect_ValidationBlocking_StaysAndShowsError()
    {
        string[] badHeaders = [.. PayrollContract.Headers];
        badHeaders[0] = "WRONG";
        PayrollWizardViewModel vm = Create(
            new FakeReader(_ => new WorkbookReadResult(badHeaders, [ValidInput(1)], [])));
        FillSelectStep(vm);

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Select, vm.CurrentStep);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task Next_OnSelect_ReaderBlockingIssue_StaysOnSelect()
    {
        PayrollIssue blocking = new(IssueSeverity.Blocking, "CachedValueMissing", 2, null,
            "Total Potongan", null, null);
        PayrollWizardViewModel vm = Create(
            new FakeReader(_ => new WorkbookReadResult(PayrollContract.Headers, [ValidInput(1)], [blocking])));
        FillSelectStep(vm);

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Select, vm.CurrentStep);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task Next_OnSelect_WithWarnings_AdvancesButBlocksUntilConfirmed()
    {
        PayrollRowInput row = ValidInput(1) with { Total = 1m };
        PayrollWizardViewModel vm = Create(
            new FakeReader(_ => new WorkbookReadResult(PayrollContract.Headers, [row], [])));
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Validate, vm.CurrentStep);
        Assert.True(vm.HasWarnings);
        Assert.False(vm.NextCommand.CanExecute(null));

        vm.WarningConfirmed = true;
        Assert.True(vm.NextCommand.CanExecute(null));
    }

    [Fact]
    public async Task Back_FromValidate_ReturnsToSelect()
    {
        PayrollWizardViewModel vm = Create();
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);

        vm.BackCommand.Execute(null);

        Assert.Equal(WizardStep.Select, vm.CurrentStep);
    }

    [Fact]
    public async Task ConfirmSend_Disabled_WhenGmailNotConnected()
    {
        PayrollWizardViewModel vm = Create(gmailConnected: false);
        await vm.LoadedCommand.ExecuteAsync(null);
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep = WizardStep.Confirm;

        Assert.False(vm.ConfirmSendCommand.CanExecute(null));
    }

    [Fact]
    public async Task ConfirmSend_Disabled_WhenStampMissing()
    {
        PayrollWizardViewModel vm = Create(hasStamp: false);
        await vm.LoadedCommand.ExecuteAsync(null);
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep = WizardStep.Confirm;

        Assert.False(vm.ConfirmSendCommand.CanExecute(null));
    }

    [Fact]
    public async Task ConfirmSend_Enabled_WhenReady()
    {
        PayrollWizardViewModel vm = Create();
        await vm.LoadedCommand.ExecuteAsync(null);
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep = WizardStep.Confirm;

        Assert.True(vm.ConfirmSendCommand.CanExecute(null));
    }

    [Fact]
    public async Task Loaded_RefreshesPrerequisitesAndEnablesConfirm()
    {
        PayrollWizardViewModel vm = Create();
        vm.Begin(PayrollWizardEntry.Normal());
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep = WizardStep.Confirm;

        Assert.False(vm.ConfirmSendCommand.CanExecute(null));
        await vm.LoadedCommand.ExecuteAsync(null);

        Assert.True(vm.GmailReady);
        Assert.True(vm.StampReady);
        Assert.True(vm.ConfirmSendCommand.CanExecute(null));
    }

    [Fact]
    public async Task MovingFromPreviewToConfirm_RefreshesPrerequisites()
    {
        FakeGmail gmail = new(false);
        FakeStampStore stamp = new(false);
        PayrollWizardViewModel vm = Create(gmail: gmail, stampStore: stamp);
        vm.Begin(PayrollWizardEntry.Normal());
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep = WizardStep.Preview;
        gmail.Connected = true;
        stamp.HasStamp = true;

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Confirm, vm.CurrentStep);
        Assert.True(vm.ConfirmSendCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedRetry_ReusesBatchFiltersRowsAndUsesFailedRetryAttemptType()
    {
        Guid batchId = Guid.NewGuid();
        FakeCoordinator coordinator = new();
        FakeRecovery recovery = new() { FailedNiks = ["NIK0002"] };
        PayrollWizardViewModel vm = Create(out FakeRepository repository,
            reader: ReaderWithRows(ValidInput(1), ValidInput(2)), coordinator: coordinator, recovery: recovery);
        repository.CreateBatch(BatchForRows(batchId, "JULY 2025", ValidInput(1), ValidInput(2)));
        SeedRecipients(repository, batchId, "NIK0001", "NIK0002");
        Assert.True(vm.Begin(PayrollWizardEntry.FailedRetry(batchId)));
        vm.SelectedFilePath = "payroll.xlsx";

        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep = WizardStep.Confirm;
        await vm.LoadedCommand.ExecuteAsync(null);
        await vm.ConfirmSendCommand.ExecuteAsync(null);

        Assert.Equal(batchId, coordinator.LastRequest!.BatchId);
        Assert.Equal(AttemptType.FailedRetry, coordinator.LastRequest.AttemptKind);
        Assert.Single(coordinator.LastRequest.Rows, row => row.Nik == "NIK0002");
        Assert.Empty(recovery.Prepared);
    }

    [Fact]
    public async Task Recovery_PreparesOnlyAtConfirmedSendAndExcludesSentRecipients()
    {
        Guid batchId = Guid.NewGuid();
        FakeCoordinator coordinator = new();
        FakeRecovery recovery = new() { RecoveryNiks = ["NIK0002"] };
        PayrollWizardViewModel vm = Create(out FakeRepository repository,
            reader: ReaderWithRows(ValidInput(1), ValidInput(2)), coordinator: coordinator, recovery: recovery);
        repository.CreateBatch(BatchForRows(batchId, "JULY 2025", ValidInput(1), ValidInput(2)) with
        {
            Status = BatchStatus.Interrupted,
        });
        SeedRecipients(repository, batchId, "NIK0001", "NIK0002");
        Assert.True(vm.Begin(PayrollWizardEntry.RecoveryRetry(batchId)));
        vm.SelectedFilePath = "payroll.xlsx";

        await vm.NextCommand.ExecuteAsync(null);
        Assert.Empty(recovery.Prepared);
        vm.CurrentStep = WizardStep.Confirm;
        await vm.LoadedCommand.ExecuteAsync(null);
        await vm.ConfirmSendCommand.ExecuteAsync(null);

        Assert.Single(recovery.Prepared, batchId);
        Assert.Equal(AttemptType.RecoveryRetry, coordinator.LastRequest!.AttemptKind);
        Assert.Single(coordinator.LastRequest.Rows, row => row.Nik == "NIK0002");
    }

    [Fact]
    public async Task Resume_FingerprintMismatchBlocksBeforeRecipientFiltering()
    {
        FakeRecovery recovery = new() { FingerprintMatches = false };
        PayrollWizardViewModel vm = Create(out FakeRepository repository, recovery: recovery);
        Guid batchId = repository.CreateBatch(BatchForRows(Guid.NewGuid(), "JULY 2025", ValidInput(1)));
        Assert.True(vm.Begin(PayrollWizardEntry.FailedRetry(batchId)));
        vm.SelectedFilePath = "different.xlsx";

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Select, vm.CurrentStep);
        Assert.Contains("fingerprint", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(repository.Batches, batch => batch.Id != batchId);
    }

    [Fact]
    public async Task ConfirmSend_ExecutesBatch_AndReachesResults()
    {
        PayrollWizardViewModel vm = Create();
        await vm.LoadedCommand.ExecuteAsync(null);
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep = WizardStep.Confirm;

        await vm.ConfirmSendCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Results, vm.CurrentStep);
        Assert.Single(vm.Results);
        Assert.Equal(1, vm.SentCount);
    }

    [Fact]
    public async Task CanGoNext_FalseOnSendStep()
    {
        PayrollWizardViewModel vm = Create();
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep = WizardStep.Send;

        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public async Task Next_OnSelect_PersistsRecipientsToRepository()
    {
        PayrollWizardViewModel vm = Create(out FakeRepository repo);
        FillSelectStep(vm);

        await vm.NextCommand.ExecuteAsync(null);

        Assert.NotEmpty(repo.Recipients);
        BatchRecipientRecord recipient = Assert.Single(repo.Recipients);
        Assert.Equal("NIK0001", recipient.EncryptedNik);
        Assert.Equal(RecipientStatus.Pending, recipient.Status);
    }

    [Fact]
    public async Task GeneratePreviewPdf_ReturnsGeneratedPathWithoutLaunchingExternalViewer()
    {
        PayrollWizardViewModel vm = Create(out _, hasStamp: true);
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep = WizardStep.Preview;

        string? path = vm.GeneratePreviewPdf();

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void Reset_ReturnsToSelectAndClearsState()
    {
        PayrollWizardViewModel vm = Create();
        vm.SelectedFilePath = "x";
        vm.Period = "P";
        vm.ErrorMessage = "err";
        vm.CurrentStep = WizardStep.Results;

        vm.Reset();

        Assert.Equal(WizardStep.Select, vm.CurrentStep);
        Assert.Null(vm.SelectedFilePath);
        Assert.Null(vm.ErrorMessage);
        Assert.Empty(vm.ValidationRows);
    }
}
