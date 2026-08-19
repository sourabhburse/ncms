using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Dashboard;

public static class GetDashboardSummary
{
    public sealed record Query : IRequest<DashboardSummaryDto>;

    public sealed class Handler : IRequestHandler<Query, DashboardSummaryDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<DashboardSummaryDto> Handle(Query q, CancellationToken ct)
        {
            var since = DateTimeOffset.UtcNow.AddHours(-24);
            var inactiveSince = DateTimeOffset.UtcNow.AddDays(-7);

            var totalDevices = await _db.Devices.CountAsync(ct);
            var onlineDevices = await _db.Devices.CountAsync(d => d.IsOnline, ct);
            var totalHardware = await _db.HardwareInventory.CountAsync(ct);
            var unprovisioned = await _db.HardwareInventory.CountAsync(h => !h.IsProvisioned, ct);
            var publishedPackages = await _db.Firmwares.CountAsync(ct);
            var activeCampaigns = await _db.UpgradeTasks
                .CountAsync(t => t.Status == UpgradeTaskStatus.InProgress, ct);
            var recentTelemetry = await _db.TelemetryRecords
                .CountAsync(t => t.Timestamp >= since, ct);

            // Offline: expected to communicate (has checked in within the inactivity window) but
            // currently unreachable — a live problem (network/power/comms failure).
            var offlineDevices = await _db.Devices
                .CountAsync(d => !d.IsOnline && d.LastSeenAt != null && d.LastSeenAt >= inactiveSince, ct);

            // Inactive: not expected to communicate — never checked in, or dormant well past the
            // inactivity window (disabled, decommissioned, under maintenance, not yet activated).
            var inactiveDevices = await _db.Devices
                .CountAsync(d => !d.IsOnline && (d.LastSeenAt == null || d.LastSeenAt < inactiveSince), ct);

            return new DashboardSummaryDto(
                totalDevices, onlineDevices, totalHardware, unprovisioned,
                publishedPackages, activeCampaigns, recentTelemetry,
                offlineDevices, inactiveDevices);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/summary", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Query(), ct)))
        .WithSummary("Get dashboard summary counts")
        .RequireAuthorization();
}
