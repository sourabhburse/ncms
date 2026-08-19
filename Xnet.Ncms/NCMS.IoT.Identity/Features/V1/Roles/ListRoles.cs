using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Entities;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Identity.Features.V1.Roles;

public static class ListRoles
{
    public sealed record Query(string? Search, int PageNumber, int PageSize) : IRequest<PagedResponse<RoleDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<RoleDto>>
    {
        private readonly RoleManager<AppRole> _roleManager;

        public Handler(RoleManager<AppRole> roleManager) => _roleManager = roleManager;

        public async ValueTask<PagedResponse<RoleDto>> Handle(Query req, CancellationToken ct)
        {
            var query = _roleManager.Roles.AsQueryable();

            if (!string.IsNullOrWhiteSpace(req.Search))
            {
                var term = req.Search.Trim();
                query = query.Where(r =>
                    (r.Name != null && EF.Functions.ILike(r.Name, $"%{term}%")) ||
                    (r.Description != null && EF.Functions.ILike(r.Description, $"%{term}%")));
            }

            query = query.OrderBy(r => r.Name);

            return await query
                .Select(r => new RoleDto(r.Id, r.Name!, r.Description))
                .ToPagedResponseAsync(new PagedQuery(req.PageNumber, req.PageSize), ct);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (string? search, int? pageNumber, int? pageSize, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new Query(search, pageNumber ?? 1, pageSize ?? 25), ct)))
            .WithSummary("List roles (filterable, paged)")
            .RequireAuthorization(IdentityPermissions.Roles.List);
}
