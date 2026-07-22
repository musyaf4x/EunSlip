using System.Diagnostics;
using EunSlip.Core.Payroll;
using EunSlip.Core.Validation;
using EunSlip.Infrastructure.Excel;
using EunSlip.Infrastructure.Pdf;
using EunSlip.Infrastructure.Tests.Excel;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit.Abstractions;

namespace EunSlip.Infrastructure.Tests;

public sealed class ReleaseCandidateBaselineTests(ITestOutputHelper output) : IDisposable
{
    private const int EmployeeCount = PayrollContract.MaxEmployeeRows;
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "eunslip-task-016", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FiveHundredRows_ImportValidateAndGenerateOnePagePdfs_WithBoundedTempFiles()
    {
        Directory.CreateDirectory(_directory);
        string stampPath = Path.Combine(_directory, "dummy-stamp.png");
        File.WriteAllBytes(stampPath, Convert.FromBase64String(TinyPngBase64));
        TestCell[][] workbookRows =
        [
            TestWorkbook.HeaderRow([.. PayrollContract.Headers]),
            .. Enumerable.Range(1, EmployeeCount).Select(ValidEmployeeRow),
        ];
        string workbookPath = TestWorkbook.Create(workbookRows);
        Stopwatch stopwatch = Stopwatch.StartNew();

        ValidationResult validation;
        try
        {
            validation = await Task.Run(() =>
            {
                WorkbookReadResult read = new OpenXmlWorkbookReader().Read(workbookPath);
                Assert.Empty(read.ReadIssues);
                Assert.Equal(EmployeeCount, read.Rows.Count);
                return PayrollValidator.Validate(read.Headers, read.Rows);
            });
        }
        finally
        {
            File.Delete(workbookPath);
        }

        Assert.Empty(validation.Issues);
        Assert.Equal(EmployeeCount, validation.ValidRows.Count);

        PayslipPdfGenerator generator = new();
        BatchContext context = new("JULY 2026", new DateOnly(2026, 7, 22));
        int peakPdfCount = 0;
        foreach (PayrollRow row in validation.ValidRows)
        {
            string pdfPath = Path.Combine(_directory, $"slip-{row.Nik}.pdf");
            generator.Generate(new PayslipRequest(context, row, stampPath), pdfPath);
            peakPdfCount = Math.Max(peakPdfCount, Directory.EnumerateFiles(_directory, "*.pdf").Count());

            using (PdfDocument document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(1, document.PageCount);
            }

            File.Delete(pdfPath);
        }

        stopwatch.Stop();
        Assert.Equal(1, peakPdfCount);
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.pdf"));
        output.WriteLine(
            "TASK-016 baseline: {0} rows, {1} one-page PDFs, elapsed {2:N2}s, peak temp PDFs {3}.",
            EmployeeCount, EmployeeCount, stopwatch.Elapsed.TotalSeconds, peakPdfCount);
    }

    private static TestCell[] ValidEmployeeRow(int index)
    {
        int excelRow = index + 1;
        return
        [
            new(TestCellKind.SharedString, $"UAT{index:0000}"),
            new(TestCellKind.SharedString, $"Dummy Employee {index:0000}"),
            new(TestCellKind.SharedString, "Finance"),
            new(TestCellKind.SharedString, "Staff"),
            new(TestCellKind.DateSerial, "43845"),
            new(TestCellKind.SharedString, "Monthly"),
            new(TestCellKind.SharedString, $"employee{index:0000}@example.test"),
            new(TestCellKind.Number, "5000000"),
            new(TestCellKind.Blank),
            new(TestCellKind.Blank),
            new(TestCellKind.Blank),
            new(TestCellKind.Blank),
            new(TestCellKind.Blank),
            new(TestCellKind.Blank),
            new(TestCellKind.Blank),
            new(TestCellKind.Blank),
            new(TestCellKind.Blank),
            new(TestCellKind.Blank),
            new(TestCellKind.Number, "100000"),
            new(TestCellKind.Number, "100000"),
            new(TestCellKind.Number, "50000"),
            new(TestCellKind.Number, "50000"),
            new(TestCellKind.Blank),
            new(TestCellKind.Formula, "300000", $"SUM(T{excelRow}:W{excelRow})"),
            new(TestCellKind.Formula, "5000000", $"SUM(H{excelRow}:R{excelRow})"),
            new(TestCellKind.Formula, "4700000", $"Y{excelRow}-X{excelRow}"),
            new(TestCellKind.Number, "12.5"),
        ];
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
