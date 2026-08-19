namespace NCMS.IoT.Host.Helpers;

public static class Format
{
    public static string Macs(IReadOnlyDictionary<string, string>? macs)
    {
        if (macs is null || macs.Count == 0) return "—";
        return string.Join("  ·  ", macs.Select(m => $"{m.Key}: {m.Value}"));
    }

    public static string Coordinates(decimal? lat, decimal? lng)
        => lat is null || lng is null ? "—" : $"{lat}, {lng}";

    public static string OrDash(string? value) => string.IsNullOrEmpty(value) ? "—" : value;

    public static string DateTime(DateTimeOffset? value)
        => value is null ? "—" : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public static string Uptime(long seconds)
    {
        var d = seconds / 86400;
        var h = (seconds % 86400) / 3600;
        var m = (seconds % 3600) / 60;
        var parts = new List<string>();
        if (d > 0) parts.Add($"{d} day{(d > 1 ? "s" : "")}");
        if (h > 0) parts.Add($"{h} hr{(h > 1 ? "s" : "")}");
        if (m > 0) parts.Add($"{m} min");
        return parts.Count > 0 ? string.Join(", ", parts) : "< 1 min";
    }
}
