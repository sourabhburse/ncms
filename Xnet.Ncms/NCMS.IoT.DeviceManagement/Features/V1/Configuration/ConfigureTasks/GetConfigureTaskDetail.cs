using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.ConfigureTasks;

public static class GetConfigureTaskDetail
{
    public sealed record Query(Guid Id, int Page, int PageSize) : IRequest<ConfigureTaskDetailDto?>;

    public sealed class Handler : IRequestHandler<Query, ConfigureTaskDetailDto?>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ConfigureTaskDetailDto?> Handle(Query q, CancellationToken ct)
        {
            var task = await _db.ConfigureTasks
                .Include(t => t.Profile).ThenInclude(p => p.Product)
                .FirstOrDefaultAsync(t => t.Id == q.Id, ct);
            if (task is null) return null;

            var totalDevices = await _db.ConfigureTaskDevices.CountAsync(d => d.ConfigureTaskId == q.Id, ct);
            var taskDevices = await _db.ConfigureTaskDevices
                .Where(d => d.ConfigureTaskId == q.Id)
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
                    Name = d.Name,
                    ProductModel = d.HardwareInventory.Product.Name
                })
                .ToDictionaryAsync(d => d.Id, ct);

            var counts = await _db.ConfigureTaskDevices
                .Where(d => d.ConfigureTaskId == q.Id)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    NotStarted = g.Count(d => d.Status == DeviceConfigStatus.NotStarted),
                    InProgress = g.Count(d => d.Status == DeviceConfigStatus.ConfigInProgress),
                    Complete   = g.Count(d => d.Status == DeviceConfigStatus.ConfigComplete),
                    Terminated = g.Count(d => d.Status == DeviceConfigStatus.TerminationConfig),
                })
                .FirstOrDefaultAsync(ct);

            var progress = new ConfigureTaskProgressDto(
                totalDevices,
                counts?.NotStarted ?? 0,
                counts?.InProgress ?? 0,
                counts?.Complete   ?? 0,
                counts?.Terminated ?? 0);

            var durationHours = task.ConfigTimeout.HasValue
                ? (decimal)task.ConfigTimeout.Value.TotalHours
                : 1m;

            var deviceDtos = taskDevices.Select((d, idx) =>
            {
                deviceInfo.TryGetValue(d.DeviceId, out var info);
                var name = info is null
                    ? d.DeviceId.ToString()
                    : (string.IsNullOrWhiteSpace(info.Name) ? info.SerialNumber : info.Name);
                return new ConfigureTaskDeviceDto(
                    (q.Page - 1) * q.PageSize + idx + 1,
                    d.DeviceId,
                    name,
                    info?.ProductModel ?? string.Empty,
                    ToConfigState(d),
                    d.AttemptCount,
                    d.DispatchedAt,
                    d.CompletedAt,
                    d.FailureReason ?? d.ErrorMessage);
            }).ToList();

            return new ConfigureTaskDetailDto(
                task.Id, task.TaskNumber, task.ProfileName, task.Profile?.Product?.Name ?? string.Empty,
                totalDevices, durationHours,
                task.CreatedAt, task.StartedAt, task.CompletedAt,
                task.Status, task.CreatedBy,
                progress, deviceDtos);
        }

        private static ConfigDeviceState ToConfigState(Entities.ConfigureTaskDevice d) => d.Status switch
        {
            DeviceConfigStatus.NotStarted => ConfigDeviceState.WaitingToConfig,
            // Dispatched but not yet acknowledged (e.g. offline) → still waiting.
            DeviceConfigStatus.ConfigInProgress => d.AcknowledgedAt is null
                                                    ? ConfigDeviceState.WaitingToConfig
                                                    : ConfigDeviceState.Configuring,
            DeviceConfigStatus.ConfigComplete => ConfigDeviceState.ConfigSucceeded,
            DeviceConfigStatus.TerminationConfig => ConfigDeviceState.Terminated,
            _ => ConfigDeviceState.WaitingToConfig
        };
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, int page, int pageSize, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new Query(id, page, pageSize), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization(DeviceManagementPermissions.ConfigTasks.View)
        .WithSummary("Get config task detail with per-device state");
}
