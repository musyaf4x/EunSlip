using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Batches;
using EunSlip.Core.Common;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Recovery;
using EunSlip.Core.Security;
using EunSlip.Core.Sending;
using EunSlip.Core.Validation;
using EunSlip.Desktop.Localization;
using Microsoft.Extensions.Logging;

namespace EunSlip.Desktop.ViewModels;

public enum WizardStep { Select, Validate, Preview, Confirm, Send, Results }

public sealed partial class PayrollWizardViewModel : ViewModelBase
{
    private static readonly WizardStep[] StepOrder =
        [WizardStep.Select, WizardStep.Validate, WizardStep.Preview, WizardStep.Confirm, WizardStep.Send, WizardStep.Results];

    private readonly IPayrollWorkbookReader _reader;
    private readonly IBatchCoordinator _coordinator;
    private readonly IPayslipPdfGenerator _pdfGenerator;
    private readonly IGmailAuthorization _gmail;
    private readonly ISharedFileStore _stampStore;
    private readonly IAppRepository _repository;
    private readonly ISecretStore _secretStore;
    private readonly ITempFileService _tempFiles;
    private readonly IRecoveryService _recovery;
    private readonly ILogger<PayrollWizardViewModel> _logger;
    private PayrollWizardEntry _entry = PayrollWizardEntry.Normal();

    [ObservableProperty]
    private WizardStep _currentStep = WizardStep.Select;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private string? _selectedFilePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private string _period = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private DateTime? _paymentDate;

    [ObservableProperty]
    private string _emailSubject = Strings.Get("DefaultEmailSubject");

    [ObservableProperty]
    private string _emailBody = Strings.Get("DefaultEmailBody");

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasWarnings;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private bool _warningConfirmed;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isPrerequisiteLoading;

    [ObservableProperty]
    private string? _connectedGmail;

    [ObservableProperty]
    private bool _hasGmailConnection;

    [ObservableProperty]
    private bool _hasStamp;

    [ObservableProperty]
    private int _currentRecipient;

    [ObservableProperty]
    private int _totalRecipients;

    [ObservableProperty]
    private int _sentCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private int _currentAttempt;

    [ObservableProperty]
    private string? _currentNik;

    [ObservableProperty]
    private string? _currentName;

    public ObservableCollection<ValidationRowViewModel> ValidationRows { get; } = [];
    public ObservableCollection<RecipientResult> Results { get; } = [];

    private ValidationResult? _validation;
    private IReadOnlyList<PayrollRow> _validRows = [];
    private Guid _batchId;

    public PayrollWizardViewModel(
        IPayrollWorkbookReader reader,
        IBatchCoordinator coordinator,
        IPayslipPdfGenerator pdfGenerator,
        IGmailAuthorization gmail,
        ISharedFileStore stampStore,
        IAppRepository repository,
        ISecretStore secretStore,
        ITempFileService tempFiles,
        IRecoveryService recovery,
        ILogger<PayrollWizardViewModel> logger)
    {
        _reader = reader;
        _coordinator = coordinator;
        _pdfGenerator = pdfGenerator;
        _gmail = gmail;
        _stampStore = stampStore;
        _repository = repository;
        _secretStore = secretStore;
        _tempFiles = tempFiles;
        _recovery = recovery;
        _logger = logger;
    }

    public int StepIndex => Array.IndexOf(StepOrder, CurrentStep);
    public static int StepCount => StepOrder.Length;
    public string StepTitle => Strings.Get($"WizardStep_{CurrentStep}");
    public bool IsOnSendStep => CurrentStep == WizardStep.Send;
    public bool IsOnResultsStep => CurrentStep == WizardStep.Results;
    public int RecipientCount => _validRows.Count;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool CanGoBack => StepIndex > 0 && !IsOnSendStep;
    public bool CanGoNext => !IsOnSendStep && !IsOnResultsStep;
    public bool IsReadyToConfirm =>
        CurrentStep == WizardStep.Confirm && !IsBusy && !IsPrerequisiteLoading && HasGmailConnection && HasStamp;
    public bool GmailReady => HasGmailConnection;
    public bool StampReady => HasStamp;
    public PayrollRunMode RunMode => _entry.Mode;
    public bool IsResumeMode => _entry.IsResume;
    public string GmailStatusText => IsPrerequisiteLoading
        ? Strings.Get("StatusChecking")
        : HasGmailConnection ? Strings.Get("StatusReady") : Strings.Get("StatusNotReady");
    public string StampStatusText => IsPrerequisiteLoading
        ? Strings.Get("StatusChecking")
        : HasStamp ? Strings.Get("StatusReady") : Strings.Get("StatusNotReady");
    public bool IsSending => CurrentStep == WizardStep.Send && IsBusy;
    public bool CanGeneratePreview => CurrentStep == WizardStep.Preview && _validRows.Count > 0;

