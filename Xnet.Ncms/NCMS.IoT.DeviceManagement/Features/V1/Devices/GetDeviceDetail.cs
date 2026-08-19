using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Devices;

public static class GetDeviceDetail
{
    public sealed record Query(Guid DeviceId) : IRequest<DeviceDetailDto?>;

    public sealed class Handler : IRequestHandler<Query, DeviceDetailDto?>
    {
        private const int RecentEventCount = 25;
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<DeviceDetailDto?> Handle(Query q, CancellationToken ct)
        {
            var device = await _db.Devices
                .Include(d => d.HardwareInventory)
                .Include(d => d.Certificates)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == q.DeviceId, ct);

            if (device is null)
                return null;

            var events = await _db.DeviceEvents.AsNoTracking()
                .Where(e => e.DeviceId == q.DeviceId)
                .OrderByDescending(e => e.Timestamp)
                .Take(RecentEventCount)
                .Select(e => new DeviceEventDto(e.Id, e.Timestamp, e.EventType, e.Severity, e.PayloadJson))
                .ToListAsync(ct);

            var certs = device.Certificates
                .OrderByDescending(c => c.IssuedAt)
                .Select(c => new DeviceCertificateSummaryDto(
                    c.Thumbprint, c.SubjectName, c.IssuedAt, c.ExpiresAt, c.IsActive))
                .ToList();

            return new DeviceDetailDto(
                device.Id,
                device.HardwareInventory.SerialNumber,
                device.HardwareModel,
                device.Name,
                device.FirmwareVersion,
                device.AgentVersion,
                device.IsOnline,
                device.Status,
                device.WanIpAddress,
                device.MacAddresses,
                device.Latitude,
                device.Longitude,
                device.Notes,
                device.LastSeenAt,
                device.ActivatedAt,
                device.CreatedAt,
                device.UpdatedAt,
                device.HardwareInventory.ProductId, // TenantId placeholder — use ProductId until tenant model is added
                certs,
                events);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new Query(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithSummary("Get device detail with certificates and recent events")
        .RequireAuthorization(DeviceManagementPermissions.Devices.View);
}
