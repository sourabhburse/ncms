using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Telemetry;

public static class ListDeviceTelemetry
{
    public sealed record Query(
        Guid DeviceId,
        string? SerialNumber,
        DateTimeOffset? Before,
        int Page,
        int PageSize
    ) : IRequest<DeviceTelemetryPageResult>;

    public sealed class Handler : IRequestHandler<Query, DeviceTelemetryPageResult>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<DeviceTelemetryPageResult> Handle(Query q, CancellationToken ct)
        {
            var page = q.Page < 1 ? 1 : q.Page;
            var pageSize = q.PageSize is < 1 or > 200 ? 50 : q.PageSize;

            var query = _db.DeviceTelemetries
                .AsNoTracking()
                .Where(t => t.DeviceId == q.DeviceId);

            if (!string.IsNullOrWhiteSpace(q.SerialNumber))
                query = query.Where(t => EF.Functions.Like(t.Device.HardwareInventory.SerialNumber, $"%{q.SerialNumber.Trim()}%"));

            if (q.Before.HasValue)
            {
                var beforeUtc = q.Before.Value.ToUniversalTime();
                query = query.Where(t => t.Timestamp < beforeUtc);
            }

            var items = await query
                .OrderByDescending(t => t.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => GetLatestDeviceTelemetry.ToDto(t))
                .ToListAsync(ct);

            return new DeviceTelemetryPageResult(items, page, pageSize);
        }
    }
}
