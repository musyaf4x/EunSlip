using EunSlip.Core.Common;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Security;
using EunSlip.Core.Sending;
using Microsoft.Extensions.Logging;

namespace EunSlip.Core.Batches;

public sealed class BatchCoordinator(
    IPayslipPdfGenerator pdfGenerator,
    IGmailRetrySender sender,
    IAppRepository repository,
    ISecretStore secretStore,
    ISharedFileStore stampStore,
    ITempFileService tempFiles,
    ILogger<BatchCoordinator> logger) : IBatchCoordinator
{
    private readonly IPayslipPdfGenerator _pdfGenerator = pdfGenerator;
    private readonly IGmailRetrySender _sender = sender;
    private readonly IAppRepository _repository = repository;
    private readonly ISecretStore _secretStore = secretStore;
    private readonly ISharedFileStore _stampStore = stampStore;
    private readonly ITempFileService _tempFiles = tempFiles;
    private readonly ILogger<BatchCoordinator> _logger = logger;

    public async Task<BatchRunResult> RunBatchAsync(BatchRunRequest request, CancellationToken cancellationToken)
    {
        if (request.Rows.Count == 0)
        {
            return new BatchRunResult(request.BatchId, []);
        }

        string? stampPath = _stampStore.GetActiveStampPath();
        if (string.IsNullOrEmpty(stampPath))
        {
            throw new BatchCoordinatorException("Company stamp is missing.");
        }

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        _repository.UpdateBatchStatus(request.BatchId, BatchStatus.Sending, startedAt, null);

        List<RecipientResult> results = [];
        int succeeded = 0;
        int failed = 0;
        IReadOnlyList<BatchRecipientRecord> recipients = _repository.ListRecipients(request.BatchId);
        Dictionary<string, BatchRecipientRecord> recipientByNik = recipients.ToDictionary(
            r => _secretStore.Unprotect(r.EncryptedNik), StringComparer.Ordinal);

        int current = 0;
        foreach (PayrollRow row in request.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current++;

            if (!recipientByNik.TryGetValue(row.Nik, out BatchRecipientRecord? recipientRecord))
            {
                _logger.LogWarning("Skipping recipient not in batch {BatchId} {NikHint}", request.BatchId, NikHint.LastFour(row.Nik));
                continue;
            }

            RecipientResult result = await SendOneAsync(
                request, row, recipientRecord, stampPath, current, request.Rows.Count,
                succeeded, failed, cancellationToken);

            if (result.Succeeded)
            {
                succeeded++;
            }
            else
            {
                failed++;
            }

            results.Add(result);
        }

        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        _repository.UpdateBatchStatus(request.BatchId, BatchStatus.Completed, null, completedAt);

        return new BatchRunResult(request.BatchId, results);
    }

    private async Task<RecipientResult> SendOneAsync(
        BatchRunRequest request, PayrollRow row, BatchRecipientRecord recipientRecord,
        string stampPath, int current, int total,
        int succeeded, int failed, CancellationToken cancellationToken)
    {
        string email = _secretStore.Unprotect(recipientRecord.EncryptedEmail);
        _repository.UpdateRecipientStatus(recipientRecord.Id, RecipientStatus.Sending, DateTimeOffset.UtcNow);

        string fileName = PayrollFormatting.BuildPayslipFileName(request.Context.Period, row.Nik);
        string tempDir = _tempFiles.CreateBatchTempDirectory(request.BatchId);
        string pdfPath = Path.Combine(tempDir, fileName);

        try
        {
            _pdfGenerator.Generate(
                new PayslipRequest(request.Context, row, stampPath), pdfPath);

            SendRequest sendRequest = new(
                email, request.Subject, request.Body, pdfPath, fileName, request.SenderDisplayName);

            RetrySendOutcome outcome = await _sender.SendWithRetryAsync(sendRequest, cancellationToken);

            DateTimeOffset recipientCompletedAt = DateTimeOffset.UtcNow;

            foreach (AttemptDetail detail in outcome.Attempts)
            {
                Guid attemptId = Guid.NewGuid();
                _repository.AddAttempt(new SendAttemptRecord(
                    attemptId, recipientRecord.Id, detail.AttemptNumber, request.AttemptKind,
                    detail.StartedAtUtc, detail.CompletedAtUtc,
                    detail.Result == SendResult.Sent ? AttemptStatus.Sent : AttemptStatus.Failed,
                    detail.ErrorCategory, detail.ErrorMessage, detail.GmailMessageId));
            }

            if (outcome.Result == SendResult.Sent)
            {
                _repository.UpdateRecipientStatus(recipientRecord.Id, RecipientStatus.Sent, recipientCompletedAt);
            }
            else
            {
                _repository.UpdateRecipientStatus(recipientRecord.Id, RecipientStatus.Failed, recipientCompletedAt);
            }

            request.Progress.Report(new BatchProgress(
                current, total, row.Nik, row.Nama,
                succeeded + (outcome.Result == SendResult.Sent ? 1 : 0),
                failed + (outcome.Result == SendResult.Failed ? 1 : 0),
                outcome.AttemptsMade));

            return new RecipientResult(
                row.Nik, row.Nama, email, outcome.Result == SendResult.Sent,
                outcome.AttemptsMade, outcome.ErrorCategory, outcome.ErrorMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process recipient {NikHint}", NikHint.LastFour(row.Nik));
            DateTimeOffset failedAt = DateTimeOffset.UtcNow;
            Guid attemptId = Guid.NewGuid();
            _repository.AddAttempt(new SendAttemptRecord(
                attemptId, recipientRecord.Id, 0, request.AttemptKind,
                DateTimeOffset.UtcNow, failedAt, AttemptStatus.Failed,
                "UnexpectedError", "Processing failed.", null));
            _repository.UpdateRecipientStatus(recipientRecord.Id, RecipientStatus.Failed, failedAt);
            return new RecipientResult(row.Nik, row.Nama, email, false, 0, "UnexpectedError", "Processing failed.");
        }
        finally
        {
            _tempFiles.DeleteFile(pdfPath);
        }
    }
}

public interface ITempFileService
{
    string CreateBatchTempDirectory(Guid batchId);
    void DeleteFile(string path);
    void CleanupLeftovers();
}
