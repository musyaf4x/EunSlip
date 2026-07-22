namespace EunSlip.Infrastructure.Pdf;

internal readonly record struct PdfRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Contains(PdfRect other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    public bool Intersects(PdfRect other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}

internal sealed record PayslipLayout(
    double PageWidth,
    double PageHeight,
    PdfRect Frame,
    PdfRect Header,
    PdfRect Identity,
    PdfRect TableHeader,
    PdfRect TableBody,
    PdfRect Totals,
    PdfRect NettIncome,
    PdfRect Metadata,
    PdfRect Authorization,
    PdfRect IncomeColumn,
    PdfRect DeductionColumn,
    PdfRect MadeByArea,
    PdfRect StampArea,
    PdfRect AccArea,
    double MiddleDividerX,
    double IncomeValueRight,
    double DeductionValueRight,
    double RowHeight,
    double TotalTopRuleY,
    double TotalTextBaseline,
    double TotalBottomRuleY,
    double MinimumRuleTextGap)
{
    public IReadOnlyList<PdfRect> ContentRegions =>
    [
        Header,
        Identity,
        TableHeader,
        TableBody,
        Totals,
        NettIncome,
        Metadata,
        Authorization,
        IncomeColumn,
        DeductionColumn,
        MadeByArea,
        StampArea,
        AccArea,
    ];
}

internal static class PayslipLayoutFactory
{
    internal const double PageWidth = 595;
    internal const double PageHeight = 842;
    internal const double Margin = 36;
    internal const double RowHeight = 16;
    internal const double MinimumRuleTextGap = 6;

    public static PayslipLayout Create(int incomeRowCount, int deductionRowCount)
    {
        int rowCount = Math.Max(incomeRowCount, deductionRowCount);
        double frameWidth = PageWidth - (2 * Margin);
        PdfRect frame = new(Margin, Margin, frameWidth, PageHeight - (2 * Margin));
        double contentX = Margin + 12;
        double contentWidth = frameWidth - 24;
        double middle = Margin + (frameWidth / 2);

        PdfRect header = new(contentX, 50, contentWidth, 44);
        PdfRect identity = new(contentX, 110, contentWidth, 64);
        PdfRect tableHeader = new(Margin, 192, frameWidth, 24);
        PdfRect tableBody = new(Margin, tableHeader.Bottom, frameWidth, rowCount * RowHeight);
        PdfRect totals = new(Margin, tableBody.Bottom, frameWidth, 28);
        PdfRect nettIncome = new(contentX, totals.Bottom + 12, contentWidth, 32);
        PdfRect metadata = new(contentX, nettIncome.Bottom + 18, contentWidth, 24);
        PdfRect authorization = new(contentX, metadata.Bottom + 22, contentWidth, 160);

        PdfRect incomeColumn = new(Margin, tableHeader.Y, frameWidth / 2, totals.Bottom - tableHeader.Y);
        PdfRect deductionColumn = new(middle, tableHeader.Y, frameWidth / 2, totals.Bottom - tableHeader.Y);
        PdfRect madeBy = new(authorization.X + 12, authorization.Y + 16, 150, 110);
        PdfRect stamp = new(middle - 45, authorization.Y + 26, 90, 82);
        PdfRect acc = new(authorization.Right - 162, authorization.Y + 16, 150, 110);
        double totalTopRule = totals.Y;
        double totalBaseline = totals.Y + 15;
        double totalBottomRule = totals.Bottom;

        return new PayslipLayout(
            PageWidth,
            PageHeight,
            frame,
            header,
            identity,
            tableHeader,
            tableBody,
            totals,
            nettIncome,
            metadata,
            authorization,
            incomeColumn,
            deductionColumn,
            madeBy,
            stamp,
            acc,
            middle,
            middle - 12,
            frame.Right - 12,
            RowHeight,
            totalTopRule,
            totalBaseline,
            totalBottomRule,
            MinimumRuleTextGap);
    }
}
