using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Contracts.Services;
using NCMS.IoT.Identity.Entities;
using NCMS.IoT.Identity.Services;
using NCMS.Shared.Exceptions;

namespace NCMS.IoT.Identity.Features.V1.Users;

/// <summary>
/// Admin-driven password reset — unlike self-service <see cref="ChangePassword"/>, this does
/// not require knowing the user's current password.
/// </summary>
public static class ResetPassword
{
    public sealed record Command(Guid UserId, string NewPassword) : IRequest<Unit>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }

    public sealed class Handler : IRequestHandler<Command, Unit>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IAuditLogService _auditLog;
        private readonly ICurrentUser _currentUser;

        public Handler(UserManager<AppUser> userManager, IAuditLogService auditLog, ICurrentUser currentUser)
        {
            _userManager = userManager;
            _auditLog = auditLog;
            _currentUser = currentUser;
        }

        public async ValueTask<Unit> Handle(Command req, CancellationToken ct)
        {
            var user = await _userManager.FindByIdAsync(req.UserId.ToString())
                ?? throw NotFoundException.For<AppUser>(req.UserId);

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, req.NewPassword);
            if (!result.Succeeded)
                throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));

            await _auditLog.RecordAsync(
                AuditEventType.PasswordReset,
                $"Password reset for user '{user.Email}' by an administrator.",
                subjectUserId: user.Id,
                subjectDisplay: user.Email,
                actorUserId: _currentUser.UserId,
                actorDisplay: _currentUser.UserName ?? _currentUser.Email,
                ct: ct);

            return Unit.Value;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/reset-password", async (
                Guid id, ResetPasswordRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new Command(id, request.NewPassword), ct);
            return Results.NoContent();
        })
        .WithSummary("Admin-reset a user's password (no current password required)")
        .RequireAuthorization(IdentityPermissions.Users.Edit);
}

public sealed record ResetPasswordRequest(string NewPassword);
