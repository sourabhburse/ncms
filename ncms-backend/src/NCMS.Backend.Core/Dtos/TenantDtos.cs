using System;

namespace NCMS.Backend.Core.Dtos;

public record TenantResponse(
    Guid Id,
    string Name,
    string Slug,
    string? ContactEmail,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateTenantRequest(
    string Name,
    string Slug,
    string? ContactEmail
);

public record UpdateTenantRequest(
    string Name,
    string? ContactEmail,
    bool IsActive
);
