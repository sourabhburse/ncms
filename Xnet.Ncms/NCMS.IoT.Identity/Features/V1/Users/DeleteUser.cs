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

public static class DeleteUser
{
    public sealed record Command(Guid Id) : IRequest<Unit>;

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
            var user = await _userManager.FindByIdAsync(req.Id.ToString())
                ?? throw NotFoundException.For<AppUser>(req.Id);

            var email = user.Email;
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));

            await _auditLog.RecordAsync(
                AuditEventType.UserDeleted,
                $"User '{email}' was deleted.",
                subjectUserId: req.Id,
                subjectDisplay: email,
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
        .WithSummary("Delete a user")
        .RequireAuthorization(IdentityPermissions.Users.Delete);
}
