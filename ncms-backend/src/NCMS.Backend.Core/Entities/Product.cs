using System;

namespace NCMS.Backend.Core.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string ConfigFormat { get; set; } = string.Empty; // e.g. "uci"
    public string ConfigSchemaVersion { get; set; } = "1.0";
    public bool HasCellular { get; set; }
    public bool HasWifi { get; set; }
    public bool HasEthernet { get; set; }
    public bool HasGps { get; set; }
    public bool HasModbus { get; set; }
    public int? FlashSizeMB { get; set; }
    public int? RamSizeMB { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
