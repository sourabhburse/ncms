using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Data;
using NCMS.IoT.Identity.Entities;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Identity.Features.V1.Users;

public static class ListUsers
{
    public sealed record Query(
        string? Search,
        string? Role,
        bool? IsActive,
        int PageNumber,
        int PageSize) : IRequest<PagedResponse<UserDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<UserDto>>
    {
        private readonly IdentityDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public Handler(IdentityDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async ValueTask<PagedResponse<UserDto>> Handle(Query req, CancellationToken ct)
        {
            var query = _db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(req.Search))
            {
                var term = req.Search.Trim();
                query = query.Where(u =>
                    (u.UserName != null && EF.Functions.ILike(u.UserName, $"%{term}%")) ||
                    (u.Email != null && EF.Functions.ILike(u.Email, $"%{term}%")) ||
                    (u.FirstName != null && EF.Functions.ILike(u.FirstName, $"%{term}%")) ||
                    (u.LastName != null && EF.Functions.ILike(u.LastName, $"%{term}%")));
            }

            if (req.IsActive is { } isActive)
                query = query.Where(u => u.IsActive == isActive);

            if (!string.IsNullOrWhiteSpace(req.Role))
            {
                query = query.Where(u => _db.UserRoles.Any(ur =>
                    ur.UserId == u.Id && _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == req.Role)));
            }

            query = query.OrderBy(u => u.UserName);

            var paged = await query.ToPagedResponseAsync(new PagedQuery(req.PageNumber, req.PageSize), ct);

            var items = new List<UserDto>(paged.Items.Count);
            foreach (var user in paged.Items)
            {
                var roles = await _userManager.GetRolesAsync(user);
                items.Add(new UserDto(
                    user.Id, user.UserName ?? "", user.Email ?? "",
                    user.FirstName, user.LastName, user.IsActive, roles.ToList()));
            }

            return new PagedResponse<UserDto>
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
                string? search, string? role, bool? isActive, int? pageNumber, int? pageSize,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new Query(search, role, isActive, pageNumber ?? 1, pageSize ?? 25), ct)))
            .WithSummary("List users (filterable, paged)")
            .RequireAuthorization(IdentityPermissions.Users.List);
}
