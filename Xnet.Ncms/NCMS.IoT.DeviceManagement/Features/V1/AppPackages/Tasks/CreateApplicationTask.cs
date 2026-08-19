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

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Tasks;

public static class CreateApplicationTask
{
    public sealed record Command(
        ApplicationTaskAction Action,
        Guid ApplicationPackageVersionId,
        List<Guid> DeviceIds,
        string TaskName,
        decimal TimeoutHours
    ) : IRequest<ApplicationTaskDetailDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DeviceIds).NotEmpty().WithMessage("At least one device must be selected.");
            RuleFor(x => x.TaskName).NotEmpty().MaximumLength(256);
            RuleFor(x => x.ApplicationPackageVersionId).NotEmpty().WithMessage("An application package version must be specified.");
        }
    }

    public sealed class Handler : IRequestHandler<Command, ApplicationTaskDetailDto>
    {
        private readonly DeviceManagementDbContext _db;

        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationTaskDetailDto> Handle(Command cmd, CancellationToken ct)
        {
            var version = await _db.ApplicationPackageVersions.FindAsync([cmd.ApplicationPackageVersionId], ct)
                ?? throw new KeyNotFoundException($"Application package version {cmd.ApplicationPackageVersionId} not found.");
            EnsureDeployable(version);

            // Cross-domain guard: a device already running a firmware/config job must not
            // also receive an application deployment (see DeviceBusyGuard).
            var busyElsewhere = await DeviceBusyGuard.GetDevicesBusyElsewhereAsync(_db, cmd.DeviceIds, ct);
            if (busyElsewhere.Count > 0)
            {
                var serials = await SerialsForAsync(busyElsewhere, ct);
                throw new InvalidOperationException(ActiveTaskConflictMessage.Build(serials, "firmware/configuration"));
            }

            // Same-domain pre-check; the partial unique index is the race-proof backstop
            // for concurrent creators — see the SaveChanges catch below.
            var conflictMessage = await BuildActiveConflictMessageAsync(cmd.DeviceIds, ct);
            if (conflictMessage is not null) throw new InvalidOperationException(conflictMessage);

            // Compatibility precedence: Remove skips Product/Firmware compatibility (you can
            // always remove something already installed) but still requires the capability
            // flag. Install/Upgrade/Downgrade require full compatibility validation.
            if (cmd.Action == ApplicationTaskAction.Remove)
                await ValidateCapabilityOnlyAsync(cmd.DeviceIds, ct);
            else
                await ValidateCompatibilityAsync(cmd.DeviceIds, version.Id, ct);

            var timeout = cmd.TimeoutHours > 0 ? TimeSpan.FromHours((double)cmd.TimeoutHours) : TimeSpan.FromHours(1);
            var task = ApplicationTask.Create(cmd.TaskName, cmd.Action, version.Id, "system", timeout);
            _db.ApplicationTasks.Add(task);

            foreach (var deviceId in cmd.DeviceIds)
                _db.ApplicationTaskDevices.Add(ApplicationTaskDevice.Create(task.Id, deviceId, version.Id));

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsActiveDeviceUniqueViolation(ex))
            {
                var raceMessage = await BuildActiveConflictMessageAsync(cmd.DeviceIds, ct)
                    ?? "One or more selected devices were just assigned to another active application "
                       + "deployment task. Please refresh the device list and try again.";
                throw new InvalidOperationException(raceMessage);
            }

            var detail = await new GetApplicationTaskDetail.Handler(_db)
                .Handle(new GetApplicationTaskDetail.Query(task.Id, 1, 50), ct);
            return detail!;
        }

        private static void EnsureDeployable(ApplicationPackageVersion version)
        {
            if (!version.IsEnabled)
                throw new InvalidOperationException(
                    $"Application package version {version.Version} is disabled and cannot be used in a deployment.");
        }

        /// <summary>
        /// Product compatibility is mandatory — mirrors Firmware's own compatibility model
        /// exactly (Product-only, presence-only).
        /// </summary>
        private async Task ValidateCompatibilityAsync(List<Guid> deviceIds, Guid versionId, CancellationToken ct)
        {
            var devices = await _db.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .Select(d => new
                {
                    d.Id,
                    ProductId = d.HardwareInventory.ProductId,
                    SupportsSoftwarePackages = d.HardwareInventory.Product.SupportsSoftwarePackages
                })
                .ToListAsync(ct);

            var unsupported = devices.Where(d => !d.SupportsSoftwarePackages).Select(d => d.Id).ToHashSet();
            if (unsupported.Count > 0)
            {
                var serials = await SerialsForAsync(unsupported, ct);
                throw new InvalidOperationException(
                    $"{serials.Count} selected device(s) do not support application package management "
                    + $"(serial numbers: {string.Join(", ", serials)}).");
            }

            var compatibleProducts = (await _db.ApplicationPackageProductCompat
                .Where(c => c.ApplicationPackageVersionId == versionId)
                .Select(c => c.ProductId)
                .ToListAsync(ct)).ToHashSet();

            var incompatibleDeviceIds = devices
                .Where(d => !compatibleProducts.Contains(d.ProductId))
                .Select(d => d.Id)
                .ToHashSet();

            if (incompatibleDeviceIds.Count > 0)
            {
                var serials = await SerialsForAsync(incompatibleDeviceIds, ct);
                throw new InvalidOperationException(
                    $"{serials.Count} selected device(s) are not compatible with the selected package "
                    + $"(serial numbers: {string.Join(", ", serials)}).");
            }
        }

        private async Task ValidateCapabilityOnlyAsync(List<Guid> deviceIds, CancellationToken ct)
        {
            var unsupported = await _db.Devices
                .Where(d => deviceIds.Contains(d.Id) && !d.HardwareInventory.Product.SupportsSoftwarePackages)
                .Select(d => d.HardwareInventory.SerialNumber)
                .ToListAsync(ct);

            if (unsupported.Count > 0)
                throw new InvalidOperationException(
                    $"{unsupported.Count} selected device(s) do not support application package management "
                    + $"(serial numbers: {string.Join(", ", unsupported)}).");
        }

        private async Task<string?> BuildActiveConflictMessageAsync(IReadOnlyCollection<Guid> deviceIds, CancellationToken ct)
        {
            var conflictingDeviceIds = await _db.ApplicationTaskDevices
                .Where(td => deviceIds.Contains(td.DeviceId)
                    && (td.Status == ApplicationTaskDeviceStatus.Pending || td.Status == ApplicationTaskDeviceStatus.InProgress)
                    && (td.ApplicationTask.Status == ApplicationTaskStatus.NotStarted || td.ApplicationTask.Status == ApplicationTaskStatus.InProgress))
                .Select(td => td.DeviceId)
                .Distinct()
                .ToListAsync(ct);

            if (conflictingDeviceIds.Count == 0) return null;

            var serials = await SerialsForAsync(conflictingDeviceIds.ToHashSet(), ct);
            return ActiveTaskConflictMessage.Build(serials, "application deployment");
        }

        private async Task<List<string>> SerialsForAsync(IReadOnlyCollection<Guid> deviceIds, CancellationToken ct) =>
            await _db.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .Select(d => d.HardwareInventory.SerialNumber)
                .OrderBy(s => s)
                .ToListAsync(ct);

        private static bool IsActiveDeviceUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: "23505" } pg
            && pg.ConstraintName == "ux_application_task_devices_active_device";
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateApplicationTaskRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var cmd = new Command(
                    req.Action, req.ApplicationPackageVersionId,
                    req.DeviceIds, req.TaskName, req.TimeoutHours);
                var result = await sender.Send(cmd, ct);
                return Results.Created($"/api/v1/application/tasks/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationTasks.Add)
        .WithSummary("Create a new application deployment task");
}
