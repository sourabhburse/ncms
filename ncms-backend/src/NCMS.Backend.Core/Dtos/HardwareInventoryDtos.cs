using System;
using System.Collections.Generic;

namespace NCMS.Backend.Core.Dtos;

public record HardwareInventoryResponse(
    Guid Id,
    Guid TenantId,
    Guid ProductId,
    string SerialNumber,
    string Status,
    string IdentityPolicy,
    Dictionary<string, string?> IdentityClaims,
    DateTime ImportedAt
);

public record CreateHardwareInventoryRequest(
    Guid TenantId,
    Guid ProductId,
    string SerialNumber,
    string IdentityPolicy,
    Dictionary<string, string?> IdentityClaims
);

public record UpdateHardwareInventoryRequest(
    string Status,
    string IdentityPolicy,
    Dictionary<string, string?> IdentityClaims
);
