namespace NCMS.IoT.Identity.Contracts.Dtos;

public sealed record RoleDto(Guid Id, string Name, string? Description);

public sealed record CreateRoleRequest(string Name, string? Description);

public sealed record UpdateRoleRequest(string? Name, string? Description);

public sealed record RolePermissionsDto(Guid RoleId, string RoleName, IReadOnlyList<RoleClaimDto> Permissions);

public sealed record RoleClaimDto(string Permission, bool Granted);

public sealed record UpdateRolePermissionsRequest(IReadOnlyList<string> Permissions);
