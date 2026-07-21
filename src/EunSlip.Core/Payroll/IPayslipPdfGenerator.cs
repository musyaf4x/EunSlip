namespace EunSlip.Core.Payroll;

public interface IPayslipPdfGenerator
{
    void Generate(PayslipRequest request, string outputPath);
}

public sealed record PayslipRequest(
    BatchContext Context,
    PayrollRow Row,
    string? StampImagePath);

public sealed class PayslipGenerationException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
