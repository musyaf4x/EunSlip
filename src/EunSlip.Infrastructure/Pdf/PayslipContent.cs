using EunSlip.Core.Common;
using EunSlip.Core.Payroll;

namespace EunSlip.Infrastructure.Pdf;

public static class PayslipContent
{
    public static string[] HeaderLines(PayslipRequest request) =>
        ["PT. EUNSUNG INDONESIA", $"SALARY {request.Context.Period}"];

    public static (string Label, string Value)[] Identity(PayslipRequest request)
    {
        PayrollRow row = request.Row;
        return
        [
            ("NIK", row.Nik),
            ("NAME", row.Nama),
            ("Departement", row.Departement ?? string.Empty),
            ("Position", row.Position ?? string.Empty),
            ("Join Date", PayrollFormatting.FormatJoinDate(row.JoinDate)),
            ("Salary", row.SalaryStatus ?? string.Empty),
        ];
    }

    public static (string Label, string Value)[] IncomeRows(PayslipRequest request)
    {
        PayrollRow row = request.Row;
        return
        [
            ("Basic", N(row.Basic)),
            ("Jabatan", N(row.Jabatan)),
            ("Tunjangan Lembur", N(row.TunjanganLembur)),
            ("Haid", N(row.Haid)),
            ("Tunjangan Lain-lain", N(row.TunjanganLainLain)),
            ("Koreksi", N(row.Koreksi)),
            ("Kompensasi", N(row.Kompensasi)),
            ("Cuti", N(row.Cuti)),
            ("Transport", N(row.Transport)),
            ("Lembur", N(row.Lembur)),
            ("Insentive", N(row.Incentive)),
        ];
    }

    public static (string Label, string Value)[] DeductionRows(PayslipRequest request)
    {
        PayrollRow row = request.Row;
        return
        [
            ("Pph21", N(row.Pph21)),
            ("JHT 2%", N(row.Jht2Percent)),
            ("JP 1%", N(row.Jp1Percent)),
            ("BPJS 1%", N(row.Bpjs1Percent)),
            ("Kehadiran", N(row.Kehadiran)),
        ];
    }

    public static string OtHoursText(PayslipRequest request) =>
        $"OT Hours : {PayrollFormatting.FormatOtHours(request.Row.OtHours)}";

    public static string IncomeTotal(PayslipRequest request) => N(request.Row.Total);
    public static string DeductionTotal(PayslipRequest request) => N(request.Row.TotalPotongan);
    public static string NettIncome(PayslipRequest request) => N(request.Row.Nett);

    public static string PaymentDateText(PayslipRequest request) =>
        PayrollFormatting.FormatPaymentDate(request.Context.PaymentDate);

    public const string MadeByText = "Made By";
    public const string AccText = "ACC";

    private static string N(long value) => PayrollFormatting.FormatNominal(value);
}
