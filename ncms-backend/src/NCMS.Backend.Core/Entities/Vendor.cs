using System;

namespace NCMS.Backend.Core.Entities;

public sealed class Vendor
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
