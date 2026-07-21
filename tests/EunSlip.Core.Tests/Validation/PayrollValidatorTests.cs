using EunSlip.Core.Payroll;
using EunSlip.Core.Validation;

namespace EunSlip.Core.Tests.Validation;

public sealed class PayrollValidatorTests
{
    private static PayrollRowInput ValidRow(int n) => new(
        RowNumber: n + 1,
        Nik: $"NIK{n:D4}",
        Nama: $"Employee {n}",
        Departement: "Finance",
        Position: "Staff",
        JoinDate: new DateOnly(2020, 1, 15),
        SalaryStatus: "Monthly",
        Email: $"employee{n}@example.com",
        Basic: 5_000_000m,
        Jabatan: null,
        TunjanganLembur: null,
        Haid: null,
        TunjanganLainLain: null,
        Koreksi: null,
        Kompensasi: null,
        Cuti: null,
        Transport: null,
        Lembur: null,
        Incentive: null,
        Pph21: 100_000m,
        Jht2Percent: 100_000m,
        Jp1Percent: 50_000m,
        Bpjs1Percent: 50_000m,
        Kehadiran: null,
        TotalPotongan: 300_000m,
        Total: 5_000_000m,
        Nett: 4_700_000m,
        OtHours: null);


    [Fact]
    public void ValidSingleRow_ProducesNormalizedRow_NoBlockingIssues()
    {
        ValidationResult result = PayrollValidator.Validate(
            PayrollContract.Headers, [ValidRow(1)]);

        Assert.True(result.CanProceed);
        Assert.Empty(result.Issues);
        PayrollRow row = Assert.Single(result.ValidRows);
        Assert.Equal("NIK0001", row.Nik);
        Assert.Equal(5_000_000L, row.Basic);
        Assert.Equal(0L, row.Jabatan);
        Assert.Equal(0m, row.OtHours);
    }

    [Theory]
    [InlineData(0, "NIK_WRONG")]
    [InlineData(6, "Email")]
    [InlineData(26, "OT Hours!")]
    public void RenamedHeader_IsBlocking_AndIdentifiesPosition(int columnIndex, string wrongName)
    {
        string[] headers = [.. PayrollContract.Headers];
        headers[columnIndex] = wrongName;

        ValidationResult result = PayrollValidator.Validate(headers, [ValidRow(1)]);

        Assert.False(result.CanProceed);
        PayrollIssue issue = Assert.Single(result.Issues);
        Assert.Equal(IssueSeverity.Blocking, issue.Severity);
        Assert.Equal("TemplateMismatch", issue.Code);
        Assert.Equal(PayrollContract.Headers[columnIndex], issue.ExpectedValue);
        Assert.Equal(wrongName, issue.StoredValue);
    }

    [Fact]
    public void ReorderedHeaders_IsBlocking()
    {
        string[] headers = [.. PayrollContract.Headers];
        (headers[0], headers[1]) = (headers[1], headers[0]);

        ValidationResult result = PayrollValidator.Validate(headers, [ValidRow(1)]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "TemplateMismatch");
    }

    [Fact]
    public void MissingColumn_IsBlocking()
    {
        string[] headers = [.. PayrollContract.Headers.Take(26)];

        ValidationResult result = PayrollValidator.Validate(headers, [ValidRow(1)]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "TemplateMismatch");
    }

    [Fact]
    public void ExtraColumn_IsBlocking()
    {
        string[] headers = [.. PayrollContract.Headers, "Extra"];

        ValidationResult result = PayrollValidator.Validate(headers, [ValidRow(1)]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "TemplateMismatch");
    }

