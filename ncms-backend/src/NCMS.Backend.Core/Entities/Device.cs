using System;
using System.Collections.Generic;

namespace NCMS.Backend.Core.Entities;

public sealed class Device
{
    public Guid Id { get; set; }
    
    public Guid HardwareInventoryId { get; set; }
    public HardwareInventory? HardwareInventory { get; set; }
    
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    
    public string? Name { get; set; }
    public string Status { get; set; } = "PROVISIONING";
    public DateTime? LastSeenAt { get; set; }
    
    public string? CurrentFirmwareVersion { get; set; }
    public string? CurrentAgentVersion { get; set; }
    public string? WanIpAddress { get; set; }
    public Dictionary<string, string> MacAddresses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DeviceCertificate> Certificates { get; set; } = [];
    public ICollection<DeviceTelemetry> Telemetries { get; set; } = [];
}
