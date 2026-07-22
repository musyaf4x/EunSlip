using EunSlip.Core.Payroll;
using EunSlip.Infrastructure.Pdf;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace EunSlip.Infrastructure.Tests.Pdf;

public sealed class PayslipPdfGeneratorTests : IDisposable
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid().ToString("N"));
    private readonly string _stampPath;

    public PayslipPdfGeneratorTests()
    {
        Directory.CreateDirectory(_directory);
        _stampPath = Path.Combine(_directory, "stamp.png");
        File.WriteAllBytes(_stampPath, Convert.FromBase64String(TinyPngBase64));
    }

    private PayslipRequest Request(string? stampPath = null) => new(
        new BatchContext("JULY 2025", new DateOnly(2026, 5, 11)),
        new PayrollRow(
            Nik: "NIK0001", Nama: "Budi Santoso", Departement: "Finance", Position: "Staff",
            JoinDate: new DateOnly(2020, 1, 15), SalaryStatus: "Monthly", Email: "budi@example.com",
            Basic: 5_000_000L, Jabatan: 0L, TunjanganLembur: 0L, Haid: 0L, TunjanganLainLain: 0L,
            Koreksi: 0L, Kompensasi: 0L, Cuti: 0L, Transport: 0L, Lembur: 0L, Incentive: 0L,
            Pph21: 100_000L, Jht2Percent: 100_000L, Jp1Percent: 50_000L, Bpjs1Percent: 50_000L,
            Kehadiran: 0L, TotalPotongan: 300_000L, Total: 5_000_000L, Nett: 4_700_000L,
            OtHours: 12.5m),
        stampPath ?? _stampPath);

    [Fact]
    public void Generate_ProducesExactlyOnePage()
    {
        string output = Path.Combine(_directory, "one.pdf");

        new PayslipPdfGenerator().Generate(Request(), output);

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(1, document.PageCount);
    }

    [Fact]
    public void Generate_EmbedsStampImage()
    {
        string output = Path.Combine(_directory, "stamped.pdf");

        new PayslipPdfGenerator().Generate(Request(), output);

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.True(
            document.Pages[0].Resources.Elements.ContainsKey("/XObject"),
            "PDF must contain the stamp image resource.");
    }

    [Fact]
    public void Generate_LongIdentityAndNegativeValues_RemainsOnePageWithStamp()
    {
        PayslipRequest request = Request() with
        {
            Row = Request().Row with
            {
                Nama = "NAMA KARYAWAN DENGAN IDENTITAS PROFESIONAL YANG PANJANG",
                Departement = "HUMAN RESOURCES AND GENERAL AFFAIRS",
                Position = "SENIOR OPERATIONAL PAYROLL MANAGER",
                Koreksi = -99_999_999L,
                Total = 999_999_999L,
                Nett = 888_888_888L,
            },
        };
        string output = Path.Combine(_directory, "long-values.pdf");

        new PayslipPdfGenerator().Generate(request, output);

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(1, document.PageCount);
        Assert.True(document.Pages[0].Resources.Elements.ContainsKey("/XObject"));
    }

    [Fact]
    public void Generate_MissingStamp_Throws()
    {
        string output = Path.Combine(_directory, "out.pdf");

        Assert.Throws<PayslipGenerationException>(() =>
            new PayslipPdfGenerator().Generate(Request() with { StampImagePath = null }, output));
        Assert.Throws<PayslipGenerationException>(() =>
            new PayslipPdfGenerator().Generate(Request() with { StampImagePath = "does-not-exist.png" }, output));
    }

    [Fact]
    public void Generate_UnreadableStamp_Throws()
    {
        string garbage = Path.Combine(_directory, "garbage.png");
        File.WriteAllText(garbage, "not an image");
        string output = Path.Combine(_directory, "out.pdf");

        Assert.Throws<PayslipGenerationException>(() =>
            new PayslipPdfGenerator().Generate(Request(stampPath: garbage), output));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
