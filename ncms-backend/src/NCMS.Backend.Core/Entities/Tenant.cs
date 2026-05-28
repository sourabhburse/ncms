using System;

namespace NCMS.Backend.Core.Entities;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public Guid? ParentTenantId { get; set; }
    public Tenant? ParentTenant { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public int? MaxDevices { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
