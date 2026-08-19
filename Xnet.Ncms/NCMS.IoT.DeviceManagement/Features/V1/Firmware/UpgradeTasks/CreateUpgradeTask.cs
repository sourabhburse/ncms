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

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.UpgradeTasks;

public static class CreateUpgradeTask
{
    public sealed record Command(
        Guid FirmwarePackageId,
        List<Guid> DeviceIds,
        string TaskName,
        decimal UpgradeDurationHours
    ) : IRequest<UpgradeTaskDetailDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.FirmwarePackageId).NotEmpty();
            RuleFor(x => x.DeviceIds).NotEmpty().WithMessage("At least one device must be selected.");
            RuleFor(x => x.TaskName).NotEmpty().MaximumLength(256);
        }
    }

    public sealed class Handler : IRequestHandler<Command, UpgradeTaskDetailDto>
    {
        private readonly DeviceManagementDbContext _db;

        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<UpgradeTaskDetailDto> Handle(Command cmd, CancellationToken ct)
        {
            var firmware = await _db.Firmwares.FindAsync([cmd.FirmwarePackageId], ct)
                ?? throw new KeyNotFoundException($"Firmware {cmd.FirmwarePackageId} not found.");
            if (!firmware.IsEnabled)
                throw new InvalidOperationException("Selected firmware package is disabled and cannot be used for upgrades.");

            // Application-layer pre-check: reject if any selected device is already in an ACTIVE
            // upgrade task — its own entry is non-terminal (Pending/InProgress) AND its parent task
            // is non-terminal (NotStarted/InProgress). Fast path with a clean, named message before
            // the DB write. The partial unique index ux_upgrade_task_devices_active_device is the
            // race-proof backstop for concurrent creators — see the SaveChanges catch below.
            var conflictMessage = await BuildActiveConflictMessageAsync(cmd.DeviceIds, ct);
            if (conflictMessage is not null)
                throw new InvalidOperationException(conflictMessage);

            var deviceVersionMap = await _db.Devices
                .Where(d => cmd.DeviceIds.Contains(d.Id))
                .Select(d => new { d.Id, d.FirmwareVersion })
                .ToDictionaryAsync(d => d.Id, d => d.FirmwareVersion, ct);

            var upgradeTimeout = cmd.UpgradeDurationHours > 0
                ? TimeSpan.FromHours((double)cmd.UpgradeDurationHours)
                : TimeSpan.FromHours(1);

            var task = UpgradeTask.Create(firmware.Id, cmd.TaskName, "system", upgradeTimeout);
            _db.UpgradeTasks.Add(task);

            var devices = cmd.DeviceIds.Select(deviceId =>
                UpgradeTaskDevice.Create(
                    task.Id,
                    deviceId,
                    deviceVersionMap.GetValueOrDefault(deviceId) ?? string.Empty,
                    firmware.Version)
            ).ToList();
            _db.UpgradeTaskDevices.AddRange(devices);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsActiveDeviceUniqueViolation(ex))
            {
                // Lost a race against a concurrent task creation between the pre-check and the
                // insert: the partial unique index rejected a device already taken. Re-derive the
                // conflict list and surface the same user-friendly message.
                var raceMessage = await BuildActiveConflictMessageAsync(cmd.DeviceIds, ct)
                    ?? "One or more selected devices were just assigned to another active firmware "
                       + "upgrade task. Please refresh the device list and try again.";
                throw new InvalidOperationException(raceMessage);
            }

            var detail = await new GetUpgradeTaskDetail.Handler(_db)
                .Handle(new GetUpgradeTaskDetail.Query(task.Id, 1, 50), ct);
            return detail!;
        }

        /// <summary>
        /// Builds the user-facing conflict message if any of <paramref name="deviceIds"/> is
        /// already in an active upgrade task, else null. Shared by the pre-check and the
        /// unique-violation (23505) handler so both surface identical wording.
        /// </summary>
        private async Task<string?> BuildActiveConflictMessageAsync(
            IReadOnlyCollection<Guid> deviceIds, CancellationToken ct)
        {
            var conflictingDeviceIds = await _db.UpgradeTaskDevices
                .Where(td => deviceIds.Contains(td.DeviceId)
                    && (td.Status == UpgradeDeviceStatus.Pending || td.Status == UpgradeDeviceStatus.InProgress)
                    && (td.UpgradeTask.Status == UpgradeTaskStatus.NotStarted
                        || td.UpgradeTask.Status == UpgradeTaskStatus.InProgress))
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

            return ActiveTaskConflictMessage.Build(serials, "firmware upgrade");
        }

        private static bool IsActiveDeviceUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: "23505" } pg
            && pg.ConstraintName == "ux_upgrade_task_devices_active_device";
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateUpgradeTaskRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var cmd = new Command(req.FirmwarePackageId, req.DeviceIds, req.TaskName, req.UpgradeDurationHours);
                var result = await sender.Send(cmd, ct);
                return Results.Created($"/api/v1/firmware/upgrade-tasks/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthorization(DeviceManagementPermissions.UpgradeTasks.Add)
        .WithSummary("Create a new upgrade task");
}
