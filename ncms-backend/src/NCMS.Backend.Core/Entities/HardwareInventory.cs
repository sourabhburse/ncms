using System;
using System.Collections.Generic;

namespace NCMS.Backend.Core.Entities;

public sealed class HardwareInventory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string IdentityPolicy { get; set; } = "serial_only";
    public Dictionary<string, string?> IdentityClaims { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Status { get; set; } = "PENDING_ACTIVATION";
    public DateTime? ActivationWindowStart { get; set; }
    public DateTime? ActivationWindowEnd { get; set; }
    
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public Guid? ImportedByUserId { get; set; }
}