    public bool Begin(PayrollWizardEntry entry)
    {
        Reset();
        _entry = entry;
        OnPropertyChanged(nameof(RunMode));
        OnPropertyChanged(nameof(IsResumeMode));
        if (entry.BatchId is not Guid batchId)
        {
            return true;
        }

        PayrollBatchRecord? batch = _repository.GetBatch(batchId);
        if (batch is null)
        {
            ErrorMessage = Strings.Get("HistoryBatchMissing");
            return false;
        }

        _batchId = batch.Id;
        Period = batch.Period;
        PaymentDate = batch.PaymentDate.ToDateTime(TimeOnly.MinValue);
        return true;
    }

    public void Reset()
    {
        _entry = PayrollWizardEntry.Normal();
        CurrentStep = WizardStep.Select;
        SelectedFilePath = null;
        Period = string.Empty;
        PaymentDate = null;
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = false;
        IsPrerequisiteLoading = false;
        ConnectedGmail = null;
        HasGmailConnection = false;
        HasStamp = false;
        ValidationRows.Clear();
        Results.Clear();
        _validation = null;
        _validRows = [];
        HasWarnings = false;
        WarningConfirmed = false;
        CurrentRecipient = 0;
        TotalRecipients = 0;
        SentCount = 0;
        FailedCount = 0;
        CurrentAttempt = 0;
        OnPropertyChanged(nameof(RecipientCount));
        OnPropertyChanged(nameof(CanGeneratePreview));
        OnPropertyChanged(nameof(RunMode));
        OnPropertyChanged(nameof(IsResumeMode));
    }

    [RelayCommand]
    private async Task LoadedAsync()
    {
        await RefreshPrerequisitesAsync(CancellationToken.None);
        LoadEmailTemplate();
    }

