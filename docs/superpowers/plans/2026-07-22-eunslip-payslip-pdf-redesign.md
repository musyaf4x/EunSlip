# EunSlip Payslip PDF Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the A4 payslip layout around testable geometry so totals, values, borders, signatures, and stamp placement are clean and faithful to the supplied reference.

**Architecture:** Separate immutable page geometry from PDFsharp drawing. `PayslipLayoutFactory` owns rectangles, baselines, value insets, and collision rules; `PayslipPdfGenerator` consumes that layout and the unchanged `PayslipContent` data mapping. Geometry tests catch overlap regressions without relying on fragile pixel snapshots, while a final raster inspection validates the visual result.

**Tech Stack:** .NET 10, PDFsharp, xUnit 2.9.3, Poppler `pdftoppm` available at `C:\Users\hafid\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\override\pdftoppm.cmd`.

## Global Constraints

- Execute after `2026-07-22-eunslip-workflow-history-redesign.md` is complete and green.
- Output remains A4 portrait, exactly one page, with the company stamp required.
- Preserve all payroll values and labels currently provided by `PayslipContent`; do not recalculate totals.
- Keep the full income and deduction component lists, including zero values.
- Use a PDFsharp-compatible neutral sans font and preserve the existing Windows font resolver.
- No text baseline may intersect a rule; no value may touch the middle divider or page frame.
- Stamp must preserve aspect ratio and remain inside its authorization rectangle.
- Layout tests must not depend on an installed PDF viewer or screen resolution.
- Do not add a new PDF library, image library, or golden-image dependency.

## File Map

### Create

- `src/EunSlip.Infrastructure/Pdf/PayslipLayout.cs` — immutable rectangles, safe insets, and layout factory.
- `src/EunSlip.Infrastructure/Properties/AssemblyInfo.cs` — test-only internal visibility for geometry assertions.
- `tests/EunSlip.Infrastructure.Tests/Pdf/PayslipLayoutTests.cs` — bounds, non-intersection, symmetry, inset, and stamp tests.

### Modify

- `src/EunSlip.Infrastructure/Pdf/PayslipPdfGenerator.cs` — consume layout model and redraw all document regions.
- `tests/EunSlip.Infrastructure.Tests/Pdf/PayslipPdfGeneratorTests.cs` — one-page, stamp, long-value, and evidence-output regressions.
- `tests/EunSlip.Infrastructure.Tests/Pdf/PayslipContentTests.cs` — retain all mapping and formatting assertions.

---

### Task 1: Define and Test Collision-Free Page Geometry

**Files:**
- Create: `src/EunSlip.Infrastructure/Pdf/PayslipLayout.cs`
- Create: `src/EunSlip.Infrastructure/Properties/AssemblyInfo.cs`
- Create: `tests/EunSlip.Infrastructure.Tests/Pdf/PayslipLayoutTests.cs`

**Interfaces:**
- Consumes: income/deduction row counts from `PayslipContent`.
- Produces: `PdfRect`, `PayslipLayout`, and `PayslipLayoutFactory.Create(int incomeRowCount, int deductionRowCount)` for the generator.

- [ ] **Step 1: Write failing layout geometry tests**

```csharp
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
```

- [ ] **Step 2: Run the layout tests and verify the model is absent**

Run:

```powershell
dotnet test tests/EunSlip.Infrastructure.Tests/EunSlip.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~PayslipLayoutTests
```

Expected: build FAIL because the three layout types do not exist.

