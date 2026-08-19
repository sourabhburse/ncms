using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;

public static class DeleteProfile
{
    public sealed record Command(Guid Id) : IRequest<Unit>;

    public sealed class Handler : IRequestHandler<Command, Unit>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<Unit> Handle(Command cmd, CancellationToken ct)
        {
            var profile = await _db.ConfigProfiles.FindAsync([cmd.Id], ct)
                ?? throw new KeyNotFoundException($"Config profile {cmd.Id} not found.");

            if (profile.Status == ProfileStatus.Enable)
                throw new InvalidOperationException("Disable the profile before deleting it.");

            var usedByTask = await _db.ConfigureTasks.AnyAsync(t => t.ProfileId == cmd.Id, ct);
            if (usedByTask)
                throw new InvalidOperationException(
                    "This profile is referenced by one or more config tasks and cannot be deleted.");

            _db.ConfigProfiles.Remove(profile);
            await _db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            try
            {
                await sender.Send(new Command(id), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ConfigProfiles.Delete)
        .WithSummary("Delete a config profile");
}
