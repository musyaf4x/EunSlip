using EunSlip.Core.Common;

namespace EunSlip.Core.Tests.Common;

public sealed class PayrollFormattingTests
{
    [Theory]
    [InlineData(500000, "500,000")]
    [InlineData(-500000, "-500,000")]
    [InlineData(0, "0")]
    public void FormatNominal_UsesThousandsSeparator_NoDecimals(long value, string expected)
    {
        Assert.Equal(expected, PayrollFormatting.FormatNominal(value));
    }

    [Fact]
    public void FormatJoinDate_UsesEnglishShortMonth()
    {
        Assert.Equal("01-May-2017", PayrollFormatting.FormatJoinDate(new DateOnly(2017, 5, 1)));
    }

    [Fact]
    public void FormatPaymentDate_UsesIndonesianMonthName()
    {
        Assert.Equal("11-Mei-2026", PayrollFormatting.FormatPaymentDate(new DateOnly(2026, 5, 11)));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(12.0, "12")]
    [InlineData(12.5, "12.5")]
    public void FormatOtHours_TrimsWholeNumbers_KeepsOneDecimal(decimal hours, string expected)
    {
        Assert.Equal(expected, PayrollFormatting.FormatOtHours(hours));
    }

    [Fact]
    public void BuildPayslipFileName_UsesPeriodAndNik()
    {
        Assert.Equal(
            "Slip_Gaji_JULY_2025_12345.pdf",
            PayrollFormatting.BuildPayslipFileName("JULY 2025", "12345"));
    }

    [Theory]
    [InlineData("JULY/2025", "Slip_Gaji_JULY-2025_12345.pdf")]
    [InlineData("JULY:2025?", "Slip_Gaji_JULY-2025-_12345.pdf")]
    public void BuildPayslipFileName_NormalizesUnsafeCharacters(string period, string expected)
    {
        Assert.Equal(expected, PayrollFormatting.BuildPayslipFileName(period, "12345"));
    }

    [Fact]
    public void BuildPayslipFileName_NormalizesUnsafeNikCharacters()
    {
        Assert.Equal(
            "Slip_Gaji_JULY_2025_12-34.pdf",
            PayrollFormatting.BuildPayslipFileName("JULY 2025", "12/34"));
    }
}
