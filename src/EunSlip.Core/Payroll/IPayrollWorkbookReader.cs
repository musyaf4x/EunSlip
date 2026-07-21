using EunSlip.Core.Validation;

namespace EunSlip.Core.Payroll;

public interface IPayrollWorkbookReader
{
    WorkbookReadResult Read(string filePath);
}

public sealed record WorkbookReadResult(
    IReadOnlyList<string> Headers,
    IReadOnlyList<PayrollRowInput> Rows,
    IReadOnlyList<PayrollIssue> ReadIssues);

public sealed class WorkbookUnreadableException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
