using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.Identity.Configuration;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Data.Seeding;
using NCMS.IoT.Identity.Entities;
using NCMS.Shared.Exceptions;

namespace NCMS.IoT.Identity.Features.V1.Roles;

public static class GetRolePermissions
{
    public sealed record Query(Guid RoleId) : IRequest<RolePermissionsDto>;

    public sealed class Handler : IRequestHandler<Query, RolePermissionsDto>
    {
        private readonly RoleManager<AppRole> _roleManager;
        private readonly KnownPermissions _knownPermissions;

        public Handler(RoleManager<AppRole> roleManager, KnownPermissions knownPermissions)
        {
            _roleManager = roleManager;
            _knownPermissions = knownPermissions;
        }

        public async ValueTask<RolePermissionsDto> Handle(Query req, CancellationToken ct)
        {
            var role = await _roleManager.FindByIdAsync(req.RoleId.ToString())
                ?? throw NotFoundException.For<AppRole>(req.RoleId);

            var granted = (await _roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == RoleAndAdminSeeder.PermissionClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            var permissions = _knownPermissions.All
                .Select(p => new RoleClaimDto(p, granted.Contains(p)))
                .ToList();

            return new RolePermissionsDto(role.Id, role.Name!, permissions);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}/permissions", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new Query(id), ct)))
            .WithSummary("Get a role's granted permissions, alongside every known permission")
            .RequireAuthorization(IdentityPermissions.Roles.View);
}
