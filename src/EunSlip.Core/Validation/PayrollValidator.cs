using System.Net.Mail;
using EunSlip.Core.Payroll;
using EunSlip.Core.Persistence;

namespace EunSlip.Core.Validation;

public static class PayrollValidator
{
    public static ValidationResult Validate(
        IReadOnlyList<string> headers,
        IReadOnlyList<PayrollRowInput> rows,
        IReadOnlySet<string>? previouslySentNiks = null)
    {
        List<PayrollIssue> issues = [];
        ValidateHeaders(headers, issues);

        if (rows.Count > PayrollContract.MaxEmployeeRows)
        {
            issues.Add(new PayrollIssue(
                IssueSeverity.Blocking, "TooManyRows", null, null, null,
                rows.Count.ToStringInvariant(), PayrollContract.MaxEmployeeRows.ToStringInvariant()));
        }

        List<PayrollRow> validRows = [];
        foreach (PayrollRowInput row in rows)
        {
            PayrollRow? normalized = NormalizeRow(row, issues);
            if (normalized is not null)
            {
                AddCalculationWarnings(normalized, issues);
                AddPreviouslySentWarning(normalized, previouslySentNiks, issues);
                validRows.Add(normalized);
            }
        }

        AddDuplicateIssues(rows, issues);

        return new ValidationResult(issues, validRows);
    }

    private static void ValidateHeaders(IReadOnlyList<string> headers, List<PayrollIssue> issues)
    {
        if (headers.Count != PayrollContract.ColumnCount)
        {
            issues.Add(new PayrollIssue(
                IssueSeverity.Blocking, "TemplateMismatch", null, null, null,
                $"{headers.Count} columns", $"{PayrollContract.ColumnCount} columns"));
            return;
        }

        for (int i = 0; i < PayrollContract.ColumnCount; i++)
        {
            if (!string.Equals(headers[i], PayrollContract.Headers[i], StringComparison.Ordinal))
            {
                issues.Add(new PayrollIssue(
                    IssueSeverity.Blocking, "TemplateMismatch", null, null, PayrollContract.Headers[i],
                    headers[i], PayrollContract.Headers[i]));
            }
        }
    }

    private static PayrollRow? NormalizeRow(PayrollRowInput row, List<PayrollIssue> issues)
    {
        string nik = row.Nik?.Trim() ?? string.Empty;
        string nama = row.Nama?.Trim() ?? string.Empty;
        string email = row.Email?.Trim() ?? string.Empty;

        foreach ((string? raw, string field) in new (string?, string)[]
        {
            (row.Nik, "NIK"), (row.Nama, "NAMA"), (row.Departement, "Departement"),
            (row.Position, "Position"), (row.SalaryStatus, "Salary Status"), (row.Email, "email"),
        })
        {
            if (raw is not null && raw.Any(char.IsControl))
            {
                issues.Add(Blocking(row, "InvalidCharacters", field));
            }
        }

        if (nik.Length == 0)
        {
            issues.Add(Blocking(row, "RequiredFieldBlank", "NIK"));
        }

        if (nama.Length == 0)
        {
            issues.Add(Blocking(row, "RequiredFieldBlank", "NAMA"));
        }

        if (email.Length == 0)
        {
            issues.Add(Blocking(row, "RequiredFieldBlank", "email"));
        }
        else if (!IsValidEmail(email))
        {
            issues.Add(Blocking(row, "InvalidEmail", "email", email));
        }

        if (row.JoinDate is null)
        {
            issues.Add(Blocking(row, "InvalidJoinDate", "Join Date"));
        }

        long? basic = NormalizeNominal(row, "Basic", row.Basic, issues);
        long? jabatan = NormalizeNominal(row, "Jabatan", row.Jabatan, issues);
        long? tunjanganLembur = NormalizeNominal(row, "Tunjangan Lembur", row.TunjanganLembur, issues);
        long? haid = NormalizeNominal(row, "Haid", row.Haid, issues);
        long? tunjanganLainLain = NormalizeNominal(row, "Tunjangan Lain-lain", row.TunjanganLainLain, issues);
        long? koreksi = NormalizeNominal(row, "Koreksi", row.Koreksi, issues);
        long? kompensasi = NormalizeNominal(row, "Kompensasi", row.Kompensasi, issues);
        long? cuti = NormalizeNominal(row, "Cuti", row.Cuti, issues);
        long? transport = NormalizeNominal(row, "transport", row.Transport, issues);
        long? lembur = NormalizeNominal(row, "Lembur", row.Lembur, issues);
        long? incentive = NormalizeNominal(row, "Incentive", row.Incentive, issues);
        long? pph21 = NormalizeNominal(row, "Pph21", row.Pph21, issues);
        long? jht2 = NormalizeNominal(row, "JHT 2%", row.Jht2Percent, issues);
        long? jp1 = NormalizeNominal(row, "JP 1%", row.Jp1Percent, issues);
        long? bpjs1 = NormalizeNominal(row, "BPJS 1%", row.Bpjs1Percent, issues);
        long? kehadiran = NormalizeNominal(row, "Kehadiran", row.Kehadiran, issues);
        long? totalPotongan = NormalizeRequiredTotal(row, "TotalPotongan", row.TotalPotongan, issues);
        long? total = NormalizeRequiredTotal(row, "Total", row.Total, issues);
        long? nett = NormalizeRequiredTotal(row, "Nett", row.Nett, issues);

        decimal otHours = 0m;
        if (row.OtHours is decimal ot)
        {
            if (Math.Round(ot, 1) != ot)
            {
                issues.Add(Blocking(row, "OtHoursTooManyDecimals", "OT Hours", ot.ToStringInvariant()));
            }
            else
            {
                otHours = ot;
            }
        }

        bool rowBlocking = issues.Any(i =>
            i.Severity == IssueSeverity.Blocking && i.RowNumber == row.RowNumber);
        return rowBlocking
            ? null
            : new PayrollRow(
            nik, nama,
            string.IsNullOrWhiteSpace(row.Departement) ? null : row.Departement.Trim(),
            string.IsNullOrWhiteSpace(row.Position) ? null : row.Position.Trim(),
            row.JoinDate!.Value,
            string.IsNullOrWhiteSpace(row.SalaryStatus) ? null : row.SalaryStatus.Trim(),
            email,
            basic!.Value, jabatan!.Value, tunjanganLembur!.Value, haid!.Value,
            tunjanganLainLain!.Value, koreksi!.Value, kompensasi!.Value, cuti!.Value,
            transport!.Value, lembur!.Value, incentive!.Value,
            pph21!.Value, jht2!.Value, jp1!.Value, bpjs1!.Value, kehadiran!.Value,
            totalPotongan!.Value, total!.Value, nett!.Value, otHours);
    }

