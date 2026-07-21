using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Batches;
using EunSlip.Core.Common;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
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
    private readonly ILogger<PayrollWizardViewModel> _logger;

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
    private string _emailSubject = "Slip Gaji Karyawan";

    [ObservableProperty]
    private string _emailBody = "Yth. Bapak/Ibu,\n\nTerlampir kami sampaikan slip gaji Anda.\n\nMohon menjaga kerahasiaan dokumen ini dan tidak meneruskannya kepada pihak lain.\n\nTerima kasih.\n\nPT. EUNSUNG INDONESIA";

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
    public bool IsReadyToConfirm => CurrentStep == WizardStep.Confirm && !IsBusy && HasGmailConnection && HasStamp;
    public bool GmailReady => HasGmailConnection;
    public bool StampReady => HasStamp;

    public void Reset()
    {
        CurrentStep = WizardStep.Select;
        SelectedFilePath = null;
        Period = string.Empty;
        PaymentDate = null;
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = false;
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
    }

    [RelayCommand]
    private async Task LoadedAsync()
    {
        GoogleAccount? account = await _gmail.RestoreAsync(CancellationToken.None);
        ConnectedGmail = account?.Email;
        HasGmailConnection = account is not null;
        HasStamp = _stampStore.GetActiveStampPath() is not null;
        LoadEmailTemplate();
    }

    [RelayCommand(CanExecute = nameof(CanNext))]
    private async Task NextAsync()
    {
        if (CurrentStep == WizardStep.Select)
        {
            await RunValidationAsync();
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

    [RelayCommand(CanExecute = nameof(CanOpenPreview))]
    private void OpenPreview()
    {
        if (_validRows.Count == 0)
        {
            return;
        }

        string? stampPath = _stampStore.GetActiveStampPath();
        if (string.IsNullOrEmpty(stampPath))
        {
            ErrorMessage = Strings.Get("ValidationBlockingMessage");
            return;
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

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfPath)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Preview generation failed");
            ErrorMessage = Strings.Get("UnexpectedErrorMessage");
        }
    }

    private bool CanOpenPreview() =>
        CurrentStep == WizardStep.Preview && _validRows.Count > 0;

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
            BatchRunRequest request = new(
                new BatchContext(Period, DateOnly.FromDateTime(PaymentDate!.Value)),
                _validRows,
                EmailSubject,
                EmailBody,
                "PT. EUNSUNG INDONESIA",
                _batchId,
                AttemptType.Normal,
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
        CurrentStep == WizardStep.Confirm && !IsBusy && HasGmailConnection && HasStamp;

    private void OnProgress(BatchProgress progress)
    {
        CurrentRecipient = progress.Current;
        TotalRecipients = progress.Total;
        SentCount = progress.Succeeded;
        FailedCount = progress.Failed;
        CurrentAttempt = progress.CurrentAttempt;
        CurrentNik = progress.Nik;
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
            IReadOnlyList<string> previouslySentNiks = _repository.FindPreviouslySentNiks(Period);
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

            _validRows = validation.ValidRows;
            OnPropertyChanged(nameof(RecipientCount));

            bool blockingReadIssue = read.ReadIssues.Any(i => i.Severity == IssueSeverity.Blocking);
            if (!validation.CanProceed || blockingReadIssue)
            {
                ErrorMessage = Strings.Get("ValidationBlockingMessage");
                return;
            }

            HasWarnings = validation.Issues.Any(i => i.Severity == IssueSeverity.Warning);
            _batchId = CreateBatchRecord();
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
        NextCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        ConfirmSendCommand.NotifyCanExecuteChanged();
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnSelectedFilePathChanged(string? value) => NextCommand.NotifyCanExecuteChanged();
    partial void OnPeriodChanged(string value) => NextCommand.NotifyCanExecuteChanged();
    partial void OnPaymentDateChanged(DateTime? value) => NextCommand.NotifyCanExecuteChanged();
    partial void OnHasGmailConnectionChanged(bool value) => NextCommand.NotifyCanExecuteChanged();
    partial void OnHasStampChanged(bool value) => NextCommand.NotifyCanExecuteChanged();
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