    [Fact]
    public void MoreThan500Rows_IsBlocking()
    {
        PayrollRowInput[] rows = [.. Enumerable.Range(1, 501).Select(ValidRow)];

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, rows);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "TooManyRows");
    }

    [Fact]
    public void BlankNik_IsBlocking()
    {
        PayrollRowInput row = ValidRow(1) with { Nik = "  " };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "RequiredFieldBlank" && i.Field == "NIK" && i.RowNumber == 2);
    }

    [Fact]
    public void BlankNama_IsBlocking()
    {
        PayrollRowInput row = ValidRow(1) with { Nama = null };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "RequiredFieldBlank" && i.Field == "NAMA");
    }

    [Fact]
    public void BlankEmail_IsBlocking()
    {
        PayrollRowInput row = ValidRow(1) with { Email = "" };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "RequiredFieldBlank" && i.Field == "email");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@nodomain.com")]
    public void InvalidEmail_IsBlocking(string email)
    {
        PayrollRowInput row = ValidRow(1) with { Email = email };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "InvalidEmail");
    }

    [Fact]
    public void DuplicateNik_IsBlocking()
    {
        PayrollRowInput first = ValidRow(1);
        PayrollRowInput second = ValidRow(2) with { Nik = first.Nik };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [first, second]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "DuplicateNik");
    }

    [Fact]
    public void DuplicateEmail_DifferentCasing_IsBlocking()
    {
        PayrollRowInput first = ValidRow(1);
        PayrollRowInput second = ValidRow(2) with { Email = first.Email!.ToUpperInvariant() };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [first, second]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "DuplicateEmail");
    }

    [Fact]
    public void MissingJoinDate_IsBlocking()
    {
        PayrollRowInput row = ValidRow(1) with { JoinDate = null };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "InvalidJoinDate");
    }

    [Theory]
    [InlineData("TotalPotongan")]
    [InlineData("Total")]
    [InlineData("Nett")]
    public void MissingCachedTotal_IsBlocking(string field)
    {
        PayrollRowInput row = field switch
        {
            "TotalPotongan" => ValidRow(1) with { TotalPotongan = null },
            "Total" => ValidRow(1) with { Total = null },
            _ => ValidRow(1) with { Nett = null },
        };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "MissingCachedValue" && i.Field == field);
    }

    [Fact]
    public void FractionalNominal_IsBlocking()
    {
        PayrollRowInput row = ValidRow(1) with { Basic = 5_000_000.50m };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "FractionalNominal" && i.Field == "Basic");
    }

    [Fact]
    public void NegativeNominal_IsAccepted()
    {
        PayrollRowInput row = ValidRow(1) with
        {
            Koreksi = -1_500_000m,
            Total = 3_500_000m,
            Nett = 3_200_000m,
        };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.True(result.CanProceed);
        Assert.Equal(-1_500_000L, result.ValidRows[0].Koreksi);
    }

    [Fact]
    public void OtHoursBlank_BecomesZero()
    {
        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [ValidRow(1)]);

        Assert.Equal(0m, result.ValidRows[0].OtHours);
    }

    [Fact]
    public void OtHoursWholeNumber_IsAccepted()
    {
        PayrollRowInput row = ValidRow(1) with { OtHours = 12.0m };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.True(result.CanProceed);
        Assert.Equal(12.0m, result.ValidRows[0].OtHours);
    }

    [Fact]
    public void OtHoursOneDecimal_IsAccepted()
    {
        PayrollRowInput row = ValidRow(1) with { OtHours = 12.5m };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.True(result.CanProceed);
        Assert.Equal(12.5m, result.ValidRows[0].OtHours);
    }

    [Fact]
    public void OtHoursMoreThanOneDecimal_IsBlocking()
    {
        PayrollRowInput row = ValidRow(1) with { OtHours = 12.55m };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "OtHoursTooManyDecimals");
    }

    [Fact]
    public void TotalMismatch_IsWarning_WithStoredAndExpected()
    {
        PayrollRowInput row = ValidRow(1) with { Total = 4_999_999m, Nett = 4_699_999m };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.True(result.CanProceed);
        PayrollIssue warning = Assert.Single(result.Issues);
        Assert.Equal(IssueSeverity.Warning, warning.Severity);
        Assert.Equal("TotalMismatch", warning.Code);
        Assert.Equal("NIK0001", warning.Nik);
        Assert.Equal("Total", warning.Field);
        Assert.Equal("4999999", warning.StoredValue);
        Assert.Equal("5000000", warning.ExpectedValue);
    }

    [Fact]
    public void TotalPotonganMismatch_IsWarning()
    {
        PayrollRowInput row = ValidRow(1) with { TotalPotongan = 1m };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.True(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "TotalPotonganMismatch" && i.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void NettMismatch_IsWarning()
    {
        PayrollRowInput row = ValidRow(1) with { Nett = 1m };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.True(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "NettMismatch" && i.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void PreviouslySentNik_IsWarning()
    {
        HashSet<string> previouslySent = ["NIK0001"];

        ValidationResult result = PayrollValidator.Validate(
            PayrollContract.Headers, [ValidRow(1)], previouslySent);

        Assert.True(result.CanProceed);
        PayrollIssue warning = Assert.Single(result.Issues);
        Assert.Equal("PreviouslySent", warning.Code);
        Assert.Equal(IssueSeverity.Warning, warning.Severity);
        Assert.Equal("NIK0001", warning.Nik);
    }

    [Fact]
    public void RowsWithBlockingIssues_AreExcludedFromValidRows()
    {
        PayrollRowInput bad = ValidRow(1) with { Email = "bad" };
        PayrollRowInput good = ValidRow(2);

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [bad, good]);

        Assert.False(result.CanProceed);
        PayrollRow row = Assert.Single(result.ValidRows);
        Assert.Equal("NIK0002", row.Nik);
    }

    [Fact]
    public void NominalBeyondLongRange_IsBlocking_NotCrash()
    {
        PayrollRowInput row = ValidRow(1) with { Basic = 1e20m };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "NominalOutOfRange" && i.Field == "Basic");
    }

    [Fact]
    public void HugeIntegralOtHours_IsAccepted_WithoutOverflow()
    {
        PayrollRowInput row = ValidRow(1) with { OtHours = 79_228_162_514_264_337_593_543_950_335m };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.DoesNotContain(result.Issues, i => i.Code == "OtHoursTooManyDecimals");
    }

    [Fact]
    public void ControlCharacterInText_IsBlocking()
    {
        PayrollRowInput row = ValidRow(1) with { Nama = "Evil\u001FName" };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "InvalidCharacters" && i.Field == "NAMA");
    }

    [Fact]
    public void EmailWithDisplayName_IsBlocking()
    {
        PayrollRowInput row = ValidRow(1) with { Email = "John <john@example.co>" };

        ValidationResult result = PayrollValidator.Validate(PayrollContract.Headers, [row]);

        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, i => i.Code == "InvalidEmail");
    }

    [Fact]
    public void PreviouslySentNik_MatchesCaseInsensitively()
    {
        HashSet<string> previouslySent = ["nik0001"];

        ValidationResult result = PayrollValidator.Validate(
            PayrollContract.Headers, [ValidRow(1)], previouslySent);

        Assert.Contains(result.Issues, i => i.Code == "PreviouslySent");
    }
}
