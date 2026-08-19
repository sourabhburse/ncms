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

public static class CreateRole
{
    public sealed record Command(string Name, string? Description) : IRequest<RoleDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
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
            if (await _roleManager.RoleExistsAsync(req.Name))
                throw new ConflictException($"A role named '{req.Name}' already exists.");

            var role = new AppRole(req.Name, req.Description);
            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
                throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));

            await _auditLog.RecordAsync(
                AuditEventType.RoleCreated,
                $"Role '{role.Name}' was created.",
                actorUserId: _currentUser.UserId,
                actorDisplay: _currentUser.UserName ?? _currentUser.Email,
                ct: ct);

            return new RoleDto(role.Id, role.Name!, role.Description);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateRoleRequest request, ISender sender, CancellationToken ct) =>
        {
            var response = await sender.Send(new Command(request.Name, request.Description), ct);
            return Results.Created($"/api/v1/roles/{response.Id}", response);
        })
        .WithSummary("Create a new role")
        .RequireAuthorization(IdentityPermissions.Roles.Add);
}
