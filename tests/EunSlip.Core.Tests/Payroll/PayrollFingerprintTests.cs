using EunSlip.Core.Payroll;

namespace EunSlip.Core.Tests.Payroll;

public sealed class PayrollFingerprintTests
{
    private static readonly BatchContext Context = new("JULY 2025", new DateOnly(2025, 7, 31));

    private static PayrollRow Row(int n) => new(
        Nik: $"NIK{n:D4}",
        Nama: $"Employee {n}",
        Departement: "Finance",
        Position: "Staff",
        JoinDate: new DateOnly(2020, 1, 15),
        SalaryStatus: "Monthly",
        Email: $"Employee{n}@Example.com",
        Basic: 5_000_000L,
        Jabatan: 0L,
        TunjanganLembur: 0L,
        Haid: 0L,
        TunjanganLainLain: 0L,
        Koreksi: 0L,
        Kompensasi: 0L,
        Cuti: 0L,
        Transport: 0L,
        Lembur: 0L,
        Incentive: 0L,
        Pph21: 100_000L,
        Jht2Percent: 100_000L,
        Jp1Percent: 50_000L,
        Bpjs1Percent: 50_000L,
        Kehadiran: 0L,
        TotalPotongan: 300_000L,
        Total: 5_000_000L,
        Nett: 4_700_000L,
        OtHours: 0m);

    [Fact]
    public void Compute_IsDeterministic()
    {
        string first = PayrollFingerprint.Compute(Context, [Row(1), Row(2)]);
        string second = PayrollFingerprint.Compute(Context, [Row(1), Row(2)]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_Is64LowerHexCharacters_Sha256()
    {
        string hash = PayrollFingerprint.Compute(Context, [Row(1)]);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ReorderedRows_ProduceSameFingerprint()
    {
        string first = PayrollFingerprint.Compute(Context, [Row(1), Row(2), Row(3)]);
        string reordered = PayrollFingerprint.Compute(Context, [Row(3), Row(1), Row(2)]);

        Assert.Equal(first, reordered);
    }

    [Fact]
    public void ChangedField_ProducesDifferentFingerprint()
    {
        string original = PayrollFingerprint.Compute(Context, [Row(1)]);
        string changed = PayrollFingerprint.Compute(Context, [Row(1) with { Basic = 5_000_001L }]);

        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void DifferentPeriod_ProducesDifferentFingerprint()
    {
        string original = PayrollFingerprint.Compute(Context, [Row(1)]);
        string changed = PayrollFingerprint.Compute(
            Context with { Period = "AUGUST 2025" }, [Row(1)]);

        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void DifferentPaymentDate_ProducesDifferentFingerprint()
    {
        string original = PayrollFingerprint.Compute(Context, [Row(1)]);
        string changed = PayrollFingerprint.Compute(
            Context with { PaymentDate = new DateOnly(2025, 8, 1) }, [Row(1)]);

        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void EmailCasing_DoesNotChangeFingerprint()
    {
        string lower = PayrollFingerprint.Compute(
            Context, [Row(1) with { Email = "employee1@example.com" }]);
        string upper = PayrollFingerprint.Compute(
            Context, [Row(1) with { Email = "EMPLOYEE1@EXAMPLE.COM" }]);

        Assert.Equal(lower, upper);
    }

    [Fact]
    public void OtHoursScale_DoesNotChangeFingerprint()
    {
        string integer = PayrollFingerprint.Compute(Context, [Row(1) with { OtHours = 12m }]);
        string scaled = PayrollFingerprint.Compute(Context, [Row(1) with { OtHours = 12.0m }]);

        Assert.Equal(integer, scaled);
    }
}
