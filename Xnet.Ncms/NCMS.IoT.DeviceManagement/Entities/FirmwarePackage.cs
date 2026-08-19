using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Entities;

public sealed class Firmware
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public FirmwareType Type { get; set; } = FirmwareType.Full;
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string Md5 { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    // Extended fields kept for lifecycle and integrity verification
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DeviceTypeCode { get; set; } = string.Empty;
    public string BinaryChecksum { get; set; } = string.Empty;
    public string DigitalSignature { get; set; } = string.Empty;
    public string? MinRequiredFirmwareVersion { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Whether this package may be used when creating upgrade tasks.
    /// Disabled packages are hidden from the upgrade-task package selector.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    // Navigation
    public ICollection<FirmwareProduct> SupportedProducts { get; set; } = new List<FirmwareProduct>();
    public ICollection<UpgradeTask> UpgradeTasks { get; set; } = new List<UpgradeTask>();
}
