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

namespace NCMS.IoT.Identity.Features.V1.Roles;

public static class UpdateRole
{
    public sealed record Command(Guid Id, string? Name, string? Description) : IRequest<RoleDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.Id).NotEmpty();
    }

    public sealed class Handler : IRequestHandler<Command, RoleDto>
    {
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IAuditLogService _auditLog;
        private readonly ICurrentUser _currentUser;

        public Handler(RoleManager<AppRole> roleManager, IAuditLogService auditLog, ICurrentUser currentUser)
        {
            _roleManager = roleManager;
            _auditLog = auditLog;
            _currentUser = currentUser;
        }

        public async ValueTask<RoleDto> Handle(Command req, CancellationToken ct)
        {
            var role = await _roleManager.FindByIdAsync(req.Id.ToString())
                ?? throw NotFoundException.For<AppRole>(req.Id);

            if (!string.IsNullOrWhiteSpace(req.Name)) role.Name = req.Name;
            if (req.Description is not null) role.Description = req.Description;

            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
                throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));

            await _auditLog.RecordAsync(
                AuditEventType.RoleUpdated,
                $"Role '{role.Name}' was updated.",
                actorUserId: _currentUser.UserId,
                actorDisplay: _currentUser.UserName ?? _currentUser.Email,
                ct: ct);

            return new RoleDto(role.Id, role.Name!, role.Description);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{id:guid}", async (Guid id, UpdateRoleRequest request, ISender sender, CancellationToken ct) =>
        {
            var response = await sender.Send(new Command(id, request.Name, request.Description), ct);
            return Results.Ok(response);
        })
        .WithSummary("Update a role's name/description")
        .RequireAuthorization(IdentityPermissions.Roles.Edit);
}
