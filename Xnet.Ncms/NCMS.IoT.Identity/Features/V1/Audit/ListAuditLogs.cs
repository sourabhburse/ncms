using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Data;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Identity.Features.V1.Audit;

public static class ListAuditLogs
{
    public sealed record Query(
        IReadOnlyList<AuditEventType>? EventTypes,
        string? Search,
        DateTimeOffset? From,
        DateTimeOffset? To,
        int PageNumber,
        int PageSize) : IRequest<PagedResponse<AuditLogDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<AuditLogDto>>
    {
        private readonly IdentityDbContext _db;

        public Handler(IdentityDbContext db) => _db = db;

        public async ValueTask<PagedResponse<AuditLogDto>> Handle(Query req, CancellationToken ct)
        {
            var query = _db.AuditLogs.AsQueryable();

            if (req.EventTypes is { Count: > 0 } eventTypes)
                query = query.Where(a => eventTypes.Contains(a.EventType));

            if (!string.IsNullOrWhiteSpace(req.Search))
            {
                var term = req.Search.Trim();
                query = query.Where(a =>
                    (a.SubjectDisplay != null && EF.Functions.ILike(a.SubjectDisplay, $"%{term}%")) ||
                    (a.ActorDisplay != null && EF.Functions.ILike(a.ActorDisplay, $"%{term}%")) ||
                    EF.Functions.ILike(a.Description, $"%{term}%"));
            }

            if (req.From is { } from) query = query.Where(a => a.OccurredAt >= from);
            if (req.To is { } to) query = query.Where(a => a.OccurredAt <= to);

            query = query.OrderByDescending(a => a.OccurredAt);

            return await query
                .Select(a => new AuditLogDto(
                    a.Id, a.OccurredAt, a.EventType, a.SubjectUserId, a.SubjectDisplay,
                    a.ActorUserId, a.ActorDisplay, a.Description, a.IpAddress))
                .ToPagedResponseAsync(new PagedQuery(req.PageNumber, req.PageSize), ct);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (
                AuditEventType[]? eventType, string? search, DateTimeOffset? from, DateTimeOffset? to,
                int? pageNumber, int? pageSize, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new Query(eventType, search, from, to, pageNumber ?? 1, pageSize ?? 25), ct)))
        .WithSummary("List audit/security events (logins, logouts, password/role/permission changes)")
        .RequireAuthorization(IdentityPermissions.AuditLogs.List);
}
