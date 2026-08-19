namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Expresses that a firmware binary is compatible with a specific Product model.
/// This is the source of truth for upgrade eligibility: before creating an UpgradeTask
/// the system must verify a FirmwareProduct record exists for
/// (FirmwarePackageId, device.ProductId).
///
/// Why M2M: one firmware build often supports an entire hardware family (same architecture,
/// different SKU names). Storing compatibility here avoids uploading duplicate binaries.
///
/// Invariants:
///   - Product.SupportsFirmwareUpgrade must be true
///   - Cannot be removed while an active UpgradeTask targets devices of this product
///     with this firmware
/// </summary>
public sealed class FirmwareProduct
{
    public Guid FirmwareId { get; set; }
    public Guid ProductId { get; set; }

    // Navigation
    public Firmware Firmware { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
