using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Entities;
using NCMS.IoT.Identity.Services;
using NCMS.Shared.Exceptions;

namespace NCMS.IoT.Identity.Features.V1.Users;

public static class ChangePassword
{
    public sealed record Command(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Unit>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CurrentPassword).NotEmpty();
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
        }
    }

    public sealed class Handler : IRequestHandler<Command, Unit>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IAuditLogService _auditLog;

        public Handler(UserManager<AppUser> userManager, IAuditLogService auditLog)
        {
            _userManager = userManager;
            _auditLog = auditLog;
        }

        public async ValueTask<Unit> Handle(Command req, CancellationToken ct)
        {
            var user = await _userManager.FindByIdAsync(req.UserId.ToString())
                ?? throw NotFoundException.For<AppUser>(req.UserId);

            var result = await _userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
            if (!result.Succeeded)
                throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));

            await _auditLog.RecordAsync(
                AuditEventType.PasswordChanged,
                $"User '{user.Email}' changed their own password.",
                subjectUserId: user.Id,
                subjectDisplay: user.Email,
                actorUserId: user.Id,
                actorDisplay: user.Email,
                ct: ct);

            return Unit.Value;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/change-password", async (
                Guid id, ChangePasswordRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new Command(id, request.CurrentPassword, request.NewPassword), ct);
            return Results.NoContent();
        })
        .WithSummary("Change a user's own password")
        .RequireAuthorization();
}
