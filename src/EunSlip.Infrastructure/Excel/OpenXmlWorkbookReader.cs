using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EunSlip.Core.Payroll;
using EunSlip.Core.Validation;

namespace EunSlip.Infrastructure.Excel;

public sealed class OpenXmlWorkbookReader : IPayrollWorkbookReader
{
    public WorkbookReadResult Read(string filePath)
    {
        SpreadsheetDocument document;
        try
        {
            document = SpreadsheetDocument.Open(filePath, isEditable: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FileFormatException)
        {
            throw new WorkbookUnreadableException($"Cannot open workbook '{filePath}'.", ex);
        }

        using (document)
        {
            try
            {
                return ReadContent(document);
            }
            catch (WorkbookUnreadableException)
            {
                throw;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Xml.XmlException
                or FormatException or OverflowException or KeyNotFoundException)
            {
                throw new WorkbookUnreadableException($"Workbook '{filePath}' is corrupt or unsupported.", ex);
            }
        }
    }

    private static WorkbookReadResult ReadContent(SpreadsheetDocument document)
    {
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new WorkbookUnreadableException("Workbook has no workbook part.");
        Sheet? firstSheet = workbookPart.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new WorkbookUnreadableException("Workbook contains no worksheet.");
        WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(firstSheet.Id!);

        List<string>? sharedStrings = workbookPart
            .GetPartsOfType<SharedStringTablePart>().FirstOrDefault()?.SharedStringTable?
            .Elements<SharedStringItem>().Select(item => item.InnerText).ToList();

        Worksheet? worksheet = worksheetPart.Worksheet
            ?? throw new WorkbookUnreadableException("Worksheet has no content.");
        SheetData sheetData = worksheet.Elements<SheetData>().First();
        List<Row> rows = [.. sheetData.Elements<Row>()];
        if (rows.Count == 0)
        {
            return new WorkbookReadResult([], [], []);
        }

        IReadOnlyList<string> headers = ReadHeaders(rows[0], sharedStrings);
        List<PayrollRowInput> employees = [];
        List<PayrollIssue> readIssues = [];
        int fallbackRowNumber = 1;

        foreach (Row row in rows.Skip(1))
        {
            fallbackRowNumber++;
            int rowNumber = row.RowIndex is { } rowIndex ? (int)rowIndex.Value : fallbackRowNumber;
            string?[] cells = new string?[27];
            Dictionary<int, Cell> formulaCells = [];
            foreach (Cell cell in row.Elements<Cell>())
            {
                int columnIndex = ColumnIndex(cell.CellReference?.Value);
                if (columnIndex is < 0 or > 26)
                {
                    continue;
                }

                if (cell.CellFormula is not null)
                {
                    formulaCells[columnIndex] = cell;
                }

                cells[columnIndex] = ResolveCellText(cell, sharedStrings);
            }

            foreach (KeyValuePair<int, Cell> entry in formulaCells)
            {
                if (entry.Value.CellValue is null)
                {
                    readIssues.Add(new PayrollIssue(
                        IssueSeverity.Blocking, "CachedValueMissing",
                        rowNumber, null,
                        FieldName(headers, entry.Key), null, null));
                }
            }

            if (cells.All(string.IsNullOrEmpty))
            {
                continue;
            }

            employees.Add(BuildRowInput(rowNumber, cells, headers, readIssues));
        }

        return new WorkbookReadResult(headers, employees, readIssues);
    }

    private static List<string> ReadHeaders(Row headerRow, List<string>? sharedStrings)
    {
        List<string> headers = [];
        foreach (Cell cell in headerRow.Elements<Cell>())
        {
            int columnIndex = ColumnIndex(cell.CellReference?.Value);
            if (columnIndex < 0)
            {
                continue;
            }

            while (headers.Count <= columnIndex)
            {
                headers.Add(string.Empty);
            }

            headers[columnIndex] = ResolveCellText(cell, sharedStrings) ?? string.Empty;
        }
        return headers;
    }

    private static PayrollRowInput BuildRowInput(
        int rowNumber, string?[] cells, IReadOnlyList<string> headers, List<PayrollIssue> readIssues)
    {
        decimal? Numeric(int index)
        {
            return ParseNumeric(rowNumber, index, cells[index], headers, readIssues);
        }

        return new PayrollRowInput(
            rowNumber,
            cells[0], cells[1], cells[2], cells[3],
            ParseDate(rowNumber, cells[4], headers, readIssues),
            cells[5], cells[6],
            Numeric(7), Numeric(8), Numeric(9), Numeric(10), Numeric(11), Numeric(12),
            Numeric(13), Numeric(14), Numeric(15), Numeric(16), Numeric(17), Numeric(18),
            Numeric(19), Numeric(20), Numeric(21), Numeric(22),
            Numeric(23), Numeric(24), Numeric(25), Numeric(26));
    }

    private static decimal? ParseNumeric(
        int rowNumber, int index, string? text, IReadOnlyList<string> headers, List<PayrollIssue> readIssues)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
        {
            readIssues.Add(new PayrollIssue(
                IssueSeverity.Blocking, "InvalidNumeric", rowNumber, null,
                FieldName(headers, index), text, null));
            return null;
        }

        return value;
    }

    private static DateOnly? ParseDate(
        int rowNumber, string? text, IReadOnlyList<string> headers, List<PayrollIssue> readIssues)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly isoDate))
        {
            return isoDate;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial)
            && serial is >= 0 and <= 2_958_465)
        {
            return DateOnly.FromDateTime(DateTime.FromOADate(serial));
        }

        readIssues.Add(new PayrollIssue(
            IssueSeverity.Blocking, "InvalidDate", rowNumber, null,
            FieldName(headers, 4), text, null));
        return null;
    }

    private static string? ResolveCellText(Cell cell, List<string>? sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            string? indexText = cell.CellValue?.Text;
            if (indexText is null || sharedStrings is null)
            {
                return null;
            }

            return int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                && index >= 0 && index < sharedStrings.Count
                    ? sharedStrings[index]
                    : null;
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText;
        }

        string? value = cell.CellValue?.Text;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string FieldName(IReadOnlyList<string> headers, int columnIndex) =>
        columnIndex < headers.Count && headers[columnIndex].Length > 0
            ? headers[columnIndex]
            : $"Column {columnIndex + 1}";

    private static int ColumnIndex(string? cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
        {
            return -1;
        }

        int index = 0;
        foreach (char c in cellReference)
        {
            if (c is < 'A' or > 'Z')
            {
                break;
            }

            int digit = c - 'A' + 1;
            index = (index * 26) + digit;
        }

        return index - 1;
    }
}
