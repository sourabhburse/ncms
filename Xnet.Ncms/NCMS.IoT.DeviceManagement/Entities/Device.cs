using NCMS.Shared.Domain;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// A provisioned, registered physical device instance.
/// Created from a HardwareInventory record during the ZTP registration flow.
/// Product information is always available via HardwareInventory.Product.
/// </summary>
public sealed class Device : IAuditable
{
    public Guid Id { get; set; }
    public Guid HardwareInventoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? FirmwareVersion { get; set; }
    public string? AgentVersion { get; set; }
    public string? HardwareModel { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool IsOnline { get; set; }

    // ── Fields from reference backend ─────────────────────────────────────────
    public string Status { get; set; } = "PROVISIONING";
    public string? WanIpAddress { get; set; }
    public Dictionary<string, string> MacAddresses { get; set; } = new();
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public HardwareInventory HardwareInventory { get; set; } = null!;
    public ICollection<DeviceCertificate> Certificates { get; set; } = [];
    public ICollection<TelemetryRecord> TelemetryRecords { get; set; } = [];
    public ICollection<DeviceTelemetry> DeviceTelemetries { get; set; } = [];
    public ICollection<DeviceEvent> Events { get; set; } = [];
}
