using System.Security.Claims;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.Identity.Configuration;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Contracts.Services;
using NCMS.IoT.Identity.Data.Seeding;
using NCMS.IoT.Identity.Entities;
using NCMS.IoT.Identity.Services;
using NCMS.Shared.Exceptions;

namespace NCMS.IoT.Identity.Features.V1.Roles;

public static class UpdateRolePermissions
{
    public sealed record Command(Guid RoleId, IReadOnlyList<string> Permissions) : IRequest<RolePermissionsDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.RoleId).NotEmpty();
    }

    public sealed class Handler : IRequestHandler<Command, RolePermissionsDto>
    {
        private readonly RoleManager<AppRole> _roleManager;
        private readonly KnownPermissions _knownPermissions;
        private readonly IAuditLogService _auditLog;
        private readonly ICurrentUser _currentUser;
        private readonly IRolePermissionCache _rolePermissionCache;

        public Handler(
            RoleManager<AppRole> roleManager, KnownPermissions knownPermissions,
            IAuditLogService auditLog, ICurrentUser currentUser, IRolePermissionCache rolePermissionCache)
        {
            _roleManager = roleManager;
            _knownPermissions = knownPermissions;
            _auditLog = auditLog;
            _currentUser = currentUser;
            _rolePermissionCache = rolePermissionCache;
        }

        public async ValueTask<RolePermissionsDto> Handle(Command req, CancellationToken ct)
        {
            var role = await _roleManager.FindByIdAsync(req.RoleId.ToString())
                ?? throw NotFoundException.For<AppRole>(req.RoleId);

            var requested = req.Permissions
                .Where(p => _knownPermissions.All.Contains(p, StringComparer.Ordinal))
                .ToHashSet(StringComparer.Ordinal);

            var existingClaims = (await _roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == RoleAndAdminSeeder.PermissionClaimType)
                .ToList();
            var existingValues = existingClaims.Select(c => c.Value).ToHashSet(StringComparer.Ordinal);

            var added = requested.Except(existingValues).ToList();
            var removed = existingValues.Except(requested).ToList();

            foreach (var claim in existingClaims.Where(c => !requested.Contains(c.Value)))
                await _roleManager.RemoveClaimAsync(role, claim);

            foreach (var permission in added)
                await _roleManager.AddClaimAsync(role, new Claim(RoleAndAdminSeeder.PermissionClaimType, permission));

            if (added.Count > 0 || removed.Count > 0)
            {
                _rolePermissionCache.Invalidate(role.Name!);

                await _auditLog.RecordAsync(
                    AuditEventType.RolePermissionsChanged,
                    $"Role '{role.Name}' permissions changed: +{added.Count} / -{removed.Count}.",
                    actorUserId: _currentUser.UserId,
                    actorDisplay: _currentUser.UserName ?? _currentUser.Email,
                    ct: ct);
            }

            var granted = requested;
            var permissions = _knownPermissions.All
                .Select(p => new RoleClaimDto(p, granted.Contains(p)))
                .ToList();

            return new RolePermissionsDto(role.Id, role.Name!, permissions);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{id:guid}/permissions", async (
                Guid id, UpdateRolePermissionsRequest request, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Command(id, request.Permissions), ct)))
            .WithSummary("Replace a role's granted permissions")
            .RequireAuthorization(IdentityPermissions.Roles.Edit);
}
