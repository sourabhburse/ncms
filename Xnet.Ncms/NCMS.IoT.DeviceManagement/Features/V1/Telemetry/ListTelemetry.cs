using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Telemetry;

public static class ListTelemetry
{
    public sealed record Query(Guid? DeviceId, int Page, int PageSize,
        string? SerialNumber = null, DateTimeOffset? StartDate = null, DateTimeOffset? EndDate = null)
        : IRequest<TelemetryPagedResult>;

    public sealed class Handler : IRequestHandler<Query, TelemetryPagedResult>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<TelemetryPagedResult> Handle(Query q, CancellationToken ct)
        {
            var page = q.Page < 1 ? 1 : q.Page;
            var pageSize = q.PageSize is < 1 or > 200 ? 25 : q.PageSize;

            var query = _db.TelemetryRecords
                .Include(t => t.Device).ThenInclude(d => d.HardwareInventory)
                .AsNoTracking();
            if (q.DeviceId.HasValue)
                query = query.Where(t => t.DeviceId == q.DeviceId.Value);
            if (!string.IsNullOrWhiteSpace(q.SerialNumber))
                query = query.Where(t => EF.Functions.Like(t.Device.HardwareInventory.SerialNumber, $"%{q.SerialNumber.Trim()}%"));
            if (q.StartDate.HasValue)
            {
                var startUtc = q.StartDate.Value.ToUniversalTime();
                query = query.Where(t => t.Timestamp >= startUtc);
            }
            if (q.EndDate.HasValue)
            {
                // Exclusive upper bound: caller passes the start of the day *after* the
                // selected end date, so the whole end date is included.
                var endUtc = q.EndDate.Value.ToUniversalTime();
                query = query.Where(t => t.Timestamp < endUtc);
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(t => t.Timestamp)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(t => new TelemetryRecordDto(
                    t.Id,
                    t.DeviceId,
                    t.Device.HardwareInventory.SerialNumber,
                    t.Timestamp,
                    t.Topic,
                    t.QosLevel,
                    t.PayloadJson))
                .ToListAsync(ct);

            return new TelemetryPagedResult(items, total, page, pageSize);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (Guid? deviceId, string? serialNumber, int? page, int? pageSize,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Query(deviceId, page ?? 1, pageSize ?? 25, serialNumber), ct)))
        .WithSummary("List telemetry records (paginated)")
        .RequireAuthorization(DeviceManagementPermissions.Devices.View);
}
