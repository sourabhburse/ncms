using System.Text.Json.Serialization;

namespace NCMS.Backend.Core.Provisioning;

public sealed class RegisterDeviceRequest
{
    [JsonPropertyName("serial_number")]
    public string SerialNumber { get; init; } = string.Empty;
    [JsonPropertyName("firmware_version")]
    public string FirmwareVersion { get; init; } = string.Empty;
    [JsonPropertyName("agent_version")]
    public string AgentVersion { get; init; } = string.Empty;
    [JsonPropertyName("hardware_model")]
    public string HardwareModel { get; init; } = string.Empty;
    [JsonPropertyName("board_name")]
    public string? BoardName { get; init; }
    [JsonPropertyName("mac_addresses")]
    public Dictionary<string, string> MacAddresses { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("identity_claims")]
    public Dictionary<string, string?> IdentityClaims { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("csr")]
    public string Csr { get; init; } = string.Empty;
}
