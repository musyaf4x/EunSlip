using EunSlip.Core.Payroll;
using EunSlip.Infrastructure.Pdf;

namespace EunSlip.Infrastructure.Tests.Pdf;

public sealed class PayslipContentTests
{
    private static PayslipRequest Request(PayrollRow? row = null) => new(
        new BatchContext("JULY 2025", new DateOnly(2026, 5, 11)),
        row ?? ValidRow(),
        "stamp.png");

    private static PayrollRow ValidRow() => new(
        Nik: "NIK0001", Nama: "Budi Santoso", Departement: "Finance", Position: "Staff",
        JoinDate: new DateOnly(2020, 1, 15), SalaryStatus: "Monthly", Email: "budi@example.com",
        Basic: 5_000_000L, Jabatan: 0L, TunjanganLembur: 0L, Haid: 0L, TunjanganLainLain: 0L,
        Koreksi: 0L, Kompensasi: 0L, Cuti: 0L, Transport: 0L, Lembur: 0L, Incentive: 0L,
        Pph21: 100_000L, Jht2Percent: 100_000L, Jp1Percent: 50_000L, Bpjs1Percent: 50_000L,
        Kehadiran: 0L, TotalPotongan: 300_000L, Total: 5_000_000L, Nett: 4_700_000L,
        OtHours: 12.5m);

    [Fact]
    public void HeaderLines_AreCompanyAndSalaryPeriod()
    {
        Assert.Equal(
            ["PT. EUNSUNG INDONESIA", "SALARY JULY 2025"],
            PayslipContent.HeaderLines(Request()));
    }

    [Fact]
    public void Identity_MapsExcelFieldsToPdfLabels()
    {
        (string Label, string Value)[] identity = PayslipContent.Identity(Request());

        Assert.Contains(("NIK", "NIK0001"), identity);
        Assert.Contains(("NAME", "Budi Santoso"), identity);
        Assert.Contains(("Join Date", "15-Jan-2020"), identity);
        Assert.Contains(("Salary", "Monthly"), identity);
    }

    [Fact]
    public void IncomeRows_ContainsAllComponents_EvenZero()
    {
        (string Label, string Value)[] income = PayslipContent.IncomeRows(Request());

        Assert.Equal(11, income.Length);
        Assert.Contains(("Basic", "5,000,000"), income);
        Assert.Contains(("Jabatan", "0"), income);
        Assert.Contains(("Insentive", "0"), income);
        Assert.DoesNotContain(income, r => r.Label == "Incentive");
    }

    [Fact]
    public void DeductionRows_ContainsAllComponents()
    {
        (string Label, string Value)[] deduction = PayslipContent.DeductionRows(Request());

        Assert.Equal(5, deduction.Length);
        Assert.Contains(("Pph21", "100,000"), deduction);
        Assert.Contains(("JHT 2%", "100,000"), deduction);
        Assert.Contains(("Kehadiran", "0"), deduction);
    }

    [Fact]
    public void Totals_UseStoredExcelValues_EvenWhenMismatching()
    {
        PayrollRow mismatched = ValidRow() with { Total = 4_999_999L, Nett = 4_699_999L };
        PayslipRequest request = Request(mismatched);

        Assert.Equal("4,999,999", PayslipContent.IncomeTotal(request));
        Assert.Equal("4,699,999", PayslipContent.NettIncome(request));
    }

    [Fact]
    public void NegativeNominal_FormatsWithMinus()
    {
        PayrollRow negative = ValidRow() with { Koreksi = -1_500_000L };
        (string Label, string Value)[] income = PayslipContent.IncomeRows(Request(negative));

        Assert.Contains(("Koreksi", "-1,500,000"), income);
    }

    [Fact]
    public void OtHoursText_UsesSpecFormat()
    {
        Assert.Equal("OT Hours : 12.5", PayslipContent.OtHoursText(Request()));
    }

    [Fact]
    public void PaymentDateText_UsesIndonesianMonthName()
    {
        Assert.Equal("11-Mei-2026", PayslipContent.PaymentDateText(Request()));
    }
}
