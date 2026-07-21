using EunSlip.Core.Payroll;
using EunSlip.Infrastructure.Excel;

namespace EunSlip.Infrastructure.Tests.Excel;

public sealed class OpenXmlWorkbookReaderTests
{
    private static readonly string[] ContractHeaders =
    [
        "NIK", "NAMA", "Departement", "Position", "Join Date", "Salary Status", "email",
        "Basic", "Jabatan", "Tunjangan Lembur", "Haid", "Tunjangan Lain-lain", "Koreksi",
        "Kompensasi", "Cuti", "transport", "Lembur", "Incentive", "Pph21", "JHT 2%",
        "JP 1%", "BPJS 1%", "Kehadiran", "Total Potongan", "Total", "Nett", "OT Hours"
    ];

    private static TestCell[] ValidEmployeeRow(string nik, string email) =>
    [
        new(TestCellKind.SharedString, nik),
        new(TestCellKind.SharedString, "Employee Name"),
        new(TestCellKind.SharedString, "Finance"),
        new(TestCellKind.SharedString, "Staff"),
        new(TestCellKind.DateSerial, "43845"), // 2020-01-15
        new(TestCellKind.SharedString, "Monthly"),
        new(TestCellKind.SharedString, email),
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
        new(TestCellKind.Formula, "300000", "SUM(T2:W2)"),
        new(TestCellKind.Formula, "5000000", "SUM(H2:R2)"),
        new(TestCellKind.Formula, "4700000", "Y2-X2"),
        new(TestCellKind.Number, "12.5"),
    ];

