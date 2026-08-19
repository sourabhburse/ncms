using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// A device configuration file/profile. Mirrors <see cref="Firmware"/> but is scoped to a
/// single <see cref="Product"/> via the <see cref="ProductId"/> foreign key (the product
/// model is read from the linked <see cref="Product"/> entity, never copied).
/// </summary>
public sealed class ConfigProfile
{
    public Guid Id { get; set; }

    /// <summary>Display name of the config profile (typically the uploaded file name).</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>Original file name as uploaded.</summary>
    public string OriginalFilename { get; set; } = string.Empty;

    /// <summary>Public download URL for the stored config file.</summary>
    public string StoragePath { get; set; } = string.Empty;

    public long Size { get; set; }
    public string Md5 { get; set; } = string.Empty;

    // ── Product (one product per profile; the model name lives on Product) ────
    public Guid ProductId { get; set; }

    public ProfileStatus Status { get; set; } = ProfileStatus.Enable;
    public string? Remark { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Product Product { get; set; } = null!;
    public ICollection<ConfigureTask> ConfigureTasks { get; set; } = new List<ConfigureTask>();
}