- [ ] **Step 3: Add internal test visibility**

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("EunSlip.Infrastructure.Tests")]
```

- [ ] **Step 4: Implement immutable geometry**

```csharp
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
        Header, Identity, TableHeader, TableBody, Totals, NettIncome, Metadata, Authorization,
        IncomeColumn, DeductionColumn, MadeByArea, StampArea, AccArea,
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
            PageWidth, PageHeight, frame, header, identity, tableHeader, tableBody, totals,
            nettIncome, metadata, authorization, incomeColumn, deductionColumn,
            madeBy, stamp, acc, middle, middle - 12, frame.Right - 12, RowHeight,
            totalTopRule, totalBaseline, totalBottomRule, MinimumRuleTextGap);
    }
}
```

- [ ] **Step 5: Run geometry tests and Infrastructure build**

Run:

```powershell
dotnet test tests/EunSlip.Infrastructure.Tests/EunSlip.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~PayslipLayoutTests
dotnet build src/EunSlip.Infrastructure/EunSlip.Infrastructure.csproj --no-restore
```

Expected: tests PASS; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 6: Commit the layout model**

```powershell
git add src/EunSlip.Infrastructure/Pdf/PayslipLayout.cs src/EunSlip.Infrastructure/Properties/AssemblyInfo.cs tests/EunSlip.Infrastructure.Tests/Pdf/PayslipLayoutTests.cs
git commit -m "test: define payslip layout geometry"
```

---

### Task 2: Redraw the Payslip from the Layout Model

**Files:**
- Modify: `src/EunSlip.Infrastructure/Pdf/PayslipPdfGenerator.cs`
- Modify: `tests/EunSlip.Infrastructure.Tests/Pdf/PayslipPdfGeneratorTests.cs`

**Interfaces:**
- Consumes: `PayslipLayoutFactory.Create`, `PayslipContent`, PDFsharp fonts/images, and the required stamp path.
- Produces: a one-page PDF whose drawing calls use layout rectangles rather than unrelated hard-coded coordinates.

- [ ] **Step 1: Add a long-content generator regression**

```csharp
[Fact]
public void Generate_LongIdentityAndNegativeValues_RemainsOnePageWithStamp()
{
    PayslipRequest request = Request() with
    {
        Row = Request().Row with
        {
            Nama = "NAMA KARYAWAN DENGAN IDENTITAS PROFESIONAL YANG PANJANG",
            Departement = "HUMAN RESOURCES AND GENERAL AFFAIRS",
            Position = "SENIOR OPERATIONAL PAYROLL MANAGER",
            Koreksi = -99_999_999L,
            Total = 999_999_999L,
            Nett = 888_888_888L,
        },
    };
    string output = Path.Combine(_directory, "long-values.pdf");

    new PayslipPdfGenerator().Generate(request, output);

    using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
    Assert.Equal(1, document.PageCount);
    Assert.True(document.Pages[0].Resources.Elements.ContainsKey("/XObject"));
}
```

- [ ] **Step 2: Run current PDF tests as the pre-refactor baseline**

Run:

```powershell
dotnet test tests/EunSlip.Infrastructure.Tests/EunSlip.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~PayslipPdfGeneratorTests|FullyQualifiedName~PayslipLayoutTests"
```

Expected: existing tests PASS; the new long-value test may PASS because it establishes a non-crash invariant. The geometry tests are the RED signal for the new drawing contract, so do not introduce an artificial failure.

- [ ] **Step 3: Make `DrawPage` consume the layout**

Remove `StampArea`, `LeftColX`, `RightColX`, `ValueRightIncome`, `ValueRightDeduction`, and the old fixed footer/signature coordinates from the generator. Keep only local typography insets:

```csharp
private const double TextInset = 8;
private const double IdentityValueOffset = 76;
```

Replace `DrawPage` with the complete layout-driven composition:

```csharp
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

    (string Label, string Value)[] income = PayslipContent.IncomeRows(request);
    (string Label, string Value)[] deduction = PayslipContent.DeductionRows(request);
    PayslipLayout layout = PayslipLayoutFactory.Create(income.Length, deduction.Length);

    g.DrawRectangle(pen, layout.Frame.X, layout.Frame.Y, layout.Frame.Width, layout.Frame.Height);
    DrawHeader(g, request, layout, companyFont, salaryFont, pen);
    DrawIdentity(g, request, layout, labelFont, labelBold);
    DrawTable(g, request, income, deduction, layout, labelFont, sectionFont, totalFont, pen);
    DrawNettIncome(g, request, layout, nettFont, pen);
    DrawMetadata(g, request, layout, sectionFont);
    DrawAuthorization(g, request.StampImagePath!, layout, sectionFont, pen);
}

private static void DrawHeader(XGraphics g, PayslipRequest request, PayslipLayout layout,
    XFont companyFont, XFont salaryFont, XPen pen)
{
    string[] lines = PayslipContent.HeaderLines(request);
    double lineHeight = layout.Header.Height / 2;
    g.DrawString(lines[0], companyFont, XBrushes.Black,
        new XRect(layout.Header.X, layout.Header.Y, layout.Header.Width, lineHeight),
        XStringFormats.Center);
    g.DrawString(lines[1], salaryFont, XBrushes.Black,
        new XRect(layout.Header.X, layout.Header.Y + lineHeight, layout.Header.Width, lineHeight),
        XStringFormats.Center);
    g.DrawLine(pen, layout.Header.X, layout.Header.Bottom,
        layout.Header.Right, layout.Header.Bottom);
}