    public async Task RefreshPrerequisitesAsync(CancellationToken cancellationToken)
    {
        IsPrerequisiteLoading = true;
        try
        {
            GoogleAccount? account = await _gmail.RestoreAsync(cancellationToken);
            ConnectedGmail = account?.Email;
            HasGmailConnection = account is not null;
            HasStamp = _stampStore.GetActiveStampPath() is not null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prerequisite refresh failed");
            HasGmailConnection = false;
            HasStamp = false;
            ErrorMessage = Strings.Get("PrerequisiteRefreshFailed");
        }
        finally
        {
            IsPrerequisiteLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanNext))]
    private async Task NextAsync()
    {
        if (CurrentStep == WizardStep.Select)
        {
            await RunValidationAsync();
            return;
        }

        if (CurrentStep == WizardStep.Preview)
        {
            await RefreshPrerequisitesAsync(CancellationToken.None);
            CurrentStep = WizardStep.Confirm;
            return;
        }

        int index = StepIndex;
        if (index < StepOrder.Length - 1)
        {
            CurrentStep = StepOrder[index + 1];
        }
    }

    private bool CanNext()
    {
        if (IsBusy || IsOnSendStep || IsOnResultsStep)
        {
            return false;
        }

        return CurrentStep switch
        {
            WizardStep.Select =>
                !string.IsNullOrWhiteSpace(SelectedFilePath) &&
                !string.IsNullOrWhiteSpace(Period) &&
                PaymentDate is not null,
            WizardStep.Validate => (_validation?.CanProceed ?? false)
                && (!HasWarnings || WarningConfirmed),
            WizardStep.Preview => _validRows.Count > 0,
            WizardStep.Confirm => false,
            _ => false,
        };
    }

    [RelayCommand(CanExecute = nameof(CanBack))]
    private void Back()
    {
        int index = StepIndex;
        if (index > 0 && !IsOnSendStep)
        {
            CurrentStep = StepOrder[index - 1];
        }
    }

    private bool CanBack() => StepIndex > 0 && !IsOnSendStep;

    public string? GeneratePreviewPdf()
    {
        if (_validRows.Count == 0)
        {
            return null;
        }

        string? stampPath = _stampStore.GetActiveStampPath();
        if (string.IsNullOrEmpty(stampPath))
        {
            ErrorMessage = Strings.Get("ValidationBlockingMessage");
            return null;
        }

        PayrollRow first = _validRows[0];
        string tempDir = _tempFiles.CreateBatchTempDirectory(_batchId);
        string fileName = PayrollFormatting.BuildPayslipFileName(Period, first.Nik);
        string pdfPath = Path.Combine(tempDir, fileName);

        try
        {
            _pdfGenerator.Generate(
                new PayslipRequest(new BatchContext(Period, DateOnly.FromDateTime(PaymentDate!.Value)), first, stampPath),
                pdfPath);
            ErrorMessage = null;
            return pdfPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Preview generation failed");
            ErrorMessage = Strings.Get("PreviewGenerationFailed");
            return null;
        }
    }

    public void ReportPreviewOpenFailure()
    {
        ErrorMessage = Strings.Get("PreviewOpenFailed");
    }

    private void PersistEmailTemplate()
    {
        _repository.SetSetting("LastEmailSubject", EmailSubject);
        _repository.SetSetting("LastEmailBody", EmailBody);
    }

    private void LoadEmailTemplate()
    {
        string? subject = _repository.GetSetting("LastEmailSubject");
        string? body = _repository.GetSetting("LastEmailBody");
        if (!string.IsNullOrEmpty(subject))
        {
            EmailSubject = subject;
        }
        if (!string.IsNullOrEmpty(body))
        {
            EmailBody = body;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmSend))]
    private async Task ConfirmSendAsync()
    {
        IsBusy = true;
        StatusMessage = Strings.Get("SendingInProgress");
        CurrentStep = WizardStep.Send;
        try
        {
            if (_entry.Mode == PayrollRunMode.RecoveryRetry)
            {
                _recovery.PrepareForRecovery(_batchId);
            }

            BatchRunRequest request = new(
                new BatchContext(Period, DateOnly.FromDateTime(PaymentDate!.Value)),
                _validRows,
                EmailSubject,
                EmailBody,
                "PT. EUNSUNG INDONESIA",
                _batchId,
                _entry.AttemptType,
                new Progress<BatchProgress>(OnProgress));

            TotalRecipients = _validRows.Count;
            BatchRunResult result = await _coordinator.RunBatchAsync(request, CancellationToken.None);

            Results.Clear();
            foreach (RecipientResult r in result.Results)
            {
                Results.Add(r);
            }
            SentCount = result.SentCount;
            FailedCount = result.FailedCount;
            PersistEmailTemplate();
            CurrentStep = WizardStep.Results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch send failed");
            ErrorMessage = Strings.Get("SendFailedMessage");
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }

    private bool CanConfirmSend() =>
        CurrentStep == WizardStep.Confirm && !IsBusy && !IsPrerequisiteLoading && HasGmailConnection && HasStamp;

    private void OnProgress(BatchProgress progress)
    {
        CurrentRecipient = progress.Current;
        TotalRecipients = progress.Total;
        SentCount = progress.Succeeded;
        FailedCount = progress.Failed;
        CurrentAttempt = progress.CurrentAttempt;
        CurrentNik = NikHint.LastFour(progress.Nik);
        CurrentName = progress.Name;
    }

    private async Task RunValidationAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        ValidationRows.Clear();
        try
        {
            WorkbookReadResult read = _reader.Read(SelectedFilePath!);
            IReadOnlyList<string> previouslySentNiks = _entry.IsResume
                ? []
                : _repository.FindPreviouslySentNiks(Period);
            ValidationResult validation = PayrollValidator.Validate(
                read.Headers, read.Rows,
                previouslySentNiks.Count == 0 ? null : new HashSet<string>(previouslySentNiks, StringComparer.OrdinalIgnoreCase));
            _validation = validation;

            IReadOnlyList<PayrollIssue> allIssues =
                [.. read.ReadIssues, .. validation.Issues];

            foreach (PayrollRowInput row in read.Rows)
            {
                ValidationRows.Add(new ValidationRowViewModel(
                    row.RowNumber, row.Nik ?? string.Empty, row.Nama ?? string.Empty,
                    row.Email ?? string.Empty, allIssues.FirstOrDefault(i => i.RowNumber == row.RowNumber)));
            }

            IReadOnlyList<PayrollRow> validatedRows = validation.ValidRows;
            if (_entry.IsResume)
            {
                BatchContext context = new(Period, DateOnly.FromDateTime(PaymentDate!.Value));
                RecoveryGate gate = _recovery.VerifyFingerprint(_batchId, context, validatedRows);
                if (!gate.CanProceed)
                {
                    ErrorMessage = Strings.Get("RecoveryFingerprintMismatch");
                    return;
                }

                IReadOnlyList<string> eligibleNiks = _entry.Mode == PayrollRunMode.FailedRetry
                    ? _recovery.SelectRetryFailedNiks(_batchId)
                    : _recovery.SelectRecoveryResendNiks(_batchId);
                HashSet<string> eligible = new(eligibleNiks, StringComparer.OrdinalIgnoreCase);
                _validRows = [.. validatedRows.Where(row => eligible.Contains(row.Nik))];
                if (_validRows.Count == 0)
                {
                    ErrorMessage = Strings.Get("RecoveryNoEligibleRecipients");
                    return;
                }
            }
            else
            {
                _validRows = validatedRows;
            }
            OnPropertyChanged(nameof(RecipientCount));
            OnPropertyChanged(nameof(CanGeneratePreview));

            bool blockingReadIssue = read.ReadIssues.Any(i => i.Severity == IssueSeverity.Blocking);
            if (!validation.CanProceed || blockingReadIssue)
            {
                ErrorMessage = Strings.Get("ValidationBlockingMessage");
                return;
            }

            HasWarnings = validation.Issues.Any(i => i.Severity == IssueSeverity.Warning);
            if (!_entry.IsResume)
            {
                _batchId = CreateBatchRecord();
            }
            CurrentStep = WizardStep.Validate;
        }
        catch (WorkbookUnreadableException ex)
        {
            ErrorMessage = Strings.Get("WorkbookUnreadableMessage");
            _logger.LogWarning("Workbook unreadable: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.Get("UnexpectedErrorMessage");
            _logger.LogError(ex, "Validation failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Guid CreateBatchRecord()
    {
        BatchContext context = new(Period, DateOnly.FromDateTime(PaymentDate!.Value));
        string fingerprint = PayrollFingerprint.Compute(context, _validRows);
        Guid batchId = Guid.NewGuid();
        _repository.CreateBatch(new PayrollBatchRecord(
            batchId, Period, DateOnly.FromDateTime(PaymentDate!.Value), fingerprint,
            BatchStatus.Ready, DateTimeOffset.UtcNow, null, null, WarningConfirmed, _validRows.Count, 0, 0));

        foreach (PayrollRow row in _validRows)
        {
            _repository.AddRecipient(new BatchRecipientRecord(
                Guid.NewGuid(), batchId,
                _secretStore.Protect(row.Nik),
                _secretStore.Protect(row.Email),
                NikHint.LastFour(row.Nik),
                RecipientStatus.Pending,
                DateTimeOffset.UtcNow));
        }

        return batchId;
    }

    partial void OnCurrentStepChanged(WizardStep value)
    {
        OnPropertyChanged(nameof(StepIndex));
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(IsOnSendStep));
        OnPropertyChanged(nameof(IsOnResultsStep));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(IsReadyToConfirm));
        OnPropertyChanged(nameof(IsSending));
        OnPropertyChanged(nameof(CanGeneratePreview));
        NextCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        ConfirmSendCommand.NotifyCanExecuteChanged();
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnSelectedFilePathChanged(string? value) => NextCommand.NotifyCanExecuteChanged();
    partial void OnPeriodChanged(string value) => NextCommand.NotifyCanExecuteChanged();
    partial void OnPaymentDateChanged(DateTime? value) => NextCommand.NotifyCanExecuteChanged();
    partial void OnHasGmailConnectionChanged(bool value)
    {
        OnPropertyChanged(nameof(GmailReady));
        OnPropertyChanged(nameof(GmailStatusText));
        OnPropertyChanged(nameof(IsReadyToConfirm));
        ConfirmSendCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasStampChanged(bool value)
    {
        OnPropertyChanged(nameof(StampReady));
        OnPropertyChanged(nameof(StampStatusText));
        OnPropertyChanged(nameof(IsReadyToConfirm));
        ConfirmSendCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPrerequisiteLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(GmailStatusText));
        OnPropertyChanged(nameof(StampStatusText));
        OnPropertyChanged(nameof(IsReadyToConfirm));
        ConfirmSendCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSending));
        OnPropertyChanged(nameof(IsReadyToConfirm));
        NextCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        ConfirmSendCommand.NotifyCanExecuteChanged();
    }
}

public sealed partial class ValidationRowViewModel : ObservableObject
{
    public int RowNumber { get; }
    public string Nik { get; }
    public string Name { get; }
    public string Email { get; }

    [ObservableProperty]
    private string? _issueSummary;

    public ValidationRowViewModel(int rowNumber, string nik, string name, string email, PayrollIssue? issue)
    {
        RowNumber = rowNumber;
        Nik = nik;
        Name = name;
        Email = email;
        IssueSummary = issue is null ? null : $"{issue.Severity}: {issue.Code}";
    }
}
