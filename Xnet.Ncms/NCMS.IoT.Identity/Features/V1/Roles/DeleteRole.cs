using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Contracts.Services;
using NCMS.IoT.Identity.Data.Seeding;
using NCMS.IoT.Identity.Entities;
using NCMS.IoT.Identity.Services;
using NCMS.Shared.Exceptions;

namespace NCMS.IoT.Identity.Features.V1.Roles;

public static class DeleteRole
{
    public sealed record Command(Guid Id) : IRequest<Unit>;

    public sealed class Handler : IRequestHandler<Command, Unit>
    {
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IAuditLogService _auditLog;
        private readonly ICurrentUser _currentUser;
        private readonly IRolePermissionCache _rolePermissionCache;

        public Handler(
            RoleManager<AppRole> roleManager, IAuditLogService auditLog,
            ICurrentUser currentUser, IRolePermissionCache rolePermissionCache)
        {
            _roleManager = roleManager;
            _auditLog = auditLog;
            _currentUser = currentUser;
            _rolePermissionCache = rolePermissionCache;
        }

        public async ValueTask<Unit> Handle(Command req, CancellationToken ct)
        {
            var role = await _roleManager.FindByIdAsync(req.Id.ToString())
                ?? throw NotFoundException.For<AppRole>(req.Id);

            if (role.Name == RoleAndAdminSeeder.AdminRoleName)
                throw new DomainException("The built-in Admin role cannot be deleted.");

            var roleName = role.Name;
            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
                throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));

            _rolePermissionCache.Invalidate(roleName!);

            await _auditLog.RecordAsync(
                AuditEventType.RoleDeleted,
                $"Role '{roleName}' was deleted.",
                actorUserId: _currentUser.UserId,
                actorDisplay: _currentUser.UserName ?? _currentUser.Email,
                ct: ct);

            return Unit.Value;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new Command(id), ct);
            return Results.NoContent();
        })
        .WithSummary("Delete a role")
        .RequireAuthorization(IdentityPermissions.Roles.Delete);
}
