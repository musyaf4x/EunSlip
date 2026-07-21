using EunSlip.Core.Payroll;

namespace EunSlip.Core.Validation;

public sealed record ValidationResult(
    IReadOnlyList<PayrollIssue> Issues,
    IReadOnlyList<PayrollRow> ValidRows)
{
    public bool CanProceed => Issues.All(i => i.Severity != IssueSeverity.Blocking);
}