private static void DrawIdentity(XGraphics g, PayslipRequest request, PayslipLayout layout,
    XFont valueFont, XFont labelFont)
{
    (string Label, string Value)[] rows = PayslipContent.Identity(request);
    double columnGap = 12;
    PdfRect left = new(layout.Identity.X, layout.Identity.Y,
        layout.MiddleDividerX - columnGap - layout.Identity.X, layout.Identity.Height);
    PdfRect right = new(layout.MiddleDividerX + columnGap, layout.Identity.Y,
        layout.Identity.Right - layout.MiddleDividerX - columnGap, layout.Identity.Height);

    DrawIdentityColumn(g, rows[..2], left, valueFont, labelFont);
    DrawIdentityColumn(g, rows[2..], right, valueFont, labelFont);
}

private static void DrawIdentityColumn(XGraphics g,
    ReadOnlySpan<(string Label, string Value)> rows, PdfRect area,
    XFont valueFont, XFont labelFont)
{
    double rowHeight = area.Height / rows.Length;
    for (int index = 0; index < rows.Length; index++)
    {
        double y = area.Y + (index * rowHeight);
        g.DrawString(rows[index].Label, labelFont, XBrushes.Black,
            new XRect(area.X, y, IdentityValueOffset - TextInset, rowHeight),
            XStringFormats.CenterLeft);
        g.DrawString(rows[index].Value, valueFont, XBrushes.Black,
            new XRect(area.X + IdentityValueOffset, y,
                area.Width - IdentityValueOffset, rowHeight), XStringFormats.CenterLeft);
    }
}
```

Each helper receives all page geometry through `PayslipLayout`; the two constants above are text insets, not absolute page coordinates.

- [ ] **Step 4: Draw tables, totals, nett, and metadata with explicit clearance**

Add the remaining content helpers exactly as follows:

```csharp
private static void DrawTable(XGraphics g, PayslipRequest request,
    IReadOnlyList<(string Label, string Value)> income,
    IReadOnlyList<(string Label, string Value)> deduction,
    PayslipLayout layout, XFont rowFont, XFont sectionFont, XFont totalFont, XPen pen)
{
    g.DrawString("INCOME", sectionFont, XBrushes.Black,
        new XRect(layout.IncomeColumn.X + TextInset, layout.TableHeader.Y,
            layout.IncomeColumn.Width - (2 * TextInset), layout.TableHeader.Height),
        XStringFormats.CenterLeft);
    g.DrawString("DEDUCTION", sectionFont, XBrushes.Black,
        new XRect(layout.DeductionColumn.X + TextInset, layout.TableHeader.Y,
            layout.DeductionColumn.Width - (2 * TextInset), layout.TableHeader.Height),
        XStringFormats.CenterLeft);
    g.DrawLine(pen, layout.TableHeader.X, layout.TableHeader.Bottom,
        layout.TableHeader.Right, layout.TableHeader.Bottom);

    int rowCount = Math.Max(income.Count, deduction.Count);
    for (int index = 0; index < rowCount; index++)
    {
        double y = layout.TableBody.Y + (index * layout.RowHeight);
        if (index < income.Count)
        {
            DrawTableCell(g, income[index], layout.IncomeColumn, layout.IncomeValueRight,
                y, layout.RowHeight, rowFont);
        }
        if (index < deduction.Count)
        {
            DrawTableCell(g, deduction[index], layout.DeductionColumn, layout.DeductionValueRight,
                y, layout.RowHeight, rowFont);
        }
    }

    g.DrawLine(pen, layout.MiddleDividerX, layout.TableHeader.Y,
        layout.MiddleDividerX, layout.Totals.Bottom);
    DrawTotals(g, request, layout, totalFont, pen);
}

private static void DrawTableCell(XGraphics g, (string Label, string Value) row,
    PdfRect column, double valueRight, double y, double height, XFont font)
{
    double valueWidth = 92;
    g.DrawString(row.Label, font, XBrushes.Black,
        new XRect(column.X + TextInset, y,
            column.Width - valueWidth - (2 * TextInset), height), XStringFormats.CenterLeft);
    g.DrawString(row.Value, font, XBrushes.Black,
        new XRect(valueRight - valueWidth, y, valueWidth, height), XStringFormats.CenterRight);
}

