using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.UpgradeTasks;

public static class AbortUpgradeTaskJob
{
    public sealed record Command(Guid TaskId, Guid DeviceId) : ICommand;

    public sealed class Handler : ICommandHandler<Command, Unit>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<Unit> Handle(Command cmd, CancellationToken ct)
        {
            // cmd.DeviceId is the Device's GUID — match on the (task, device) pair,
            // not the UpgradeTaskDevice primary key.
            var device = await _db.UpgradeTaskDevices
                .FirstOrDefaultAsync(d => d.UpgradeTaskId == cmd.TaskId && d.DeviceId == cmd.DeviceId, ct)
                ?? throw new KeyNotFoundException($"Device {cmd.DeviceId} not found in task {cmd.TaskId}.");

            device.Skip("Aborted by operator.");

            // Skipping the last active device may complete the task — re-derive its status.
            await TaskReconciliation.ReconcileUpgradeTaskAsync(_db, cmd.TaskId, ct);

            await _db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/devices/{deviceId:guid}/abort",
            async (Guid id, Guid deviceId, ISender sender, CancellationToken ct) =>
            {
                try
                {
                    await sender.Send(new Command(id, deviceId), ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
        .RequireAuthorization(DeviceManagementPermissions.UpgradeTasks.Abort)
        .WithSummary("Skip a single device within an upgrade task");
}
