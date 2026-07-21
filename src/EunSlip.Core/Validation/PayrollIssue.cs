namespace EunSlip.Core.Validation;

public enum IssueSeverity
{
    Blocking,
    Warning,
}

public sealed record PayrollIssue(
    IssueSeverity Severity,
    string Code,
    int? RowNumber,
    string? Nik,
    string? Field,
    string? StoredValue,
    string? ExpectedValue);
