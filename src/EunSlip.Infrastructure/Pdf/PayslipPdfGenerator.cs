using EunSlip.Core.Payroll;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace EunSlip.Infrastructure.Pdf;

public sealed class PayslipPdfGenerator : IPayslipPdfGenerator
{
    private const double TextInset = 8;
    private const double IdentityValueOffset = 76;

    static PayslipPdfGenerator()
    {
        if (GlobalFontSettings.FontResolver is not WindowsFontResolver)
        {
            GlobalFontSettings.FontResolver = new WindowsFontResolver();
        }
    }

    public void Generate(PayslipRequest request, string outputPath)
    {
        if (string.IsNullOrEmpty(request.StampImagePath) || !File.Exists(request.StampImagePath))
        {
            throw new PayslipGenerationException("Company stamp is missing.");
        }

        try
        {
            using PdfDocument document = new();
            PdfPage page = document.AddPage();
            page.Width = new XUnitPt(PayslipLayoutFactory.PageWidth);
            page.Height = new XUnitPt(PayslipLayoutFactory.PageHeight);

            using (XGraphics graphics = XGraphics.FromPdfPage(page))
            {
                DrawPage(graphics, request);
            }

            document.Save(outputPath);
        }
        catch (PayslipGenerationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PayslipGenerationException("Failed to generate payslip PDF.", ex);
        }
    }

    private static void DrawPage(XGraphics graphics, PayslipRequest request)
    {
        XFont companyFont = new("Arial", 13, XFontStyleEx.Bold);
        XFont salaryFont = new("Arial", 11, XFontStyleEx.Bold);
        XFont labelFont = new("Arial", 8.5, XFontStyleEx.Regular);
        XFont labelBold = new("Arial", 8.5, XFontStyleEx.Bold);
        XFont sectionFont = new("Arial", 9, XFontStyleEx.Bold);
        XFont totalFont = new("Arial", 9, XFontStyleEx.Bold);
        XFont nettFont = new("Arial", 11, XFontStyleEx.Bold);
        XPen pen = new(XColors.Black, 0.75);

        (string Label, string Value)[] income = PayslipContent.IncomeRows(request);
        (string Label, string Value)[] deduction = PayslipContent.DeductionRows(request);
        PayslipLayout layout = PayslipLayoutFactory.Create(income.Length, deduction.Length);

        graphics.DrawRectangle(pen, layout.Frame.X, layout.Frame.Y, layout.Frame.Width, layout.Frame.Height);
        DrawHeader(graphics, request, layout, companyFont, salaryFont, pen);
        DrawIdentity(graphics, request, layout, labelFont, labelBold);
        DrawTable(graphics, request, income, deduction, layout, labelFont, sectionFont, totalFont, pen);
        DrawNettIncome(graphics, request, layout, nettFont);
        DrawMetadata(graphics, request, layout, sectionFont);
        DrawAuthorization(graphics, request.StampImagePath!, layout, sectionFont, pen);
    }

    private static void DrawHeader(
        XGraphics graphics,
        PayslipRequest request,
        PayslipLayout layout,
        XFont companyFont,
        XFont salaryFont,
        XPen pen)
    {
        string[] lines = PayslipContent.HeaderLines(request);
        double lineHeight = layout.Header.Height / 2;
        graphics.DrawString(
            lines[0],
            companyFont,
            XBrushes.Black,
            new XRect(layout.Header.X, layout.Header.Y, layout.Header.Width, lineHeight),
            XStringFormats.Center);
        graphics.DrawString(
            lines[1],
            salaryFont,
            XBrushes.Black,
            new XRect(layout.Header.X, layout.Header.Y + lineHeight, layout.Header.Width, lineHeight),
            XStringFormats.Center);
        graphics.DrawLine(pen, layout.Header.X, layout.Header.Bottom, layout.Header.Right, layout.Header.Bottom);
    }

    private static void DrawIdentity(
        XGraphics graphics,
        PayslipRequest request,
        PayslipLayout layout,
        XFont valueFont,
        XFont labelFont)
    {
        (string Label, string Value)[] rows = PayslipContent.Identity(request);
        double columnGap = 12;
        PdfRect left = new(
            layout.Identity.X,
            layout.Identity.Y,
            layout.MiddleDividerX - columnGap - layout.Identity.X,
            layout.Identity.Height);
        PdfRect right = new(
            layout.MiddleDividerX + columnGap,
            layout.Identity.Y,
            layout.Identity.Right - layout.MiddleDividerX - columnGap,
            layout.Identity.Height);

        DrawIdentityColumn(graphics, rows.AsSpan(0, 2), left, valueFont, labelFont);
        DrawIdentityColumn(graphics, rows.AsSpan(2), right, valueFont, labelFont);
    }

