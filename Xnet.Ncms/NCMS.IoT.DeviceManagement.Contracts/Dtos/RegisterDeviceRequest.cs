using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NCMS.IoT.DeviceManagement.Contracts.Dtos;

public sealed class RegisterDeviceRequest
{
    [JsonPropertyName("serial_number")]
    [Required, MaxLength(128)]
    public required string SerialNumber { get; init; }

    [JsonPropertyName("identity_claims")]
    [Required]
    public required IdentityClaimsDto IdentityClaims { get; init; }

    [JsonPropertyName("csr")]
    [Required]
    public required string Csr { get; init; }

    [JsonPropertyName("firmware_version")]
    [MaxLength(64)]
    public string? FirmwareVersion { get; init; }

    [JsonPropertyName("agent_version")]
    [MaxLength(64)]
    public string? AgentVersion { get; init; }

    [JsonPropertyName("hardware_model")]
    [MaxLength(64)]
    public string? HardwareModel { get; init; }
}

public sealed class IdentityClaimsDto
{
    [JsonPropertyName("base_mac")]
    [MaxLength(64)]
    public string? BaseMac { get; init; }
}
