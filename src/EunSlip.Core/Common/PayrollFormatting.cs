using System.Globalization;
using System.Text;

namespace EunSlip.Core.Common;

public static class PayrollFormatting
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public static string FormatNominal(long value)
    {
        return value.ToString("N0", English);
    }

    public static string FormatJoinDate(DateOnly date)
    {
        return date.ToString("dd-MMM-yyyy", English);
    }

    public static string FormatPaymentDate(DateOnly date)
    {
        return date.ToString("dd-MMM-yyyy", Indonesian);
    }

    public static string FormatOtHours(decimal hours)
    {
        return hours.ToString("0.#", English);
    }

    public static string BuildPayslipFileName(string period, string nik)
    {
        return $"Slip_Gaji_{MakeFileNameSafe(period)}_{MakeFileNameSafe(nik)}.pdf";
    }

    private static string MakeFileNameSafe(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(value.Trim());
        for (int i = 0; i < builder.Length; i++)
        {
            char c = builder[i];
            if (c == ' ')
            {
                builder[i] = '_';
            }
            else if (Array.IndexOf(invalid, c) >= 0)
            {
                builder[i] = '-';
            }
        }
        return builder.ToString();
    }
}