    private static long? NormalizeNominal(
        PayrollRowInput row, string field, decimal? value, List<PayrollIssue> issues)
    {
        if (value is null)
        {
            return 0L;
        }

        if (value.Value % 1m != 0m)
        {
            issues.Add(Blocking(row, "FractionalNominal", field, value.Value.ToStringInvariant()));
            return null;
        }

        if (value.Value is > long.MaxValue or < long.MinValue)
        {
            issues.Add(Blocking(row, "NominalOutOfRange", field, value.Value.ToStringInvariant()));
            return null;
        }

        return (long)value.Value;
    }

    private static long? NormalizeRequiredTotal(
        PayrollRowInput row, string field, decimal? value, List<PayrollIssue> issues)
    {
        if (value is null)
        {
            issues.Add(Blocking(row, "MissingCachedValue", field));
            return null;
        }

        if (value.Value % 1m != 0m)
        {
            issues.Add(Blocking(row, "FractionalNominal", field, value.Value.ToStringInvariant()));
            return null;
        }

        if (value.Value is > long.MaxValue or < long.MinValue)
        {
            issues.Add(Blocking(row, "NominalOutOfRange", field, value.Value.ToStringInvariant()));
            return null;
        }

        return (long)value.Value;
    }

    private static void AddCalculationWarnings(PayrollRow row, List<PayrollIssue> issues)
    {
        long expectedTotal = row.Basic + row.Jabatan + row.TunjanganLembur + row.Haid
            + row.TunjanganLainLain + row.Koreksi + row.Kompensasi + row.Cuti
            + row.Transport + row.Lembur + row.Incentive;
        long expectedPotongan = row.Pph21 + row.Jht2Percent + row.Jp1Percent
            + row.Bpjs1Percent + row.Kehadiran;
        long expectedNett = row.Total - row.TotalPotongan;

        if (row.Total != expectedTotal)
        {
            issues.Add(Warning(row, "TotalMismatch", "Total", row.Total, expectedTotal));
        }

        if (row.TotalPotongan != expectedPotongan)
        {
            issues.Add(Warning(row, "TotalPotonganMismatch", "Total Potongan", row.TotalPotongan, expectedPotongan));
        }

        if (row.Nett != expectedNett)
        {
            issues.Add(Warning(row, "NettMismatch", "Nett", row.Nett, expectedNett));
        }
    }

    private static void AddPreviouslySentWarning(
        PayrollRow row, IReadOnlySet<string>? previouslySentNiks, List<PayrollIssue> issues)
    {
        if (previouslySentNiks is not null
            && previouslySentNiks.Any(n => string.Equals(n, NikHint.LastFour(row.Nik), StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new PayrollIssue(
                IssueSeverity.Warning, "PreviouslySent", null, row.Nik, "NIK", row.Nik, null));
        }
    }

    private static void AddDuplicateIssues(IReadOnlyList<PayrollRowInput> rows, List<PayrollIssue> issues)
    {
        foreach (IGrouping<string, PayrollRowInput> group in rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Nik))
            .GroupBy(r => r.Nik!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            foreach (PayrollRowInput duplicate in group.Skip(1))
            {
                issues.Add(Blocking(duplicate, "DuplicateNik", "NIK", group.Key));
            }
        }

        foreach (IGrouping<string, PayrollRowInput> group in rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Email))
            .GroupBy(r => r.Email!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            foreach (PayrollRowInput duplicate in group.Skip(1))
            {
                issues.Add(Blocking(duplicate, "DuplicateEmail", "email", group.Key));
            }
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            MailAddress address = new(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase)
                && address.Host.Contains('.', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static PayrollIssue Blocking(PayrollRowInput row, string code, string? field, string? stored = null)
    {
        return new PayrollIssue(
            IssueSeverity.Blocking, code, row.RowNumber, row.Nik?.Trim(), field, stored, null);
    }

    private static PayrollIssue Warning(PayrollRow row, string code, string field, long stored, long expected)
    {
        return new PayrollIssue(
            IssueSeverity.Warning, code, null, row.Nik, field,
            stored.ToStringInvariant(), expected.ToStringInvariant());
    }
}

internal static class NumericFormatting
{
    public static string ToStringInvariant(this int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string ToStringInvariant(this long value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string ToStringInvariant(this decimal value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
