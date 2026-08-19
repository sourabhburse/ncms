using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Dashboard;

public static class GetDashboardTrends
{
    public sealed record Query(int Days) : IRequest<List<DashboardTrendPointDto>>;

    public sealed class Handler : IRequestHandler<Query, List<DashboardTrendPointDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<List<DashboardTrendPointDto>> Handle(Query q, CancellationToken ct)
        {
            var days = q.Days is 7 or 14 or 30 ? q.Days : 7;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var rangeStart = today.AddDays(-(days - 1));
            var rangeStartUtc = rangeStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            var createdDates = await _db.Devices
                .Where(d => d.CreatedAt >= rangeStartUtc)
                .Select(d => d.CreatedAt)
                .ToListAsync(ct);
            var seenDates = await _db.Devices
                .Where(d => d.LastSeenAt != null && d.LastSeenAt >= rangeStartUtc)
                .Select(d => d.LastSeenAt!.Value)
                .ToListAsync(ct);
            var totalByCreatedDate = await _db.Devices
                .Select(d => d.CreatedAt)
                .ToListAsync(ct);

            var points = new List<DashboardTrendPointDto>(days);
            for (var i = 0; i < days; i++)
            {
                var date = rangeStart.AddDays(i);

                var newDevices = createdDates.Count(c => DateOnly.FromDateTime(c.UtcDateTime) == date);
                var seenThatDay = seenDates.Count(s => DateOnly.FromDateTime(s.UtcDateTime) == date);
                var existingByThatDay = totalByCreatedDate.Count(c => DateOnly.FromDateTime(c.UtcDateTime) <= date);
                var onlineRate = existingByThatDay == 0 ? 0d : Math.Round(100d * seenThatDay / existingByThatDay, 1);

                points.Add(new DashboardTrendPointDto(date, newDevices, onlineRate));
            }

            return points;
        }
    }
}
