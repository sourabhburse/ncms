using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.UpgradeTasks;

/// <summary>
/// Filter/sort parameters for the Firmware Tasks (upgrade tasks) index: task-name text and a
/// CreatedAt date range. <paramref name="EndDate"/> is treated as an inclusive whole day.
/// </summary>
public sealed record UpgradeTaskFilter(
    string? Name,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    string? Sort);

/// <summary>
/// Intermediate projection row. The handler enriches it with per-task device counts
/// (a separate aggregate query keyed by the page's task ids).
/// </summary>
public sealed record UpgradeTaskRow(
    Guid Id,
    string Name,
    string FirmwareVersion,
    UpgradeTaskStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>
/// Query composition for the Firmware Tasks index: name/date-range filtering and whitelisted
/// server-side sorting, projected to the intermediate row.
/// </summary>
public sealed class UpgradeTasksSpecification : Specification<UpgradeTask, UpgradeTaskRow>
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<UpgradeTask, object>>> SortMap =
        new Dictionary<string, Expression<Func<UpgradeTask, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = t => t.Name,
            ["status"] = t => t.Status,
            ["created"] = t => t.CreatedAt,
            ["completed"] = t => t.CompletedAt!,
        };

    public UpgradeTasksSpecification(UpgradeTaskFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            var term = $"%{filter.Name.Trim()}%";
            Where(t => EF.Functions.ILike(t.Name, term));
        }

        // Normalize to UTC: Npgsql's 'timestamp with time zone' only accepts offset-0
        // DateTimeOffset parameters, and model-bound date inputs carry the server's local offset.
        if (filter.StartDate is { } start)
        {
            var startUtc = start.ToUniversalTime();
            Where(t => t.CreatedAt >= startUtc);
        }

        // Inclusive end day: everything strictly before the following midnight (UTC).
        if (filter.EndDate is { } end)
        {
            var endUtc = end.ToUniversalTime().AddDays(1);
            Where(t => t.CreatedAt < endUtc);
        }

        ApplySortingOverride(
            filter.Sort,
            applyDefaultOrdering: () => OrderByDescending(t => t.CreatedAt),
            SortMap);

        Select(t => new UpgradeTaskRow(
            t.Id,
            t.Name,
            t.Firmware != null ? t.Firmware.Version : string.Empty,
            t.Status,
            t.CreatedAt,
            t.CompletedAt));
    }
}
