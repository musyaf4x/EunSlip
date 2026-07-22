using EunSlip.Infrastructure.Pdf;

namespace EunSlip.Infrastructure.Tests.Pdf;

public sealed class PayslipLayoutTests
{
    [Fact]
    public void Create_KeepsEveryRegionInsideTheFrame()
    {
        PayslipLayout layout = PayslipLayoutFactory.Create(11, 5);

        foreach (PdfRect region in layout.ContentRegions)
        {
            Assert.True(layout.Frame.Contains(region), $"Region outside frame: {region}");
        }
    }

    [Fact]
    public void Create_StacksDocumentBandsWithoutOverlap()
    {
        PayslipLayout layout = PayslipLayoutFactory.Create(11, 5);
        PdfRect[] ordered =
        [
            layout.Header,
            layout.Identity,
            layout.TableHeader,
            layout.TableBody,
            layout.Totals,
            layout.NettIncome,
            layout.Metadata,
            layout.Authorization,
        ];

        for (int i = 0; i < ordered.Length - 1; i++)
        {
            Assert.True(ordered[i].Bottom <= ordered[i + 1].Y,
                $"{ordered[i]} overlaps {ordered[i + 1]}");
        }
    }

    [Fact]
    public void Create_UsesBalancedColumnsAndSafeValueInsets()
    {
        PayslipLayout layout = PayslipLayoutFactory.Create(11, 5);

        Assert.Equal(layout.IncomeColumn.Width, layout.DeductionColumn.Width, precision: 6);
        Assert.True(layout.IncomeValueRight <= layout.MiddleDividerX - 12);
        Assert.True(layout.DeductionValueRight <= layout.Frame.Right - 12);
    }

    [Fact]
    public void Create_ConnectsStampToAuthorizationWithoutSignatureOverlap()
    {
        PayslipLayout layout = PayslipLayoutFactory.Create(11, 5);

        Assert.True(layout.Authorization.Contains(layout.StampArea));
        Assert.False(layout.StampArea.Intersects(layout.MadeByArea));
        Assert.False(layout.StampArea.Intersects(layout.AccArea));
    }

    [Fact]
    public void Create_ProvidesRuleToTextClearanceForTotals()
    {
        PayslipLayout layout = PayslipLayoutFactory.Create(11, 5);

        Assert.True(layout.TotalTextBaseline - layout.TotalTopRuleY >= layout.MinimumRuleTextGap);
        Assert.True(layout.TotalBottomRuleY - layout.TotalTextBaseline >= layout.MinimumRuleTextGap);
    }
}
