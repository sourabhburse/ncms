using Microsoft.AspNetCore.Identity;

namespace NCMS.IoT.Identity.Entities;

public sealed class AppRole : IdentityRole<Guid>
{
    public string? Description { get; set; }

    public AppRole() { }

    public AppRole(string roleName, string? description = null) : base(roleName)
    {
        Description = description;
    }
}
