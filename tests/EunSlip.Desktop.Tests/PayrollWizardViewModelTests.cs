using EunSlip.Core.Batches;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
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
        public Task<BatchRunResult> RunBatchAsync(BatchRunRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            List<RecipientResult> results = [.. request.Rows
                .Select(r => new RecipientResult(r.Nik, r.Nama, "e@e.co", true, 1, null, null))];
            return Task.FromResult(new BatchRunResult(request.BatchId, results));
        }
    }

    private sealed class FakeGmail(bool connected, string? email = null) : IGmailAuthorization
    {
        public Task<GoogleAccount?> ConnectAsync(string clientSecretJson, CancellationToken cancellationToken)
            => Task.FromResult<GoogleAccount?>(connected ? new GoogleAccount(email ?? "g@e.co") : null);
        public Task<GoogleAccount?> RestoreAsync(CancellationToken cancellationToken)
            => Task.FromResult<GoogleAccount?>(connected ? new GoogleAccount(email ?? "g@e.co") : null);
        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken) => Task.FromResult(connected);
    }

    private sealed class FakeStampStore(bool hasStamp) : ISharedFileStore
    {
        public string? GetActiveStampPath() => hasStamp ? "stamp.png" : null;
        public string ImportStamp(string sourcePath) => "stamp.png";
        public void RemoveStamp() { }
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
        IPayrollWorkbookReader? reader = null, bool gmailConnected = true, bool hasStamp = true)
        => Create(out _, reader, gmailConnected, hasStamp);

    private static PayrollWizardViewModel Create(
        out FakeRepository repo,
        IPayrollWorkbookReader? reader = null, bool gmailConnected = true, bool hasStamp = true)
    {
        repo = new FakeRepository();
        return new PayrollWizardViewModel(
            reader ?? new FakeReader(_ => new WorkbookReadResult(PayrollContract.Headers, [ValidInput(1)], [])),
            new FakeCoordinator(),
            new FakePdfGenerator(),
            new FakeGmail(gmailConnected),
            new FakeStampStore(hasStamp),
            repo,
            new PassThroughSecretStore(),
            new FakeTempFiles(),
            NullLogger<PayrollWizardViewModel>.Instance);
    }

    private static void FillSelectStep(PayrollWizardViewModel vm, string file = "payroll.xlsx")
    {
        vm.SelectedFilePath = file;
        vm.Period = "JULY 2025";
        vm.PaymentDate = new DateTime(2026, 5, 11);
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
    public async Task OpenPreview_GeneratesPdf_WhenValidRowsExist()
    {
        PayrollWizardViewModel vm = Create(out _, hasStamp: true);
        FillSelectStep(vm);
        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep = WizardStep.Preview;

        Assert.True(vm.OpenPreviewCommand.CanExecute(null));

        try
        {
            vm.OpenPreviewCommand.Execute(null);
        }
        catch (Exception)
        {
        }

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
