namespace NCMS.IoT.Host.Helpers;

public static class StatusColors
{
    public static string DeviceTag(string? status) => (status ?? "").ToUpperInvariant() switch
    {
        "ONLINE" or "ACTIVE" => "success",
        "PROVISIONING" or "PENDING" or "PENDING_ACTIVATION" => "warning",
        "DECOMMISSIONED" or "BLOCKED" or "OFFLINE" => "danger",
        _ => "secondary"
    };

    public static string HardwareTag(string? status)
    {
        var s = (status ?? "").ToUpperInvariant();
        if (s.Contains("ACTIV") || s == "ONLINE") return "success";
        if (s.Contains("PENDING")) return "warning";
        if (s.Contains("BLOCK") || s.Contains("REVOK") || s.Contains("DECOMM")) return "danger";
        return "secondary";
    }
}