    private static void DrawIdentityColumn(
        XGraphics graphics,
        ReadOnlySpan<(string Label, string Value)> rows,
        PdfRect area,
        XFont valueFont,
        XFont labelFont)
    {
        double rowHeight = area.Height / rows.Length;
        for (int index = 0; index < rows.Length; index++)
        {
            double y = area.Y + (index * rowHeight);
            graphics.DrawString(
                rows[index].Label,
                labelFont,
                XBrushes.Black,
                new XRect(area.X, y, IdentityValueOffset - TextInset, rowHeight),
                XStringFormats.CenterLeft);
            graphics.DrawString(
                rows[index].Value,
                valueFont,
                XBrushes.Black,
                new XRect(area.X + IdentityValueOffset, y, area.Width - IdentityValueOffset, rowHeight),
                XStringFormats.CenterLeft);
        }
    }

    private static void DrawTable(
        XGraphics graphics,
        PayslipRequest request,
        (string Label, string Value)[] income,
        (string Label, string Value)[] deduction,
        PayslipLayout layout,
        XFont rowFont,
        XFont sectionFont,
        XFont totalFont,
        XPen pen)
    {
        graphics.DrawString(
            "INCOME",
            sectionFont,
            XBrushes.Black,
            new XRect(
                layout.IncomeColumn.X + TextInset,
                layout.TableHeader.Y,
                layout.IncomeColumn.Width - (2 * TextInset),
                layout.TableHeader.Height),
            XStringFormats.CenterLeft);
        graphics.DrawString(
            "DEDUCTION",
            sectionFont,
            XBrushes.Black,
            new XRect(
                layout.DeductionColumn.X + TextInset,
                layout.TableHeader.Y,
                layout.DeductionColumn.Width - (2 * TextInset),
                layout.TableHeader.Height),
            XStringFormats.CenterLeft);
        graphics.DrawLine(
            pen,
            layout.TableHeader.X,
            layout.TableHeader.Bottom,
            layout.TableHeader.Right,
            layout.TableHeader.Bottom);

        int rowCount = Math.Max(income.Length, deduction.Length);
        for (int index = 0; index < rowCount; index++)
        {
            double y = layout.TableBody.Y + (index * layout.RowHeight);
            if (index < income.Length)
            {
                DrawTableCell(
                    graphics,
                    income[index],
                    layout.IncomeColumn,
                    layout.IncomeValueRight,
                    y,
                    layout.RowHeight,
                    rowFont);
            }

            if (index < deduction.Length)
            {
                DrawTableCell(
                    graphics,
                    deduction[index],
                    layout.DeductionColumn,
                    layout.DeductionValueRight,
                    y,
                    layout.RowHeight,
                    rowFont);
            }
        }

        graphics.DrawLine(
            pen,
            layout.MiddleDividerX,
            layout.TableHeader.Y,
            layout.MiddleDividerX,
            layout.Totals.Bottom);
        DrawTotals(graphics, request, layout, totalFont, pen);
    }

    private static void DrawTableCell(
        XGraphics graphics,
        (string Label, string Value) row,
        PdfRect column,
        double valueRight,
        double y,
        double height,
        XFont font)
    {
        const double valueWidth = 92;
        graphics.DrawString(
            row.Label,
            font,
            XBrushes.Black,
            new XRect(
                column.X + TextInset,
                y,
                column.Width - valueWidth - (2 * TextInset),
                height),
            XStringFormats.CenterLeft);
        graphics.DrawString(
            row.Value,
            font,
            XBrushes.Black,
            new XRect(valueRight - valueWidth, y, valueWidth, height),
            XStringFormats.CenterRight);
    }

