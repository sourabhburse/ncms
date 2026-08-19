using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;
using Npgsql;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.ConfigureTasks;

public static class CreateConfigureTask
{
    public sealed record Command(
        Guid ProfileId,
        List<Guid> DeviceIds,
        decimal ConfigDurationHours
    ) : IRequest<ConfigureTaskDetailDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ProfileId).NotEmpty();
            RuleFor(x => x.DeviceIds).NotEmpty().WithMessage("At least one device must be selected.");
        }
    }

    public sealed class Handler : IRequestHandler<Command, ConfigureTaskDetailDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ConfigureTaskDetailDto> Handle(Command cmd, CancellationToken ct)
        {
            var profile = await _db.ConfigProfiles.FindAsync([cmd.ProfileId], ct)
                ?? throw new KeyNotFoundException($"Config profile {cmd.ProfileId} not found.");
            if (profile.Status != ProfileStatus.Enable)
                throw new InvalidOperationException("Selected config profile is disabled and cannot be used.");

            // Application-layer pre-check (mirrors CreateUpgradeTask): reject if any selected device
            // is already in an ACTIVE config task. The partial unique index
            // ux_configure_task_devices_active_device is the race-proof backstop — see SaveChanges.
            var conflictMessage = await BuildActiveConflictMessageAsync(cmd.DeviceIds, ct);
            if (conflictMessage is not null)
                throw new InvalidOperationException(conflictMessage);

            var configTimeout = cmd.ConfigDurationHours > 0
                ? TimeSpan.FromHours((double)cmd.ConfigDurationHours)
                : TimeSpan.FromHours(1);

            var task = ConfigureTask.Create(profile, "system", configTimeout);
            _db.ConfigureTasks.Add(task);

            var devices = cmd.DeviceIds
                .Distinct()
                .Select(deviceId => ConfigureTaskDevice.Create(task.Id, deviceId))
                .ToList();
            _db.ConfigureTaskDevices.AddRange(devices);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsActiveDeviceUniqueViolation(ex))
            {
                // Lost a race against a concurrent task creation: the partial unique index rejected
                // a device already taken. Re-derive the conflict list, surface the same message.
                var raceMessage = await BuildActiveConflictMessageAsync(cmd.DeviceIds, ct)
                    ?? "One or more selected devices were just assigned to another active "
                       + "configuration task. Please refresh the device list and try again.";
                throw new InvalidOperationException(raceMessage);
            }

            var detail = await new GetConfigureTaskDetail.Handler(_db)
                .Handle(new GetConfigureTaskDetail.Query(task.Id, 1, 50), ct);
            return detail!;
        }

        /// <summary>
        /// Builds the user-facing conflict message if any of <paramref name="deviceIds"/> is
        /// already in an active config task, else null. Shared by the pre-check and the
        /// unique-violation (23505) handler so both surface identical wording.
        /// </summary>
        private async Task<string?> BuildActiveConflictMessageAsync(
            IReadOnlyCollection<Guid> deviceIds, CancellationToken ct)
        {
            var conflictingDeviceIds = await _db.ConfigureTaskDevices
                .Where(td => deviceIds.Contains(td.DeviceId)
                    && (td.Status == DeviceConfigStatus.NotStarted || td.Status == DeviceConfigStatus.ConfigInProgress)
                    && (td.ConfigureTask.Status == ConfigureTaskStatus.NotStarted
                        || td.ConfigureTask.Status == ConfigureTaskStatus.InProgress))
                .Select(td => td.DeviceId)
                .Distinct()
                .ToListAsync(ct);

            if (conflictingDeviceIds.Count == 0) return null;

            // Identify conflicts by serial number (HardwareInventory), not internal GUID.
            var serials = await _db.Devices
                .Where(d => conflictingDeviceIds.Contains(d.Id))
                .Select(d => d.HardwareInventory.SerialNumber)
                .OrderBy(s => s)
                .ToListAsync(ct);

            return ActiveTaskConflictMessage.Build(serials, "configuration");
        }

        private static bool IsActiveDeviceUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: "23505" } pg
            && pg.ConstraintName == "ux_configure_task_devices_active_device";
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateConfigureTaskRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var cmd = new Command(req.ProfileId, req.DeviceIds, req.ConfigDurationHours);
                var result = await sender.Send(cmd, ct);
                return Results.Created($"/api/v1/config/tasks/{result.Id}", result);
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ConfigTasks.Add)
        .WithSummary("Create a new config task");
}
