using EunSlip.Core.Persistence;

namespace EunSlip.Desktop.ViewModels;

public enum PayrollRunMode { Normal, FailedRetry, RecoveryRetry }

public sealed record PayrollWizardEntry(PayrollRunMode Mode, Guid? BatchId)
{
    public static PayrollWizardEntry Normal() => new(PayrollRunMode.Normal, null);

    public static PayrollWizardEntry FailedRetry(Guid batchId) =>
        new(PayrollRunMode.FailedRetry, batchId);

    public static PayrollWizardEntry RecoveryRetry(Guid batchId) =>
        new(PayrollRunMode.RecoveryRetry, batchId);

    public AttemptType AttemptType => Mode switch
    {
        PayrollRunMode.Normal => AttemptType.Normal,
        PayrollRunMode.FailedRetry => AttemptType.FailedRetry,
        PayrollRunMode.RecoveryRetry => AttemptType.RecoveryRetry,
        _ => throw new InvalidOperationException($"Unsupported payroll run mode: {Mode}"),
    };

    public bool IsResume => Mode != PayrollRunMode.Normal;
}
