namespace EunSlip.Core.Payroll;

public static class PayrollContract
{
    public const int ColumnCount = 27;
    public const int MaxEmployeeRows = 500;

    public static IReadOnlyList<string> Headers { get; } =
    [
        "NIK", "NAMA", "Departement", "Position", "Join Date", "Salary Status", "email",
        "Basic", "Jabatan", "Tunjangan Lembur", "Haid", "Tunjangan Lain-lain", "Koreksi",
        "Kompensasi", "Cuti", "transport", "Lembur", "Incentive", "Pph21", "JHT 2%",
        "JP 1%", "BPJS 1%", "Kehadiran", "Total Potongan", "Total", "Nett", "OT Hours"
    ];
}
