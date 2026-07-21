using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EunSlip.Core.Common;

namespace EunSlip.Core.Payroll;

public static class PayrollFingerprint
{
    private const char FieldSeparator = '\u001F';
    private const char RowSeparator = '\u001E';

    public static string Compute(BatchContext context, IReadOnlyList<PayrollRow> rows)
    {
        StringBuilder builder = new();
        _ = builder.Append(context.Period.Trim())
            .Append(FieldSeparator)
            .Append(context.PaymentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        foreach (PayrollRow row in rows.OrderBy(r => r.Nik, StringComparer.Ordinal))
        {
            _ = builder.Append(RowSeparator).Append(row.Nik)
                .Append(FieldSeparator).Append(row.Nama)
                .Append(FieldSeparator).Append(row.Departement ?? string.Empty)
                .Append(FieldSeparator).Append(row.Position ?? string.Empty)
                .Append(FieldSeparator).Append(row.JoinDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.SalaryStatus ?? string.Empty)
                .Append(FieldSeparator).Append(row.Email.ToLowerInvariant())
                .Append(FieldSeparator).Append(row.Basic.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Jabatan.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.TunjanganLembur.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Haid.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.TunjanganLainLain.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Koreksi.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Kompensasi.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Cuti.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Transport.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Lembur.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Incentive.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Pph21.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Jht2Percent.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Jp1Percent.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Bpjs1Percent.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Kehadiran.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.TotalPotongan.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Total.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(row.Nett.ToString(CultureInfo.InvariantCulture))
                .Append(FieldSeparator).Append(PayrollFormatting.FormatOtHours(row.OtHours));
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