    private static void DrawTotals(
        XGraphics graphics,
        PayslipRequest request,
        PayslipLayout layout,
        XFont totalFont,
        XPen pen)
    {
        graphics.DrawLine(pen, layout.Totals.X, layout.TotalTopRuleY, layout.Totals.Right, layout.TotalTopRuleY);
        graphics.DrawString(
            "Total",
            totalFont,
            XBrushes.Black,
            layout.IncomeColumn.X + TextInset,
            layout.TotalTextBaseline);
        graphics.DrawString(
            PayslipContent.IncomeTotal(request),
            totalFont,
            XBrushes.Black,
            new XRect(
                layout.IncomeColumn.X + TextInset,
                layout.Totals.Y + 6,
                layout.IncomeValueRight - layout.IncomeColumn.X - TextInset,
                16),
            XStringFormats.TopRight);
        graphics.DrawString(
            "Total",
            totalFont,
            XBrushes.Black,
            layout.DeductionColumn.X + TextInset,
            layout.TotalTextBaseline);
        graphics.DrawString(
            PayslipContent.DeductionTotal(request),
            totalFont,
            XBrushes.Black,
            new XRect(
                layout.DeductionColumn.X + TextInset,
                layout.Totals.Y + 6,
                layout.DeductionValueRight - layout.DeductionColumn.X - TextInset,
                16),
            XStringFormats.TopRight);
        graphics.DrawLine(
            pen,
            layout.Totals.X,
            layout.TotalBottomRuleY,
            layout.Totals.Right,
            layout.TotalBottomRuleY);
    }

    private static void DrawNettIncome(
        XGraphics graphics,
        PayslipRequest request,
        PayslipLayout layout,
        XFont font)
    {
        graphics.DrawRectangle(
            new XPen(XColors.Black, 1),
            layout.NettIncome.X,
            layout.NettIncome.Y,
            layout.NettIncome.Width,
            layout.NettIncome.Height);
        graphics.DrawString(
            "NETT INCOME",
            font,
            XBrushes.Black,
            new XRect(
                layout.NettIncome.X + TextInset,
                layout.NettIncome.Y,
                layout.NettIncome.Width / 2,
                layout.NettIncome.Height),
            XStringFormats.CenterLeft);
        graphics.DrawString(
            PayslipContent.NettIncome(request),
            font,
            XBrushes.Black,
            new XRect(
                layout.NettIncome.X + (layout.NettIncome.Width / 2),
                layout.NettIncome.Y,
                (layout.NettIncome.Width / 2) - TextInset,
                layout.NettIncome.Height),
            XStringFormats.CenterRight);
    }

    private static void DrawMetadata(
        XGraphics graphics,
        PayslipRequest request,
        PayslipLayout layout,
        XFont font)
    {
        double half = layout.Metadata.Width / 2;
        graphics.DrawString(
            PayslipContent.OtHoursText(request),
            font,
            XBrushes.Black,
            new XRect(layout.Metadata.X, layout.Metadata.Y, half, layout.Metadata.Height),
            XStringFormats.CenterLeft);
        graphics.DrawString(
            $"Payment Date : {PayslipContent.PaymentDateText(request)}",
            font,
            XBrushes.Black,
            new XRect(layout.Metadata.X + half, layout.Metadata.Y, half, layout.Metadata.Height),
            XStringFormats.CenterLeft);
    }

    private static void DrawAuthorization(
        XGraphics graphics,
        string stampPath,
        PayslipLayout layout,
        XFont labelFont,
        XPen pen)
    {
        graphics.DrawString(PayslipContent.MadeByText, labelFont, XBrushes.Black, layout.MadeByArea.X, layout.MadeByArea.Y);
        graphics.DrawString(PayslipContent.AccText, labelFont, XBrushes.Black, layout.AccArea.X, layout.AccArea.Y);
        graphics.DrawLine(
            pen,
            layout.MadeByArea.X,
            layout.MadeByArea.Bottom,
            layout.MadeByArea.Right,
            layout.MadeByArea.Bottom);
        graphics.DrawLine(
            pen,
            layout.AccArea.X,
            layout.AccArea.Bottom,
            layout.AccArea.Right,
            layout.AccArea.Bottom);

        try
        {
            using XImage image = XImage.FromFile(stampPath);
            double scale = Math.Min(
                layout.StampArea.Width / image.PixelWidth,
                layout.StampArea.Height / image.PixelHeight);
            double width = image.PixelWidth * scale;
            double height = image.PixelHeight * scale;
            double x = layout.StampArea.X + ((layout.StampArea.Width - width) / 2);
            double y = layout.StampArea.Y + ((layout.StampArea.Height - height) / 2);
            graphics.DrawImage(image, x, y, width, height);
        }
        catch (Exception ex)
        {
            throw new PayslipGenerationException("Company stamp is unreadable.", ex);
        }
    }
}
