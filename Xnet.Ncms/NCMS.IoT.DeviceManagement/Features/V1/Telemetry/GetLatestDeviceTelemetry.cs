using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Telemetry;

public static class GetLatestDeviceTelemetry
{
    public sealed record Query(Guid? DeviceId, string? SerialNumber) : IRequest<DeviceTelemetryDto?>;

    public sealed class Handler : IRequestHandler<Query, DeviceTelemetryDto?>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<DeviceTelemetryDto?> Handle(Query q, CancellationToken ct)
        {
            IQueryable<Entities.DeviceTelemetry> query;

            if (q.DeviceId.HasValue)
            {
                query = _db.DeviceTelemetries.Where(t => t.DeviceId == q.DeviceId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(q.SerialNumber))
            {
                query = _db.DeviceTelemetries
                    .Where(t => t.Device.HardwareInventory.SerialNumber == q.SerialNumber.Trim());
            }
            else
            {
                return null;
            }

            var t = await query
                .AsNoTracking()
                .OrderByDescending(t => t.Timestamp)
                .FirstOrDefaultAsync(ct);

            return t is null ? null : ToDto(t);
        }
    }

    internal static DeviceTelemetryDto ToDto(Entities.DeviceTelemetry t) => new(
        t.Id, t.DeviceId, t.Timestamp,
        t.CpuUsagePercent, t.RamUsageMb, t.RamTotalMb,
        t.StorageUsedMb, t.StorageTotalMb, t.UptimeSeconds,
        t.WanIp, t.TemperatureCelsius, t.SignalStrengthRssi, t.SignalQualityRsrp);
}
