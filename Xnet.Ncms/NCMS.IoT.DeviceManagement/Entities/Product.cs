using NCMS.Shared.Domain;

namespace NCMS.IoT.DeviceManagement.Entities;

public sealed class Product : BaseEntity<Guid>
{
    private Product() { }

    public string Name { get; private set; } = default!;
    public Guid ProductTypeId { get; private set; }
    public bool IsDeleted { get; private set; }

    // Per-model capability flags.
    public bool SupportsFirmwareUpgrade { get; private set; }
    public bool SupportsConfiguration { get; private set; }
    public bool SupportsRemoteConfiguration { get; private set; }
    public bool SupportsCommands { get; private set; }
    public bool SupportsRealtimeLogs { get; private set; }
    public bool SupportsTelemetry { get; private set; }
    public bool SupportsSoftwarePackages { get; private set; }

    public ProductType ProductType { get; private set; } = default!;
    public ICollection<FirmwareProduct> FirmwareProducts { get; } = new List<FirmwareProduct>();
    public ICollection<HardwareInventory> HardwareInventories { get; } = new List<HardwareInventory>();

    public static Product Create(
        string name,
        Guid productTypeId,
        bool supportsFirmwareUpgrade = false,
        bool supportsConfiguration = false,
        bool supportsRemoteConfiguration = false,
        bool supportsCommands = false,
        bool supportsRealtimeLogs = false,
        bool supportsTelemetry = false,
        bool supportsSoftwarePackages = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 200) throw new ArgumentException("Name must be 200 characters or fewer.", nameof(name));
        if (productTypeId == Guid.Empty) throw new ArgumentException("ProductTypeId is required.", nameof(productTypeId));

        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            ProductTypeId = productTypeId,
            SupportsFirmwareUpgrade = supportsFirmwareUpgrade,
            SupportsConfiguration = supportsConfiguration,
            SupportsRemoteConfiguration = supportsRemoteConfiguration,
            SupportsCommands = supportsCommands,
            SupportsRealtimeLogs = supportsRealtimeLogs,
            SupportsTelemetry = supportsTelemetry,
            SupportsSoftwarePackages = supportsSoftwarePackages
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 200) throw new ArgumentException("Name must be 200 characters or fewer.", nameof(name));
        Name = name.Trim();
    }

    /// <summary>
    /// Capability flags may only be updated while zero HardwareInventory items are provisioned
    /// for this product. Call site must enforce this guard before invoking.
    /// </summary>
    public void UpdateCapabilities(
        bool supportsFirmwareUpgrade,
        bool supportsConfiguration,
        bool supportsRemoteConfiguration,
        bool supportsCommands,
        bool supportsRealtimeLogs,
        bool supportsTelemetry,
        bool supportsSoftwarePackages)
    {
        SupportsFirmwareUpgrade = supportsFirmwareUpgrade;
        SupportsConfiguration = supportsConfiguration;
        SupportsRemoteConfiguration = supportsRemoteConfiguration;
        SupportsCommands = supportsCommands;
        SupportsRealtimeLogs = supportsRealtimeLogs;
        SupportsTelemetry = supportsTelemetry;
        SupportsSoftwarePackages = supportsSoftwarePackages;
    }

    /// <summary>Call only after verifying no HardwareInventory items reference this product.</summary>
    public void SoftDelete() => IsDeleted = true;
}
