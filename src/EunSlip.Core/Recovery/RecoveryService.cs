using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;
using EunSlip.Core.Security;

namespace EunSlip.Core.Recovery;

public sealed class RecoveryService(IAppRepository repository, ISecretStore secretStore) : IRecoveryService
{
    private readonly IAppRepository _repository = repository;
    private readonly ISecretStore _secretStore = secretStore;

    public IReadOnlyList<Guid> DetectInterruptedBatches()
    {
        return _repository.FindInterruptedBatches();
    }

    public void PrepareForRecovery(Guid batchId)
    {
        ReconcileCommittedSends(batchId);
        _repository.UpdateBatchStatus(batchId, BatchStatus.Interrupted, null, null);
        _repository.ResetSendingRecipientsToPending(batchId);
    }

    private void ReconcileCommittedSends(Guid batchId)
    {
        foreach (BatchRecipientRecord recipient in _repository.ListRecipients(batchId)
            .Where(r => r.Status == RecipientStatus.Sending))
        {
            if (_repository.GetLatestAttemptStatus(recipient.Id) == AttemptStatus.Sent)
            {
                _repository.UpdateRecipientStatus(recipient.Id, RecipientStatus.Sent, DateTimeOffset.UtcNow);
            }
        }
    }

    public RecoveryGate VerifyFingerprint(Guid batchId, BatchContext context, IReadOnlyList<PayrollRow> rows)
    {
        PayrollBatchRecord? batch = _repository.GetBatch(batchId);
        if (batch is null)
        {
            return new RecoveryGate(RecoveryGateResult.Mismatch, null, null);
        }

        string computed = PayrollFingerprint.Compute(context, rows);
        return batch.Fingerprint == computed
            ? new RecoveryGate(RecoveryGateResult.Match, batch.Fingerprint, computed)
            : new RecoveryGate(RecoveryGateResult.Mismatch, batch.Fingerprint, computed);
    }

    public IReadOnlyList<string> SelectRetryFailedNiks(Guid batchId)
    {
        return [.. _repository.ListRecipients(batchId)
            .Where(r => r.Status == RecipientStatus.Failed)
            .Select(r => _secretStore.Unprotect(r.EncryptedNik))];
    }

    public IReadOnlyList<string> SelectRecoveryResendNiks(Guid batchId)
    {
        return [.. _repository.ListRecipients(batchId)
            .Where(r => r.Status != RecipientStatus.Sent)
            .Select(r => _secretStore.Unprotect(r.EncryptedNik))];
    }
}
