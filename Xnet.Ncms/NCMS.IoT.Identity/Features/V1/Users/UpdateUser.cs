using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Contracts.Services;
using NCMS.IoT.Identity.Entities;
using NCMS.IoT.Identity.Services;
using NCMS.Shared.Exceptions;

namespace NCMS.IoT.Identity.Features.V1.Users;

public static class UpdateUser
{
    public sealed record Command(
        Guid Id, string? FirstName, string? LastName, bool? IsActive, IReadOnlyList<string>? Roles)
        : IRequest<UserDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.Id).NotEmpty();
    }

    public sealed class Handler : IRequestHandler<Command, UserDto>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IAuditLogService _auditLog;
        private readonly ICurrentUser _currentUser;

        public Handler(
            UserManager<AppUser> userManager, RoleManager<AppRole> roleManager,
            IAuditLogService auditLog, ICurrentUser currentUser)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditLog = auditLog;
            _currentUser = currentUser;
        }

        public async ValueTask<UserDto> Handle(Command req, CancellationToken ct)
        {
            var user = await _userManager.FindByIdAsync(req.Id.ToString())
                ?? throw NotFoundException.For<AppUser>(req.Id);

            var changes = new List<string>();

            if (req.FirstName is not null && req.FirstName != user.FirstName)
            {
                user.FirstName = req.FirstName;
                changes.Add("first name");
            }
            if (req.LastName is not null && req.LastName != user.LastName)
            {
                user.LastName = req.LastName;
                changes.Add("last name");
            }
            if (req.IsActive is not null && req.IsActive.Value != user.IsActive)
            {
                user.IsActive = req.IsActive.Value;
                changes.Add(user.IsActive ? "enabled" : "disabled");
            }

            await _userManager.UpdateAsync(user);

            if (req.Roles is not null)
            {
                var currentRoles = (await _userManager.GetRolesAsync(user)).ToHashSet(StringComparer.Ordinal);
                var requestedRoles = req.Roles.ToHashSet(StringComparer.Ordinal);

                if (!currentRoles.SetEquals(requestedRoles))
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    foreach (var roleName in req.Roles)
                    {
                        if (await _roleManager.RoleExistsAsync(roleName))
                            await _userManager.AddToRoleAsync(user, roleName);
                    }
                    changes.Add($"roles ({string.Join(", ", requestedRoles)})");
                }
            }

            if (changes.Count > 0)
            {
                await _auditLog.RecordAsync(
                    AuditEventType.UserUpdated,
                    $"User '{user.Email}' updated: {string.Join(", ", changes)}.",
                    subjectUserId: user.Id,
                    subjectDisplay: user.Email,
                    actorUserId: _currentUser.UserId,
                    actorDisplay: _currentUser.UserName ?? _currentUser.Email,
                    ct: ct);
            }

            var roles = await _userManager.GetRolesAsync(user);
            return new UserDto(user.Id, user.UserName ?? "", user.Email ?? "", user.FirstName, user.LastName, user.IsActive, roles.ToList());
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new Command(id, request.FirstName, request.LastName, request.IsActive, request.Roles);
            return Results.Ok(await sender.Send(command, ct));
        })
        .WithSummary("Update a user's profile, active state, and role assignments")
        .RequireAuthorization(IdentityPermissions.Users.Edit);
}
