using System;

namespace NCMS.Backend.Core.Entities;

public sealed class DeviceCertificate
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public string Thumbprint { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public bool IsActive { get; set; } = true;
}
