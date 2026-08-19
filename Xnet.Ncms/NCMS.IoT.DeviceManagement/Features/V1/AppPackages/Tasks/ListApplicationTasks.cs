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

public static class ListApplicationTasks
{
    public sealed record Query(
        int Page,
        int PageSize,
        string? Sort = null,
        string? Name = null,
        ApplicationTaskAction? Action = null,
        ApplicationTaskStatus? Status = null) : IRequest<ApplicationTaskPagedResult>;

    public sealed class Handler : IRequestHandler<Query, ApplicationTaskPagedResult>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationTaskPagedResult> Handle(Query q, CancellationToken ct)
        {
            var filtered = _db.ApplicationTasks.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q.Name)) filtered = filtered.Where(t => t.Name.Contains(q.Name));
            if (q.Action is { } action) filtered = filtered.Where(t => t.Action == action);
            if (q.Status is { } status) filtered = filtered.Where(t => t.Status == status);

            var total = await filtered.CountAsync(ct);

            var desc = q.Sort?.StartsWith('-') ?? false;
            var ordered = (q.Sort?.TrimStart('-', '+').ToLowerInvariant(), desc) switch
            {
                ("name", false) => filtered.OrderBy(t => t.Name),
                ("name", true) => filtered.OrderByDescending(t => t.Name),
                ("action", false) => filtered.OrderBy(t => t.Action),
                ("action", true) => filtered.OrderByDescending(t => t.Action),
                ("status", false) => filtered.OrderBy(t => t.Status),
                ("status", true) => filtered.OrderByDescending(t => t.Status),
                ("created", false) => filtered.OrderBy(t => t.CreatedAt),
                ("created", true) => filtered.OrderByDescending(t => t.CreatedAt),
                ("completed", false) => filtered.OrderBy(t => t.CompletedAt),
                ("completed", true) => filtered.OrderByDescending(t => t.CompletedAt),
                _ => filtered.OrderByDescending(t => t.CreatedAt)
            };
            var tasks = await ordered
                .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
                .ToListAsync(ct);

            var ids = tasks.Select(t => t.Id).ToList();
            var deviceCounts = await _db.ApplicationTaskDevices
                .Where(d => ids.Contains(d.ApplicationTaskId))
                .GroupBy(d => d.ApplicationTaskId)
                .Select(g => new
                {
                    TaskId = g.Key,
                    Total = g.Count(),
                    Succeeded = g.Count(d => d.Status == ApplicationTaskDeviceStatus.Succeeded)
                })
                .ToListAsync(ct);
            var countMap = deviceCounts.ToDictionary(x => x.TaskId, x => (x.Total, x.Succeeded));

            var versionIds = tasks.Select(t => t.TargetApplicationPackageVersionId).ToList();
            var versionNames = await _db.ApplicationPackageVersions
                .Include(v => v.ApplicationPackage)
                .Where(v => versionIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => $"{v.ApplicationPackage.Name} {v.Version}", ct);

            var items = tasks.Select(t =>
            {
                countMap.TryGetValue(t.Id, out var counts);
                var targetName = versionNames.GetValueOrDefault(t.TargetApplicationPackageVersionId, string.Empty);

                return new ApplicationTaskListItemDto(
                    t.Id, t.Name, t.Action, targetName,
                    counts.Succeeded, counts.Total,
                    t.Status, t.CreatedAt, t.CompletedAt);
            }).ToList();

            return new ApplicationTaskPagedResult(items, total, q.Page, q.PageSize);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (
                int page, int pageSize, string? sort,
                string? name, ApplicationTaskAction? action, ApplicationTaskStatus? status,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Query(page, pageSize, sort, name, action, status), ct)))
        .RequireAuthorization(DeviceManagementPermissions.ApplicationTasks.List)
        .WithSummary("List application tasks (filtered, sorted, paginated)");
}
