using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace EunSlip.Infrastructure.Tests.Excel;

public enum TestCellKind
{
    SharedString,
    InlineString,
    Number,
    Formula,
    FormulaNoCache,
    DateSerial,
    DateIso,
    Blank,
}

public sealed record TestCell(TestCellKind Kind, string? Value = null, string? Formula = null);

public static class TestWorkbook
{
    public static string Create(TestCell[][] rows, bool addSecondSheet = false)
    {
        string path = Path.Combine(
            Path.GetTempPath(), "eunslip-tests", Guid.NewGuid().ToString("N") + ".xlsx");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (SpreadsheetDocument doc = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
        {
            WorkbookPart workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            SharedStringTablePart sharedPart = workbookPart.AddNewPart<SharedStringTablePart>();
            sharedPart.SharedStringTable = new SharedStringTable();
            Dictionary<string, uint> sharedIndex = new(StringComparer.Ordinal);

            WorksheetPart firstPart = workbookPart.AddNewPart<WorksheetPart>();
            firstPart.Worksheet = BuildWorksheet(rows, sharedPart.SharedStringTable, sharedIndex);

            Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(firstPart),
                SheetId = 1,
                Name = "Payroll",
            });

            if (addSecondSheet)
            {
                WorksheetPart secondPart = workbookPart.AddNewPart<WorksheetPart>();
                secondPart.Worksheet = BuildWorksheet(
                    [[new TestCell(TestCellKind.SharedString, "WRONG_SHEET")]],
                    sharedPart.SharedStringTable, sharedIndex);
                sheets.Append(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(secondPart),
                    SheetId = 2,
                    Name = "Other",
                });
            }

            workbookPart.Workbook.Save();
        }

        return path;
    }

    public static TestCell[] HeaderRow(params string[] headers) =>
        [.. headers.Select(h => new TestCell(TestCellKind.SharedString, h))];

    private static Worksheet BuildWorksheet(
        TestCell[][] rows, SharedStringTable table, Dictionary<string, uint> sharedIndex)
    {
        SheetData sheetData = new();
        for (int r = 0; r < rows.Length; r++)
        {
            Row row = new() { RowIndex = (uint)(r + 1) };
            for (int c = 0; c < rows[r].Length; c++)
            {
                Cell? cell = BuildCell(rows[r][c], r, c, table, sharedIndex);
                if (cell is not null)
                {
                    row.Append(cell);
                }
            }
            sheetData.Append(row);
        }
        return new Worksheet(sheetData);
    }

    private static Cell? BuildCell(
        TestCell testCell, int r, int c, SharedStringTable table, Dictionary<string, uint> sharedIndex)
    {
        string reference = $"{ColumnName(c)}{r + 1}";
        switch (testCell.Kind)
        {
            case TestCellKind.Blank:
                return null;
            case TestCellKind.SharedString:
                {
                    string text = testCell.Value ?? string.Empty;
                    if (!sharedIndex.TryGetValue(text, out uint index))
                    {
                        index = (uint)table.ChildElements.Count;
                        table.AppendChild(new SharedStringItem(new Text(text)));
                        sharedIndex[text] = index;
                    }
                    return new Cell
                    {
                        CellReference = reference,
                        DataType = CellValues.SharedString,
                        CellValue = new CellValue(index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    };
                }
            case TestCellKind.InlineString:
                return new Cell
                {
                    CellReference = reference,
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(testCell.Value ?? string.Empty)),
                };
            case TestCellKind.Number:
                return new Cell
                {
                    CellReference = reference,
                    CellValue = new CellValue(testCell.Value ?? "0"),
                };
            case TestCellKind.Formula:
                return new Cell
                {
                    CellReference = reference,
                    CellFormula = new CellFormula(testCell.Formula ?? "1+1"),
                    CellValue = new CellValue(testCell.Value ?? "0"),
                };
            case TestCellKind.FormulaNoCache:
                return new Cell
                {
                    CellReference = reference,
                    CellFormula = new CellFormula(testCell.Formula ?? "1+1"),
                };
            case TestCellKind.DateSerial:
                return new Cell
                {
                    CellReference = reference,
                    CellValue = new CellValue(testCell.Value ?? "43831"),
                };
            case TestCellKind.DateIso:
                return new Cell
                {
                    CellReference = reference,
                    DataType = CellValues.Date,
                    CellValue = new CellValue(testCell.Value ?? "2020-01-15"),
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(testCell));
        }
    }

    private static string ColumnName(int index)
    {
        string name = string.Empty;
        int i = index;
        while (true)
        {
            name = (char)('A' + (i % 26)) + name;
            i = (i / 26) - 1;
            if (i < 0)
            {
                return name;
            }
        }
    }
}
