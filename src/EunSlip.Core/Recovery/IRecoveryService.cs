using EunSlip.Core.Payroll;

namespace EunSlip.Core.Recovery;

public interface IRecoveryService
{
    IReadOnlyList<Guid> DetectInterruptedBatches();
    void PrepareForRecovery(Guid batchId);
    RecoveryGate VerifyFingerprint(Guid batchId, BatchContext context, IReadOnlyList<PayrollRow> rows);
    IReadOnlyList<string> SelectRetryFailedNiks(Guid batchId);
    IReadOnlyList<string> SelectRecoveryResendNiks(Guid batchId);
}

public enum RecoveryGateResult { Match, Mismatch }

public sealed record RecoveryGate(RecoveryGateResult Result, string? StoredFingerprint, string? ComputedFingerprint)
{
    public bool CanProceed => Result == RecoveryGateResult.Match;
}