    [Fact]
    public void Read_ReturnsHeadersAndNormalizedRow()
    {
        string path = TestWorkbook.Create(
        [
            TestWorkbook.HeaderRow(ContractHeaders),
            ValidEmployeeRow("NIK0001", "a@b.co"),
        ]);

        WorkbookReadResult result = new OpenXmlWorkbookReader().Read(path);

        Assert.Equal(ContractHeaders, result.Headers);
        Assert.Empty(result.ReadIssues);
        PayrollRowInput row = Assert.Single(result.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("NIK0001", row.Nik);
        Assert.Equal("Employee Name", row.Nama);
        Assert.Equal("a@b.co", row.Email);
        Assert.Equal(new DateOnly(2020, 1, 15), row.JoinDate);
        Assert.Equal(5_000_000m, row.Basic);
        Assert.Null(row.Jabatan);
        Assert.Equal(300_000m, row.TotalPotongan);
        Assert.Equal(5_000_000m, row.Total);
        Assert.Equal(4_700_000m, row.Nett);
        Assert.Equal(12.5m, row.OtHours);
    }

    [Fact]
    public void Read_UsesFirstWorksheetOnly()
    {
        string path = TestWorkbook.Create(
            [TestWorkbook.HeaderRow(ContractHeaders), ValidEmployeeRow("NIK0001", "a@b.co")],
            addSecondSheet: true);

        WorkbookReadResult result = new OpenXmlWorkbookReader().Read(path);

        Assert.Equal("NIK", result.Headers[0]);
        PayrollRowInput row = Assert.Single(result.Rows);
        Assert.DoesNotContain("WRONG_SHEET", row.Nik);
    }

    [Fact]
    public void FormulaWithoutCachedValue_ProducesBlockingReadIssue()
    {
        TestCell[] row = ValidEmployeeRow("NIK0001", "a@b.co");
        row[23] = new TestCell(TestCellKind.FormulaNoCache, Formula: "SUM(T2:W2)");
        string path = TestWorkbook.Create([TestWorkbook.HeaderRow(ContractHeaders), row]);

        WorkbookReadResult result = new OpenXmlWorkbookReader().Read(path);

        Assert.Contains(result.ReadIssues, i =>
            i.Code == "CachedValueMissing" && i.RowNumber == 2 && i.Field == "Total Potongan");
        Assert.Null(result.Rows[0].TotalPotongan);
    }

    [Fact]
    public void CompletelyEmptyRows_AreSkipped()
    {
        TestCell[] emptyRow = [.. Enumerable.Repeat(new TestCell(TestCellKind.Blank), 27)];
        string path = TestWorkbook.Create(
        [
            TestWorkbook.HeaderRow(ContractHeaders),
            ValidEmployeeRow("NIK0001", "a@b.co"),
            emptyRow,
            ValidEmployeeRow("NIK0002", "c@d.co"),
        ]);

        WorkbookReadResult result = new OpenXmlWorkbookReader().Read(path);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("NIK0001", result.Rows[0].Nik);
        Assert.Equal("NIK0002", result.Rows[1].Nik);
        Assert.Equal(4, result.Rows[1].RowNumber);
    }

    [Fact]
    public void InlineStringCell_IsRead()
    {
        TestCell[] row = ValidEmployeeRow("NIK0001", "a@b.co");
        row[1] = new TestCell(TestCellKind.InlineString, "Inline Name");
        string path = TestWorkbook.Create([TestWorkbook.HeaderRow(ContractHeaders), row]);

        WorkbookReadResult result = new OpenXmlWorkbookReader().Read(path);

        Assert.Equal("Inline Name", result.Rows[0].Nama);
    }

    [Fact]
    public void IsoDateCell_IsRead()
    {
        TestCell[] row = ValidEmployeeRow("NIK0001", "a@b.co");
        row[4] = new TestCell(TestCellKind.DateIso, "2017-05-01");
        string path = TestWorkbook.Create([TestWorkbook.HeaderRow(ContractHeaders), row]);

        WorkbookReadResult result = new OpenXmlWorkbookReader().Read(path);

        Assert.Equal(new DateOnly(2017, 5, 1), result.Rows[0].JoinDate);
    }

    [Fact]
    public void NonNumericInNumericColumn_ProducesBlockingReadIssue()
    {
        TestCell[] row = ValidEmployeeRow("NIK0001", "a@b.co");
        row[7] = new TestCell(TestCellKind.SharedString, "bukan-angka");
        string path = TestWorkbook.Create([TestWorkbook.HeaderRow(ContractHeaders), row]);

        WorkbookReadResult result = new OpenXmlWorkbookReader().Read(path);

        Assert.Contains(result.ReadIssues, i =>
            i.Code == "InvalidNumeric" && i.RowNumber == 2 && i.Field == "Basic");
    }

    [Fact]
    public void GarbageInDateColumn_ProducesBlockingReadIssue()
    {
        TestCell[] row = ValidEmployeeRow("NIK0001", "a@b.co");
        row[4] = new TestCell(TestCellKind.SharedString, "bukan-tanggal");
        string path = TestWorkbook.Create([TestWorkbook.HeaderRow(ContractHeaders), row]);

        WorkbookReadResult result = new OpenXmlWorkbookReader().Read(path);

        Assert.Contains(result.ReadIssues, i =>
            i.Code == "InvalidDate" && i.RowNumber == 2 && i.Field == "Join Date");
        Assert.Null(result.Rows[0].JoinDate);
    }

    [Fact]
    public void ExtraColumnHeader_AppearsInHeaders()
    {
        string path = TestWorkbook.Create(
        [
            TestWorkbook.HeaderRow([.. ContractHeaders, "Extra"]),
            [.. ValidEmployeeRow("NIK0001", "a@b.co"), new TestCell(TestCellKind.SharedString, "x")],
        ]);

        WorkbookReadResult result = new OpenXmlWorkbookReader().Read(path);

        Assert.Equal(28, result.Headers.Count);
        Assert.Equal("Extra", result.Headers[27]);
    }

    [Fact]
    public void MissingFile_ThrowsWorkbookUnreadable()
    {
        string path = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid() + ".xlsx");

        Assert.Throws<WorkbookUnreadableException>(() => new OpenXmlWorkbookReader().Read(path));
    }

    [Fact]
    public void NonXlsxFile_ThrowsWorkbookUnreadable()
    {
        string path = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid() + ".xlsx");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not a zip");

        Assert.Throws<WorkbookUnreadableException>(() => new OpenXmlWorkbookReader().Read(path));
    }

    [Fact]
    public void ApprovedTemplate_ReadsContractHeaders()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string template = Path.Combine(repoRoot, "EunSlip_Payroll_Template.xlsx");

        WorkbookReadResult result = new OpenXmlWorkbookReader().Read(template);

        Assert.Equal(ContractHeaders, result.Headers);
    }
}
