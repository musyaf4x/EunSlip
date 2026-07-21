using EunSlip.Core.Payroll;

namespace EunSlip.Core.Tests.Payroll;

public sealed class PayrollContractTests
{
    [Fact]
    public void Headers_AreExactly27_InContractOrder()
    {
        string[] expected =
        [
            "NIK", "NAMA", "Departement", "Position", "Join Date", "Salary Status", "email",
            "Basic", "Jabatan", "Tunjangan Lembur", "Haid", "Tunjangan Lain-lain", "Koreksi",
            "Kompensasi", "Cuti", "transport", "Lembur", "Incentive", "Pph21", "JHT 2%",
            "JP 1%", "BPJS 1%", "Kehadiran", "Total Potongan", "Total", "Nett", "OT Hours"
        ];

        Assert.Equal(27, PayrollContract.ColumnCount);
        Assert.Equal(expected, PayrollContract.Headers);
    }

    [Fact]
    public void MaxEmployeeRows_Is500()
    {
        Assert.Equal(500, PayrollContract.MaxEmployeeRows);
    }
}
