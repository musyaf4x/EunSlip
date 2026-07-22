using EunSlip.Core.Persistence;
using EunSlip.Desktop.Localization;

namespace EunSlip.Desktop.ViewModels;

public sealed class HistoryRecipientViewModel
{
    public HistoryRecipientViewModel(BatchRecipientRecord recipient, SendAttemptRecord? latestAttempt)
    {
        NikHint = recipient.NikHint ?? "-";
        Status = recipient.Status;
        LastUpdatedAtUtc = recipient.LastUpdatedAtUtc;
        LatestAttemptType = latestAttempt?.AttemptType;
        LatestAttemptNumber = latestAttempt?.AttemptNumber;
        LatestAttemptCompletedAtUtc = latestAttempt?.CompletedAtUtc;
        ErrorCategory = latestAttempt?.ErrorCategory ?? "-";
        ErrorSummary = latestAttempt?.Status == AttemptStatus.Failed
            ? Strings.Get("HistoryDeliveryFailedSummary")
            : "-";
    }

    public string NikHint { get; }
    public RecipientStatus Status { get; }
    public DateTimeOffset LastUpdatedAtUtc { get; }
    public AttemptType? LatestAttemptType { get; }
    public int? LatestAttemptNumber { get; }
    public DateTimeOffset? LatestAttemptCompletedAtUtc { get; }
    public string ErrorCategory { get; }
    public string ErrorSummary { get; }
}
