using System.Text.Json;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;

namespace NCMS.IoT.Host.Helpers;

/// <summary>
/// Server-side equivalent of the <c>telemetryTable()</c> Alpine component in Details.cshtml:
/// telemetry payloads have no fixed schema, so columns are discovered by walking every row's
/// PayloadJson in order of first appearance. Kept in sync with that component so an exported
/// sheet has the same columns, in the same order, as the table on screen.
/// </summary>
public static class TelemetryTable
{
    public sealed record Flattened(
        IReadOnlyList<string> Columns,
        IReadOnlyList<(DateTimeOffset Timestamp, IReadOnlyDictionary<string, object?> Values)> Rows);

    public static Flattened Flatten(IEnumerable<TelemetryRecordDto> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<(DateTimeOffset, IReadOnlyDictionary<string, object?>)>();

        foreach (var record in records)
        {
            var values = ParsePayload(record.PayloadJson);
            foreach (var key in values.Keys)
            {
                if (seen.Add(key)) columns.Add(key);
            }
            rows.Add((record.Timestamp, values));
        }

        return new Flattened(columns, rows);
    }

    /// <summary>
    /// Matches the client's <c>label()</c>: underscores become spaces and each word is
    /// capitalised, so <c>ram_usage_mb</c> renders as <c>Ram Usage Mb</c>.
    /// </summary>
    public static string Label(string column)
    {
        if (string.IsNullOrEmpty(column)) return column;

        var chars = column.Replace('_', ' ').ToCharArray();
        var startOfWord = true;
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i])) { startOfWord = true; continue; }
            if (startOfWord) { chars[i] = char.ToUpperInvariant(chars[i]); startOfWord = false; }
        }
        return new string(chars);
    }

    /// <summary>
    /// A malformed or non-object payload yields no columns rather than failing the export —
    /// the on-screen table degrades the same way (its JSON.parse is wrapped in try/catch).
    /// </summary>
    private static Dictionary<string, object?> ParsePayload(string? payloadJson)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(payloadJson)) return values;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return values;

            foreach (var prop in doc.RootElement.EnumerateObject())
                values[prop.Name] = ToCellValue(prop.Value);
        }
        catch (JsonException)
        {
            // Leave the row's cells empty.
        }

        return values;
    }

    private static object? ToCellValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l
            : element.TryGetDecimal(out var d) ? d
            : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        // Nested objects/arrays have no column of their own; keep the raw JSON so the value
        // is still visible in the sheet.
        _ => element.GetRawText()
    };
}
