namespace NCMS.IoT.DeviceManagement.Entities;

public sealed class DeviceCertificate
{
    public int Id { get; set; }
    public Guid DeviceId { get; set; }
    public required string Thumbprint { get; set; }
    public required string CertificatePem { get; set; }
    public required string SubjectName { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Device Device { get; set; } = null!;
}
