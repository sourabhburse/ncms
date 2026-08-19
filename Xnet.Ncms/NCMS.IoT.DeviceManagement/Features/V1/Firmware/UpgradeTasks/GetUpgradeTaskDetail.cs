using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.UpgradeTasks;

public static class GetUpgradeTaskDetail
{
    public sealed record Query(Guid Id, int Page, int PageSize) : IRequest<UpgradeTaskDetailDto?>;

    public sealed class Handler : IRequestHandler<Query, UpgradeTaskDetailDto?>
    {
        private readonly DeviceManagementDbContext _db;

        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<UpgradeTaskDetailDto?> Handle(Query q, CancellationToken ct)
        {
            var task = await _db.UpgradeTasks
                .Include(t => t.Firmware)
                .FirstOrDefaultAsync(t => t.Id == q.Id, ct);
            if (task is null) return null;

            var totalDevices = await _db.UpgradeTaskDevices.CountAsync(d => d.UpgradeTaskId == q.Id, ct);
            var taskDevices = await _db.UpgradeTaskDevices
                .Where(d => d.UpgradeTaskId == q.Id)
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
                    // Product Model = the linked product catalog name (not the device-reported HardwareModel).
                    ProductModel = d.HardwareInventory.Product.Name,
                    d.FirmwareVersion
                })
                .ToDictionaryAsync(d => d.Id, ct);

            // Progress counts from all devices in the task (not just current page)
            var allStatuses = await _db.UpgradeTaskDevices
                .Where(d => d.UpgradeTaskId == q.Id)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Pending    = g.Count(d => d.Status == UpgradeDeviceStatus.Pending),
                    InProgress = g.Count(d => d.Status == UpgradeDeviceStatus.InProgress),
                    Succeeded  = g.Count(d => d.Status == UpgradeDeviceStatus.Succeeded),
                    Failed     = g.Count(d => d.Status == UpgradeDeviceStatus.Failed),
                    TimedOut   = g.Count(d => d.Status == UpgradeDeviceStatus.TimedOut),
                    Skipped    = g.Count(d => d.Status == UpgradeDeviceStatus.Skipped),
                })
                .FirstOrDefaultAsync(ct);

            var progress = new UpgradeTaskProgressDto(
                totalDevices,
                allStatuses?.Pending    ?? 0,
                allStatuses?.InProgress ?? 0,
                allStatuses?.Succeeded  ?? 0,
                allStatuses?.Failed     ?? 0,
                allStatuses?.TimedOut   ?? 0,
                allStatuses?.Skipped    ?? 0);

            var durationHours = task.UpgradeTimeout.HasValue
                ? (decimal)task.UpgradeTimeout.Value.TotalHours
                : 1m;

            var deviceDtos = taskDevices.Select((d, idx) =>
            {
                deviceInfo.TryGetValue(d.DeviceId, out var info);
                return new UpgradeTaskDeviceDto(
                    (q.Page - 1) * q.PageSize + idx + 1,
                    d.DeviceId,
                    info?.SerialNumber ?? d.DeviceId.ToString(),
                    info?.SerialNumber ?? string.Empty,
                    info?.ProductModel ?? string.Empty,
                    d.PreviousFirmwareVersion,
                    info?.FirmwareVersion,
                    d.TargetFirmwareVersion,
                    ToEscalationState(d),
                    d.AttemptCount,
                    d.DispatchedAt,
                    d.CompletedAt,
                    d.ErrorCode,
                    // FailureReason is set for every terminal failure (Failed/TimedOut/Skipped);
                    // ErrorMessage is only set by Failed/TimedOut. Prefer FailureReason so a
                    // skipped/terminated device's reason ("Aborted by operator.") is shown.
                    d.FailureReason ?? d.ErrorMessage);
            }).ToList();

            return new UpgradeTaskDetailDto(
                task.Id, task.Name,
                task.Firmware?.Name ?? string.Empty,
                task.Firmware?.Version ?? string.Empty,
                totalDevices, durationHours,
                task.CreatedAt, task.StartedAt, task.CompletedAt,
                task.Status, task.CreatedBy,
                progress, deviceDtos);
        }

        private static EscalationState ToEscalationState(Entities.UpgradeTaskDevice d) => d.Status switch
        {
            UpgradeDeviceStatus.Pending    => EscalationState.WaitingToDownload,
            // Dispatched but the device hasn't acknowledged yet (e.g. offline) → still waiting.
            UpgradeDeviceStatus.InProgress => d.AcknowledgedAt is null
                                                ? EscalationState.WaitingToDownload
                                                : EscalationState.Downloading,
            UpgradeDeviceStatus.Succeeded  => EscalationState.UpgradeSucceeded,
            UpgradeDeviceStatus.Failed     => EscalationState.UpgradeFailure,
            UpgradeDeviceStatus.TimedOut   => EscalationState.UpgradeTimeout,
            UpgradeDeviceStatus.Skipped    => EscalationState.Terminated,
            _                              => EscalationState.WaitingToDownload
        };
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, int page, int pageSize, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new Query(id, page, pageSize), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization(DeviceManagementPermissions.UpgradeTasks.View)
        .WithSummary("Get upgrade task detail with per-device escalation state");
}
