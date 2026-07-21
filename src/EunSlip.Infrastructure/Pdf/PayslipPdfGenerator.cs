using EunSlip.Core.Payroll;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace EunSlip.Infrastructure.Pdf;

public sealed class PayslipPdfGenerator : IPayslipPdfGenerator
{
    private const double PageWidth = 595.0;
    private const double PageHeight = 842.0;
    private const double Margin = 36.0;
    private const double ContentWidth = PageWidth - (2 * Margin);

    // Columns
    private const double LeftColX = Margin;
    private const double MidDividerX = Margin + (ContentWidth / 2);
    private const double RightColX = MidDividerX;
    private const double ValueRightIncome = MidDividerX - 8;
    private const double ValueRightDeduction = PageWidth - Margin;
    private const double LineHeight = 13.0;

    private static readonly XRect StampArea = new(PageWidth - Margin - 90, 540, 80, 50);

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
            page.Width = new XUnitPt(PageWidth);
            page.Height = new XUnitPt(PageHeight);

            using (XGraphics g = XGraphics.FromPdfPage(page))
            {
                DrawPage(g, request);
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

    private static void DrawPage(XGraphics g, PayslipRequest request)
    {
        XFont companyFont = new("Arial", 13, XFontStyleEx.Bold);
        XFont salaryFont = new("Arial", 11, XFontStyleEx.Bold);
        XFont labelFont = new("Arial", 8.5, XFontStyleEx.Regular);
        XFont labelBold = new("Arial", 8.5, XFontStyleEx.Bold);
        XFont sectionFont = new("Arial", 9, XFontStyleEx.Bold);
        XFont totalFont = new("Arial", 9, XFontStyleEx.Bold);
        XFont nettFont = new("Arial", 11, XFontStyleEx.Bold);
        XPen pen = new(XColors.Black, 0.75);

        // Outer border
        g.DrawRectangle(pen, Margin, Margin, ContentWidth, PageHeight - (2 * Margin));

        // Header band
        double headerY = Margin + 14;
        string[] header = PayslipContent.HeaderLines(request);
        g.DrawString(header[0], companyFont, XBrushes.Black,
            new XRect(Margin, headerY, ContentWidth, 16), XStringFormats.Center);
        g.DrawString(header[1], salaryFont, XBrushes.Black,
            new XRect(Margin, headerY + 18, ContentWidth, 14), XStringFormats.Center);
        double headerBottom = headerY + 38;
        g.DrawLine(pen, Margin + 10, headerBottom, PageWidth - Margin - 10, headerBottom);

        // Identity block
        (string Label, string Value)[] identity = PayslipContent.Identity(request);
        double idY = headerBottom + 12;
        for (int i = 0; i < 2; i++)
        {
            double y = idY + (i * LineHeight);
            g.DrawString(identity[i].Label, labelBold, XBrushes.Black, LeftColX + 4, y);
            g.DrawString(identity[i].Value, labelFont, XBrushes.Black, LeftColX + 70, y);
        }
        for (int i = 2; i < identity.Length; i++)
        {
            int row = i - 2;
            double y = idY + (row * LineHeight);
            g.DrawString(identity[i].Label, labelBold, XBrushes.Black, RightColX + 4, y);
            g.DrawString(identity[i].Value, labelFont, XBrushes.Black, RightColX + 70, y);
        }

        // Table
        double tableTop = idY + (4 * LineHeight) + 10;
        double tableBottom = DrawTable(g, request, tableTop, pen, labelFont, sectionFont, totalFont);

        // Nett income box
        double nettY = tableBottom + 12;
        g.DrawRectangle(new XPen(XColors.Black, 1), Margin + 4, nettY, ContentWidth - 8, 22);
        g.DrawString("NETT INCOME", nettFont, XBrushes.Black, Margin + 12, nettY + 6);
        g.DrawString(PayslipContent.NettIncome(request), nettFont, XBrushes.Black,
            new XRect(Margin + 4, nettY + 4, ContentWidth - 16, 16), XStringFormats.TopRight);

        // Footer
        double footerY = nettY + 44;
        g.DrawString($"OT Hours : {PayslipContent.OtHoursText(request).Replace("OT Hours : ", "")}",
            sectionFont, XBrushes.Black, LeftColX + 4, footerY);
        g.DrawString($"Payment Date : {PayslipContent.PaymentDateText(request)}",
            sectionFont, XBrushes.Black, RightColX + 4, footerY);

        // Signature row
        double sigY = footerY + 36;
        g.DrawString("Made By", sectionFont, XBrushes.Black, LeftColX + 4, sigY);
        g.DrawString("ACC", sectionFont, XBrushes.Black, RightColX + 4, sigY);
        g.DrawLine(pen, LeftColX + 4, sigY + 28, LeftColX + 110, sigY + 28);
        g.DrawLine(pen, RightColX + 4, sigY + 28, RightColX + 110, sigY + 28);

        DrawStamp(g, request.StampImagePath!);
    }

    private static double DrawTable(XGraphics g, PayslipRequest request, double top, XPen pen,
        XFont labelFont, XFont sectionFont, XFont totalFont)
    {
        (string Label, string Value)[] income = PayslipContent.IncomeRows(request);
        (string Label, string Value)[] deduction = PayslipContent.DeductionRows(request);
        int maxRows = Math.Max(income.Length, deduction.Length);

        // Section headers
        g.DrawString("INCOME", sectionFont, XBrushes.Black, LeftColX + 4, top);
        g.DrawString("DEDUCTION", sectionFont, XBrushes.Black, RightColX + 4, top);
        double headerLine = top + 14;
        g.DrawLine(pen, Margin, headerLine, PageWidth - Margin, headerLine);

        double rowY = headerLine + 4;
        for (int i = 0; i < maxRows; i++)
        {
            double y = rowY + (i * LineHeight) + LineHeight;
            if (i < income.Length)
            {
                g.DrawString(income[i].Label, labelFont, XBrushes.Black, LeftColX + 4, y);
                g.DrawString(income[i].Value, labelFont, XBrushes.Black,
                    new XRect(LeftColX + 4, y - 2, ValueRightIncome - LeftColX - 4, LineHeight), XStringFormats.TopRight);
            }
            if (i < deduction.Length)
            {
                g.DrawString(deduction[i].Label, labelFont, XBrushes.Black, RightColX + 4, y);
                g.DrawString(deduction[i].Value, labelFont, XBrushes.Black,
                    new XRect(RightColX + 4, y - 2, ValueRightDeduction - RightColX - 4, LineHeight), XStringFormats.TopRight);
            }
        }

        // Totals row
        double totalY = rowY + (maxRows * LineHeight) + LineHeight + 6;
        g.DrawLine(pen, Margin, totalY, PageWidth - Margin, totalY);
        g.DrawString("Total", totalFont, XBrushes.Black, LeftColX + 4, totalY + 4);
        g.DrawString(PayslipContent.IncomeTotal(request), totalFont, XBrushes.Black,
            new XRect(LeftColX + 4, totalY + 2, ValueRightIncome - LeftColX - 4, LineHeight), XStringFormats.TopRight);
        g.DrawString("Total", totalFont, XBrushes.Black, RightColX + 4, totalY + 4);
        g.DrawString(PayslipContent.DeductionTotal(request), totalFont, XBrushes.Black,
            new XRect(RightColX + 4, totalY + 2, ValueRightDeduction - RightColX - 4, LineHeight), XStringFormats.TopRight);
        g.DrawLine(pen, Margin, totalY + 18, PageWidth - Margin, totalY + 18);

        // Mid vertical divider
        g.DrawLine(pen, MidDividerX, headerLine, MidDividerX, totalY + 18);

        return totalY + 18;
    }

    private static void DrawStamp(XGraphics g, string stampImagePath)
    {
        try
        {
            using XImage image = XImage.FromFile(stampImagePath);
            double scale = Math.Min(StampArea.Width / image.PixelWidth, StampArea.Height / image.PixelHeight);
            double w = image.PixelWidth * scale;
            double h = image.PixelHeight * scale;
            double x = StampArea.X + ((StampArea.Width - w) / 2);
            double y = StampArea.Y + ((StampArea.Height - h) / 2);
            g.DrawImage(image, x, y, w, h);
        }
        catch (Exception ex)
        {
            throw new PayslipGenerationException("Company stamp is unreadable.", ex);
        }
    }
}
