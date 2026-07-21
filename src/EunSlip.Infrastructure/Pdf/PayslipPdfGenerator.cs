using EunSlip.Core.Payroll;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace EunSlip.Infrastructure.Pdf;

public sealed class PayslipPdfGenerator : IPayslipPdfGenerator
{
    private const double PageWidth = 595.0;
    private const double PageHeight = 842.0;
    private const double OuterMargin = 30.0;
    private const double LeftX = 44.0;
    private const double LeftValueX = 100.0;
    private const double RightLabelX = 320.0;
    private const double RightValueX = 390.0;
    private const double IncomeAmountRightX = 270.0;
    private const double DeductionAmountRightX = 545.0;
    private const double LineHeight = 11.5;
    private static readonly XRect StampArea = new(455, 322, 100, 58);

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

            using (XGraphics graphics = XGraphics.FromPdfPage(page))
            {
                DrawContent(graphics, request);
                DrawStamp(graphics, request.StampImagePath!);
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

    private static void DrawContent(XGraphics graphics, PayslipRequest request)
    {
        XFont headerFont = new("Arial", 11, XFontStyleEx.Bold);
        XFont labelFont = new("Arial", 8.5, XFontStyleEx.Regular);
        XFont boldLabelFont = new("Arial", 8.5, XFontStyleEx.Bold);
        XFont sectionFont = new("Arial", 9, XFontStyleEx.Bold);
        XFont totalFont = new("Arial", 9, XFontStyleEx.Bold);
        XFont nettFont = new("Arial", 10, XFontStyleEx.Bold);
        XPen outerPen = new(XColors.Black, 1.2);
        XPen linePen = new(XColors.Black, 0.8);

        graphics.DrawRectangle(outerPen,
            OuterMargin, OuterMargin, PageWidth - (2 * OuterMargin), PageHeight - (2 * OuterMargin));

        string[] header = PayslipContent.HeaderLines(request);
        graphics.DrawString(header[0], headerFont, XBrushes.Black,
            new XRect(0, 48, PageWidth, 14), XStringFormats.TopCenter);
        graphics.DrawString(header[1], headerFont, XBrushes.Black,
            new XRect(0, 62, PageWidth, 14), XStringFormats.TopCenter);
        graphics.DrawLine(linePen, LeftX, 80, PageWidth - LeftX, 80);

        (string Label, string Value)[] identity = PayslipContent.Identity(request);
        double identityY = 90;
        for (int i = 0; i < 2; i++)
        {
            graphics.DrawString(identity[i].Label, boldLabelFont, XBrushes.Black, LeftX, identityY + (i * LineHeight));
            graphics.DrawString(identity[i].Value, labelFont, XBrushes.Black, LeftValueX, identityY + (i * LineHeight));
        }
        for (int i = 2; i < identity.Length; i++)
        {
            int row = i - 2;
            graphics.DrawString(identity[i].Label, boldLabelFont, XBrushes.Black, RightLabelX, identityY + (row * LineHeight));
            graphics.DrawString(identity[i].Value, labelFont, XBrushes.Black, RightValueX, identityY + (row * LineHeight));
        }

        double tableTop = 140;
        graphics.DrawString("INCOME", sectionFont, XBrushes.Black, LeftX, tableTop);
        graphics.DrawString($"( {PayslipContent.OtHoursText(request)} )", labelFont, XBrushes.Black,
            new XRect(LeftX, tableTop, IncomeAmountRightX - LeftX, 11), XStringFormats.TopRight);
        graphics.DrawString("Deduction", sectionFont, XBrushes.Black, RightLabelX, tableTop);
        graphics.DrawLine(linePen, LeftX, tableTop + 13, PageWidth - LeftX, tableTop + 13);

        (string Label, string Value)[] income = PayslipContent.IncomeRows(request);
        (string Label, string Value)[] deduction = PayslipContent.DeductionRows(request);
        double rowY = tableTop + 18;
        for (int i = 0; i < income.Length; i++)
        {
            double y = rowY + (i * LineHeight);
            graphics.DrawString(income[i].Label, labelFont, XBrushes.Black, LeftX, y);
            graphics.DrawString(income[i].Value, labelFont, XBrushes.Black,
                new XRect(LeftX, y, IncomeAmountRightX - LeftX, LineHeight), XStringFormats.TopRight);
        }
        for (int i = 0; i < deduction.Length; i++)
        {
            double y = rowY + (i * LineHeight);
            graphics.DrawString(deduction[i].Label, labelFont, XBrushes.Black, RightLabelX, y);
            graphics.DrawString(deduction[i].Value, labelFont, XBrushes.Black,
                new XRect(RightLabelX, y, DeductionAmountRightX - RightLabelX, LineHeight), XStringFormats.TopRight);
        }

        double totalY = rowY + (income.Length * LineHeight) + 8;
        graphics.DrawLine(linePen, LeftX, totalY - 4, PageWidth - LeftX, totalY - 4);
        graphics.DrawString("Total", totalFont, XBrushes.Black, LeftX, totalY);
        graphics.DrawString(PayslipContent.IncomeTotal(request), totalFont, XBrushes.Black,
            new XRect(LeftX, totalY, IncomeAmountRightX - LeftX, 12), XStringFormats.TopRight);
        graphics.DrawString("Total", totalFont, XBrushes.Black, RightLabelX, totalY);
        graphics.DrawString(PayslipContent.DeductionTotal(request), totalFont, XBrushes.Black,
            new XRect(RightLabelX, totalY, DeductionAmountRightX - RightLabelX, 12), XStringFormats.TopRight);

        double nettY = totalY + 20;
        graphics.DrawRectangle(linePen, LeftX - 4, nettY - 3, PageWidth - (2 * LeftX) + 8, 18);
        graphics.DrawString("Nett Income", nettFont, XBrushes.Black, LeftX, nettY);
        graphics.DrawString(PayslipContent.NettIncome(request), nettFont, XBrushes.Black,
            new XRect(LeftX, nettY, DeductionAmountRightX - LeftX, 14), XStringFormats.TopRight);

        double footerY = nettY + 30;
        string paymentDate = PayslipContent.PaymentDateText(request);
        graphics.DrawString(paymentDate, sectionFont, XBrushes.Black, LeftX, footerY);
        double paymentWidth = graphics.MeasureString(paymentDate, sectionFont).Width;
        graphics.DrawLine(linePen, LeftX, footerY + 11, LeftX + paymentWidth, footerY + 11);

        graphics.DrawString(PayslipContent.MadeByText, sectionFont, XBrushes.Black,
            new XRect(0, footerY + 10, PageWidth, 12), XStringFormats.TopCenter);
        graphics.DrawString(PayslipContent.AccText, sectionFont, XBrushes.Black, 520, footerY + 10);
    }

    private static void DrawStamp(XGraphics graphics, string stampImagePath)
    {
        try
        {
            using XImage image = XImage.FromFile(stampImagePath);
            double scale = Math.Min(StampArea.Width / image.PixelWidth, StampArea.Height / image.PixelHeight);
            double width = image.PixelWidth * scale;
            double height = image.PixelHeight * scale;
            double x = StampArea.X + ((StampArea.Width - width) / 2);
            double y = StampArea.Y + ((StampArea.Height - height) / 2);
            graphics.DrawImage(image, x, y, width, height);
        }
        catch (Exception ex)
        {
            throw new PayslipGenerationException("Company stamp is unreadable.", ex);
        }
    }
}
