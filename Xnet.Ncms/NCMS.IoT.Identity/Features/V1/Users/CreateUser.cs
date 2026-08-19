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

public static class CreateUser
{
    public sealed record Command(
        string UserName, string Email, string Password,
        string? FirstName, string? LastName, IReadOnlyList<string>? Roles) : IRequest<UserDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserName).NotEmpty().MaximumLength(256);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        }
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
            if (await _userManager.FindByEmailAsync(req.Email) is not null)
                throw new ConflictException($"A user with email '{req.Email}' already exists.");

            var user = new AppUser
            {
                UserName = req.UserName,
                Email = req.Email,
                FirstName = req.FirstName,
                LastName = req.LastName,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));

            var roles = new List<string>();
            foreach (var roleName in req.Roles ?? [])
            {
                if (await _roleManager.RoleExistsAsync(roleName))
                {
                    await _userManager.AddToRoleAsync(user, roleName);
                    roles.Add(roleName);
                }
            }

            await _auditLog.RecordAsync(
                AuditEventType.UserCreated,
                $"User '{user.Email}' was created" + (roles.Count > 0 ? $" with role(s): {string.Join(", ", roles)}." : "."),
                subjectUserId: user.Id,
                subjectDisplay: user.Email,
                actorUserId: _currentUser.UserId,
                actorDisplay: _currentUser.UserName ?? _currentUser.Email,
                ct: ct);

            return new UserDto(user.Id, user.UserName, user.Email, user.FirstName, user.LastName, user.IsActive, roles);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateUserRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new Command(
                request.UserName, request.Email, request.Password,
                request.FirstName, request.LastName, request.Roles);
            var response = await sender.Send(command, ct);
            return Results.Created($"/api/v1/users/{response.Id}", response);
        })
        .WithSummary("Create a new user")
        .RequireAuthorization(IdentityPermissions.Users.Add);
}
