using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.Persistence.Pagination;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.UpgradeTasks;

/// <summary>
/// Lists upgrade (firmware) tasks with server-side name/date-range filtering, whitelisted
/// sorting, and shared pagination. Per-task device counts are aggregated in a second query
/// keyed by the current page's task ids.
/// </summary>
public static class ListUpgradeTasks
{
    public sealed record Query(
        string? Name,
        DateTimeOffset? StartDate,
        DateTimeOffset? EndDate,
        string? Sort,
        int Page,
        int PageSize) : IRequest<PagedResponse<UpgradeTaskListItemDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<UpgradeTaskListItemDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<PagedResponse<UpgradeTaskListItemDto>> Handle(Query q, CancellationToken ct)
        {
            var spec = new UpgradeTasksSpecification(
                new UpgradeTaskFilter(q.Name, q.StartDate, q.EndDate, q.Sort));

            var paged = await _db.UpgradeTasks
                .ApplySpecification(spec)
                .ToPagedResponseAsync(new PagedQuery(q.Page, q.PageSize), ct);

            var ids = paged.Items.Select(r => r.Id).ToList();
            var deviceCounts = await _db.UpgradeTaskDevices
                .Where(d => ids.Contains(d.UpgradeTaskId))
                .GroupBy(d => d.UpgradeTaskId)
                .Select(g => new
                {
                    TaskId = g.Key,
                    Total = g.Count(),
                    Succeeded = g.Count(d => d.Status == UpgradeDeviceStatus.Succeeded)
                })
                .ToListAsync(ct);
            var countMap = deviceCounts.ToDictionary(x => x.TaskId, x => (x.Total, x.Succeeded));

            var items = paged.Items.Select(r =>
            {
                countMap.TryGetValue(r.Id, out var counts);
                return new UpgradeTaskListItemDto(
                    r.Id, r.Name, r.FirmwareVersion,
                    counts.Succeeded, counts.Total,
                    r.Status, r.CreatedAt, r.CompletedAt);
            }).ToList();

            return new PagedResponse<UpgradeTaskListItemDto>
            {
                Items = items,
                PageNumber = paged.PageNumber,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount,
                TotalPages = paged.TotalPages
            };
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (
                string? name, DateTimeOffset? startDate, DateTimeOffset? endDate, string? sort,
                int? page, int? pageSize, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new Query(name, startDate, endDate, sort, page ?? 1, pageSize ?? 25), ct)))
        .RequireAuthorization(DeviceManagementPermissions.UpgradeTasks.List)
        .WithSummary("List upgrade tasks (filtered, sorted, paginated)");
}
