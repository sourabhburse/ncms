using ClosedXML.Excel;

namespace NCMS.IoT.Host.Helpers;

/// <summary>
/// Builds .xlsx downloads for the list/telemetry screens. Cells are written as native types
/// (numbers stay numeric, timestamps stay dates) so the exported sheet is usable for analysis
/// rather than a grid of strings.
/// </summary>
public static class ExcelExport
{
    /// <summary>Excel's own hard ceiling is 1,048,576 rows; cap well below it to bound memory.</summary>
    public const int MaxRows = 50_000;

    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string DateFormat = "yyyy-mm-dd hh:mm:ss";

    /// <summary>
    /// Renders a single worksheet from <paramref name="headers"/> and <paramref name="rows"/>.
    /// A null cell is left blank; <see cref="DateTimeOffset"/> is written in local time.
    /// </summary>
    public static byte[] Build(
        string sheetName,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SafeSheetName(sheetName));

        for (var c = 0; c < headers.Count; c++)
            sheet.Cell(1, c + 1).Value = headers[c];

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Count && c < headers.Count; c++)
                WriteCell(sheet.Cell(r, c + 1), row[c]);

            r++;
            if (r - 2 >= MaxRows) break;
        }

        // Only autofit when there is something to measure — AdjustToContents on an empty
        // sheet still walks every column and is pure overhead.
        if (headers.Count > 0)
            sheet.Columns(1, headers.Count).AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string s:
                cell.Value = s;
                break;
            case bool b:
                cell.Value = b ? "Yes" : "No";
                break;
            case DateTimeOffset dto:
                cell.Value = dto.ToLocalTime().DateTime;
                cell.Style.DateFormat.Format = DateFormat;
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = DateFormat;
                break;
            case byte or sbyte or short or ushort or int or uint or long or ulong
                 or float or double or decimal:
                cell.Value = Convert.ToDecimal(value);
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    /// <summary>
    /// Excel rejects sheet names over 31 chars or containing : \ / ? * [ ].
    /// </summary>
    private static string SafeSheetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Sheet1";

        var cleaned = new string(name.Select(ch => ":\\/?*[]".Contains(ch) ? '-' : ch).ToArray());
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    /// <summary>Timestamped file name, e.g. <c>telemetry_20260819_142530.xlsx</c>.</summary>
    public static string FileName(string prefix) =>
        $"{prefix}_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.xlsx";
}