private static void DrawTotals(XGraphics g, PayslipRequest request, PayslipLayout layout,
    XFont totalFont, XPen pen)
{
    g.DrawLine(pen, layout.Totals.X, layout.TotalTopRuleY,
        layout.Totals.Right, layout.TotalTopRuleY);
    g.DrawString("Total", totalFont, XBrushes.Black,
        layout.IncomeColumn.X + TextInset, layout.TotalTextBaseline);
    g.DrawString(PayslipContent.IncomeTotal(request), totalFont, XBrushes.Black,
        new XRect(layout.IncomeColumn.X + TextInset, layout.Totals.Y + 6,
            layout.IncomeValueRight - layout.IncomeColumn.X - TextInset, 16),
        XStringFormats.TopRight);
    g.DrawString("Total", totalFont, XBrushes.Black,
        layout.DeductionColumn.X + TextInset, layout.TotalTextBaseline);
    g.DrawString(PayslipContent.DeductionTotal(request), totalFont, XBrushes.Black,
        new XRect(layout.DeductionColumn.X + TextInset, layout.Totals.Y + 6,
            layout.DeductionValueRight - layout.DeductionColumn.X - TextInset, 16),
        XStringFormats.TopRight);
    g.DrawLine(pen, layout.Totals.X, layout.TotalBottomRuleY,
        layout.Totals.Right, layout.TotalBottomRuleY);
}

private static void DrawNettIncome(XGraphics g, PayslipRequest request,
    PayslipLayout layout, XFont font, XPen pen)
{
    g.DrawRectangle(new XPen(XColors.Black, 1), layout.NettIncome.X, layout.NettIncome.Y,
        layout.NettIncome.Width, layout.NettIncome.Height);
    g.DrawString("NETT INCOME", font, XBrushes.Black,
        new XRect(layout.NettIncome.X + TextInset, layout.NettIncome.Y,
            layout.NettIncome.Width / 2, layout.NettIncome.Height), XStringFormats.CenterLeft);
    g.DrawString(PayslipContent.NettIncome(request), font, XBrushes.Black,
        new XRect(layout.NettIncome.X + (layout.NettIncome.Width / 2), layout.NettIncome.Y,
            (layout.NettIncome.Width / 2) - TextInset, layout.NettIncome.Height),
        XStringFormats.CenterRight);
}

