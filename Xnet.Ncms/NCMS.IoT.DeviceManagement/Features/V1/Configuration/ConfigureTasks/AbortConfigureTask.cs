using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.ConfigureTasks;

public static class AbortConfigureTask
{
    public sealed record Command(Guid TaskId) : ICommand;

    public sealed class Handler : ICommandHandler<Command, Unit>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<Unit> Handle(Command cmd, CancellationToken ct)
        {
            var task = await _db.ConfigureTasks.FindAsync([cmd.TaskId], ct)
                ?? throw new KeyNotFoundException($"Config task {cmd.TaskId} not found.");

            task.Cancel();

            // Terminate every device still waiting to config: not yet dispatched, or
            // dispatched but not yet acknowledged (e.g. offline).
            var skippableDevices = await _db.ConfigureTaskDevices
                .Where(d => d.ConfigureTaskId == cmd.TaskId
                    && (d.Status == DeviceConfigStatus.NotStarted
                        || (d.Status == DeviceConfigStatus.ConfigInProgress && d.AcknowledgedAt == null)))
                .ToListAsync(ct);

            foreach (var device in skippableDevices)
                device.Skip("Task cancelled by operator.");

            // No Reconcile() here: Cancelled is sticky operator intent and is never overwritten.
            await _db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/abort", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            try
            {
                await sender.Send(new Command(id), ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ConfigTasks.Terminate)
        .WithSummary("Abort a config task");
}
