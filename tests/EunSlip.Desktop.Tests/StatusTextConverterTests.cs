using System.Globalization;
using EunSlip.Core.Persistence;
using EunSlip.Desktop.Converters;
using EunSlip.Desktop.Localization;
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop.Tests;

public sealed class StatusTextConverterTests
{
    private readonly StatusTextConverter _converter = new();
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    [Theory]
    [InlineData(BatchStatus.Interrupted, "BatchStatus_Interrupted")]
    [InlineData(RecipientStatus.Failed, "RecipientStatus_Failed")]
    [InlineData(AttemptStatus.Sent, "AttemptStatus_Sent")]
    [InlineData(AttemptType.RecoveryRetry, "AttemptType_RecoveryRetry")]
    [InlineData(PayrollRunMode.FailedRetry, "PayrollRunMode_FailedRetry")]
    public void Convert_UsesLocalizedResourceKey(object status, string key)
    {
        object result = _converter.Convert(status, typeof(string), null, Indonesian);

        Assert.Equal(Strings.GetForCulture(key, Indonesian), result);
        Assert.NotEqual(status.ToString(), result);
    }

    [Fact]
    public void Convert_NullUsesEmDash()
    {
        Assert.Equal("—", _converter.Convert(null, typeof(string), null, Indonesian));
    }
}
