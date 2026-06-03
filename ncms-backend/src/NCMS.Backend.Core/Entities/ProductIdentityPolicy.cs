using System.Collections.Generic;

namespace NCMS.Backend.Core.Entities;

public class ProductIdentityPolicy
{
    public string PolicyName { get; set; } = string.Empty;
    public List<string> RequiredClaimKeys { get; set; } = new();
}