private static void DrawMetadata(XGraphics g, PayslipRequest request,
    PayslipLayout layout, XFont font)
{
    double half = layout.Metadata.Width / 2;
    g.DrawString(PayslipContent.OtHoursText(request), font, XBrushes.Black,
        new XRect(layout.Metadata.X, layout.Metadata.Y, half, layout.Metadata.Height),
        XStringFormats.CenterLeft);
    g.DrawString($"Payment Date : {PayslipContent.PaymentDateText(request)}", font,
        XBrushes.Black,
        new XRect(layout.Metadata.X + half, layout.Metadata.Y, half, layout.Metadata.Height),
        XStringFormats.CenterLeft);
}
```

Rows use `layout.RowHeight`; values are right-aligned inside a fixed local value band and remain at least 12 points from the middle divider or outer frame.

- [ ] **Step 5: Draw the connected authorization region and proportional stamp**

```csharp
private static void DrawAuthorization(XGraphics g, string stampPath, PayslipLayout layout,
    XFont labelFont, XPen pen)
{
    g.DrawString(PayslipContent.MadeByText, labelFont, XBrushes.Black,
        layout.MadeByArea.X, layout.MadeByArea.Y);
    g.DrawString(PayslipContent.AccText, labelFont, XBrushes.Black,
        layout.AccArea.X, layout.AccArea.Y);
    g.DrawLine(pen, layout.MadeByArea.X, layout.MadeByArea.Bottom,
        layout.MadeByArea.Right, layout.MadeByArea.Bottom);
    g.DrawLine(pen, layout.AccArea.X, layout.AccArea.Bottom,
        layout.AccArea.Right, layout.AccArea.Bottom);

    try
    {
        using XImage image = XImage.FromFile(stampPath);
        double scale = Math.Min(layout.StampArea.Width / image.PixelWidth,
            layout.StampArea.Height / image.PixelHeight);
        double width = image.PixelWidth * scale;
        double height = image.PixelHeight * scale;
        double x = layout.StampArea.X + ((layout.StampArea.Width - width) / 2);
        double y = layout.StampArea.Y + ((layout.StampArea.Height - height) / 2);
        g.DrawImage(image, x, y, width, height);
    }
    catch (Exception ex)
    {
        throw new PayslipGenerationException("Company stamp is unreadable.", ex);
    }
}
```

- [ ] **Step 6: Run all PDF tests**

Run:

```powershell
dotnet test tests/EunSlip.Infrastructure.Tests/EunSlip.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~Pdf
```

Expected: all PDF layout, content, page, and stamp tests PASS; 0 failed.

- [ ] **Step 7: Commit the generator redesign**

```powershell
git add src/EunSlip.Infrastructure/Pdf tests/EunSlip.Infrastructure.Tests/Pdf
git commit -m "fix: redraw payslip with safe geometry"
```

---

### Task 3: Generate and Raster-Inspect a Representative Payslip

**Files:**
- Modify: `tests/EunSlip.Infrastructure.Tests/Pdf/PayslipPdfGeneratorTests.cs`
- Create at verification time: `artifacts/pdf/eunslip-reference-sample.pdf`
- Create at verification time: `artifacts/pdf/eunslip-reference-sample-1.png`

**Interfaces:**
- Consumes: redesigned generator, the representative payroll row, a valid stamp image, and `pdftoppm`.
- Produces: reviewable local PDF/PNG evidence; these artifacts are not production inputs.

- [ ] **Step 1: Add an opt-in evidence writer test**

```csharp
[Fact]
public void Generate_WritesReferenceEvidenceWhenPathIsProvided()
{
    string? output = Environment.GetEnvironmentVariable("EUNSLIP_PDF_EVIDENCE_PATH");
    if (string.IsNullOrWhiteSpace(output))
    {
        return;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    new PayslipPdfGenerator().Generate(Request(), output);

    Assert.True(File.Exists(output));
}
```

- [ ] **Step 2: Run the PDF suite normally**

```powershell
dotnet test tests/EunSlip.Infrastructure.Tests/EunSlip.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~Pdf
```

Expected: all tests PASS; the evidence test is side-effect free when the environment variable is absent.

- [ ] **Step 3: Generate the evidence PDF explicitly**

```powershell
$env:EUNSLIP_PDF_EVIDENCE_PATH = (Resolve-Path '.').Path + '\artifacts\pdf\eunslip-reference-sample.pdf'
dotnet test tests/EunSlip.Infrastructure.Tests/EunSlip.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~Generate_WritesReferenceEvidenceWhenPathIsProvided
Remove-Item Env:EUNSLIP_PDF_EVIDENCE_PATH
```

Expected: `artifacts/pdf/eunslip-reference-sample.pdf` exists and the test passes.

- [ ] **Step 4: Rasterize the first page**

```powershell
& 'C:\Users\hafid\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\override\pdftoppm.cmd' -png -f 1 -singlefile -r 144 'artifacts\pdf\eunslip-reference-sample.pdf' 'artifacts\pdf\eunslip-reference-sample-1'
```

Expected: `artifacts/pdf/eunslip-reference-sample-1.png` exists.

- [ ] **Step 5: Inspect against `payslip-layout-reference.png`**

Open both images with the local image viewer tool and verify:

- total labels and values sit between rules without intersection;
- deduction values retain a visible right inset;
- identity columns and income/deduction columns are balanced;
- nett income is visually dominant but contained;
- OT/payment date and authorization areas form one vertical rhythm;
- stamp is proportional and connected to Made By/ACC without overlap;
- the document uses the page intentionally and does not leave an accidental lower-right stamp island.

- [ ] **Step 6: Add a regression test before correcting any discovered defect**

For a geometry defect, add a precise assertion to `PayslipLayoutTests.cs`, run it to observe FAIL, adjust only `PayslipLayoutFactory` or the drawing method that consumes that rectangle, and rerun the complete PDF suite plus rasterization.

- [ ] **Step 7: Commit tests and reviewed evidence**

```powershell
git add tests/EunSlip.Infrastructure.Tests/Pdf/PayslipPdfGeneratorTests.cs artifacts/pdf/eunslip-reference-sample.pdf artifacts/pdf/eunslip-reference-sample-1.png
git commit -m "test: verify redesigned payslip rendering"
```

---

### Task 4: PDF Phase Checkpoint

**Files:**
- Verify only.

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces: a green PDF subsystem and review evidence ready for final application integration.

- [ ] **Step 1: Run all Infrastructure tests**

```powershell
dotnet test tests/EunSlip.Infrastructure.Tests/EunSlip.Infrastructure.Tests.csproj --no-restore
```

Expected: all Infrastructure tests PASS; 0 failed.

- [ ] **Step 2: Run the complete solution suite**

```powershell
dotnet test EunSlip.slnx --no-restore
```

Expected: all tests PASS; 0 failed.

- [ ] **Step 3: Build the complete solution**

```powershell
dotnet build EunSlip.slnx --no-restore
```

Expected: build succeeds with 0 warnings and 0 errors.

- [ ] **Step 4: Generate a preview through the redesigned Wizard**

Use the dummy workbook to reach Preview and click `BUKA PRATINJAU PDF`. Verify the external viewer opens the new document, while closing that viewer does not change Wizard state. Do not continue to real send.

- [ ] **Step 5: Confirm repository cleanliness**

```powershell
git status --short
git diff --check
```

Expected: no uncommitted source changes and no whitespace errors. Evidence artifacts are committed by Task 3.
