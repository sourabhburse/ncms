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

public static class ListConfigureTasks
{
    public sealed record Query(
        int Page,
        int PageSize,
        string? Sort = null,
        string? TaskNumber = null,
        Guid? ProductId = null,
        ConfigureTaskStatus? Status = null) : IRequest<ConfigureTaskPagedResult>;

    public sealed class Handler : IRequestHandler<Query, ConfigureTaskPagedResult>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ConfigureTaskPagedResult> Handle(Query q, CancellationToken ct)
        {
            var baseQuery = _db.ConfigureTasks.Include(t => t.Profile).ThenInclude(p => p.Product).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q.TaskNumber)) baseQuery = baseQuery.Where(t => t.TaskNumber.Contains(q.TaskNumber));
            if (q.ProductId is { } productId) baseQuery = baseQuery.Where(t => t.Profile.ProductId == productId);
            if (q.Status is { } status) baseQuery = baseQuery.Where(t => t.Status == status);

            var total = await baseQuery.CountAsync(ct);

            var desc = q.Sort?.StartsWith('-') ?? false;
            var ordered = (q.Sort?.TrimStart('-', '+').ToLowerInvariant(), desc) switch
            {
                ("tasknumber", false) => baseQuery.OrderBy(t => t.TaskNumber),
                ("tasknumber", true) => baseQuery.OrderByDescending(t => t.TaskNumber),
                ("status", false) => baseQuery.OrderBy(t => t.Status),
                ("status", true) => baseQuery.OrderByDescending(t => t.Status),
                ("created", false) => baseQuery.OrderBy(t => t.CreatedAt),
                ("created", true) => baseQuery.OrderByDescending(t => t.CreatedAt),
                ("completed", false) => baseQuery.OrderBy(t => t.CompletedAt),
                ("completed", true) => baseQuery.OrderByDescending(t => t.CompletedAt),
                _ => baseQuery.OrderByDescending(t => t.CreatedAt)
            };
            var tasks = await ordered
                .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
                .ToListAsync(ct);

            var ids = tasks.Select(t => t.Id).ToList();
            var deviceCounts = await _db.ConfigureTaskDevices
                .Where(d => ids.Contains(d.ConfigureTaskId))
                .GroupBy(d => d.ConfigureTaskId)
                .Select(g => new
                {
                    TaskId = g.Key,
                    Total = g.Count(),
                    Complete = g.Count(d => d.Status == DeviceConfigStatus.ConfigComplete)
                })
                .ToListAsync(ct);
            var countMap = deviceCounts.ToDictionary(x => x.TaskId, x => (x.Total, x.Complete));

            var items = tasks.Select(t =>
            {
                countMap.TryGetValue(t.Id, out var counts);
                return new ConfigureTaskListItemDto(
                    t.Id, t.TaskNumber, t.ProfileName, t.Profile?.Product?.Name ?? string.Empty,
                    counts.Complete, counts.Total,
                    t.Status, t.CreatedAt, t.CompletedAt);
            }).ToList();

            return new ConfigureTaskPagedResult(items, total, q.Page, q.PageSize);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (
                int page, int pageSize, string? sort,
                string? taskNumber, Guid? productId, ConfigureTaskStatus? status,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Query(page, pageSize, sort, taskNumber, productId, status), ct)))
        .RequireAuthorization(DeviceManagementPermissions.ConfigTasks.List)
        .WithSummary("List config tasks (filtered, sorted, paginated)");
}
