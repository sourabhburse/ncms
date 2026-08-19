using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Tasks;

public static class GetApplicationTaskDetail
{
    public sealed record Query(Guid Id, int Page, int PageSize) : IRequest<ApplicationTaskDetailDto?>;

    public sealed class Handler : IRequestHandler<Query, ApplicationTaskDetailDto?>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationTaskDetailDto?> Handle(Query q, CancellationToken ct)
        {
            var task = await _db.ApplicationTasks
                .Include(t => t.TargetApplicationPackageVersion).ThenInclude(v => v.ApplicationPackage)
                .FirstOrDefaultAsync(t => t.Id == q.Id, ct);
            if (task is null) return null;

            var targetName = $"{task.TargetApplicationPackageVersion.ApplicationPackage.Name} {task.TargetApplicationPackageVersion.Version}";

            var totalDevices = await _db.ApplicationTaskDevices.CountAsync(d => d.ApplicationTaskId == q.Id, ct);
            var taskDevices = await _db.ApplicationTaskDevices
                .Include(d => d.ApplicationPackageVersion).ThenInclude(v => v.ApplicationPackage)
                .Where(d => d.ApplicationTaskId == q.Id)
                .OrderBy(d => d.Id)
                .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
                .ToListAsync(ct);

            var deviceIds = taskDevices.Select(d => d.DeviceId).ToList();
            var deviceInfo = await _db.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .Select(d => new
                {
                    d.Id,
                    SerialNumber = d.HardwareInventory.SerialNumber,
                    ProductModel = d.HardwareInventory.Product.Name
                })
                .ToDictionaryAsync(d => d.Id, ct);

            var allStatuses = await _db.ApplicationTaskDevices
                .Where(d => d.ApplicationTaskId == q.Id)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Pending    = g.Count(d => d.Status == ApplicationTaskDeviceStatus.Pending),
                    InProgress = g.Count(d => d.Status == ApplicationTaskDeviceStatus.InProgress),
                    Succeeded  = g.Count(d => d.Status == ApplicationTaskDeviceStatus.Succeeded),
                    Failed     = g.Count(d => d.Status == ApplicationTaskDeviceStatus.Failed),
                    TimedOut   = g.Count(d => d.Status == ApplicationTaskDeviceStatus.TimedOut),
                    Skipped    = g.Count(d => d.Status == ApplicationTaskDeviceStatus.Skipped),
                })
                .FirstOrDefaultAsync(ct);

            var progress = new ApplicationTaskProgressDto(
                totalDevices,
                allStatuses?.Pending    ?? 0,
                allStatuses?.InProgress ?? 0,
                allStatuses?.Succeeded  ?? 0,
                allStatuses?.Failed     ?? 0,
                allStatuses?.TimedOut   ?? 0,
                allStatuses?.Skipped    ?? 0);

            var timeoutHours = task.Timeout.HasValue ? (decimal)task.Timeout.Value.TotalHours : 1m;

            var deviceDtos = taskDevices.Select((d, idx) =>
            {
                deviceInfo.TryGetValue(d.DeviceId, out var info);

                return new ApplicationTaskDeviceDto(
                    (q.Page - 1) * q.PageSize + idx + 1,
                    d.DeviceId,
                    info?.SerialNumber ?? d.DeviceId.ToString(),
                    info?.ProductModel ?? string.Empty,
                    ToEscalationState(d),
                    d.AttemptCount,
                    d.DispatchedAt,
                    d.CompletedAt,
                    d.ErrorCode,
                    d.FailureReason ?? d.ErrorMessage,
                    d.ApplicationPackageVersionId,
                    d.ApplicationPackageVersion.ApplicationPackage.Name,
                    d.ApplicationPackageVersion.Version);
            }).ToList();

            return new ApplicationTaskDetailDto(
                task.Id, task.Name, task.Action, targetName,
                totalDevices, timeoutHours,
                task.CreatedAt, task.StartedAt, task.CompletedAt,
                task.Status, task.CreatedBy,
                progress, deviceDtos);
        }

        private static ApplicationEscalationState ToEscalationState(Entities.ApplicationTaskDevice d) => d.Status switch
        {
            ApplicationTaskDeviceStatus.Pending    => ApplicationEscalationState.WaitingToStart,
            // Dispatched but the device hasn't acknowledged yet (e.g. offline) → still waiting.
            // Mirrors UpgradeTaskDevice's identical AcknowledgedAt-gated distinction.
            ApplicationTaskDeviceStatus.InProgress => d.AcknowledgedAt is null
                                                        ? ApplicationEscalationState.WaitingToStart
                                                        : ApplicationEscalationState.Downloading,
            ApplicationTaskDeviceStatus.Succeeded  => ApplicationEscalationState.Succeeded,
            ApplicationTaskDeviceStatus.Failed      => ApplicationEscalationState.Failed,
            ApplicationTaskDeviceStatus.TimedOut   => ApplicationEscalationState.TimedOut,
            ApplicationTaskDeviceStatus.Skipped    => ApplicationEscalationState.Terminated,
            _                                    => ApplicationEscalationState.WaitingToStart
        };
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, int page, int pageSize, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new Query(id, page, pageSize), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationTasks.View)
        .WithSummary("Get application task detail with per-device status");
}
