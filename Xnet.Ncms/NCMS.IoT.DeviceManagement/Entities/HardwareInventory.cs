using NCMS.Shared.Domain;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// A physical unit that has arrived in the warehouse but has not yet been provisioned.
/// This is the primary record the operator interacts with — it represents trackable stock.
/// When a device registers/provisions against this record, a Device entity is created and
/// IsProvisioned flips to true (one-way).
/// </summary>
public sealed class HardwareInventory : IAuditable
{
    public Guid Id { get; set; }

    /// <summary>
    /// Links this physical unit to its device model.
    /// Required at import — you must know what model the hardware is before it arrives.
    /// Immutable after creation.
    /// </summary>
    public Guid ProductId { get; set; }

    public required string SerialNumber { get; set; }

    /// <summary>Once true, a Device has been created. Cannot be reversed without retiring the Device.</summary>
    public bool IsProvisioned { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // ── Lifecycle & identity (from reference backend) ─────────────────────────
    public string Status { get; set; } = "PENDING_ACTIVATION";
    public string IdentityPolicy { get; set; } = "serial_only";
    public Dictionary<string, string?> IdentityClaims { get; set; } = new();
    public DateTimeOffset? ActivationWindowStart { get; set; }
    public DateTimeOffset? ActivationWindowEnd { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public Device? Device { get; set; }
}
