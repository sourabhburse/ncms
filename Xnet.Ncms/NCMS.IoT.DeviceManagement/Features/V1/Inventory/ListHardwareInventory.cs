using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.Persistence.Pagination;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.Inventory;

/// <summary>
/// Lists hardware inventory with server-side filtering (text + product hierarchy),
/// whitelisted sorting, and shared pagination (<see cref="PagedResponse{T}"/>).
/// </summary>
public static class ListHardwareInventory
{
    public sealed record Query(
        string? Search,
        Guid? CategoryId,
        Guid? TypeId,
        Guid? ProductId,
        string? Sort,
        int Page,
        int PageSize) : IRequest<PagedResponse<HardwareInventoryDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<HardwareInventoryDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<PagedResponse<HardwareInventoryDto>> Handle(Query q, CancellationToken ct)
        {
            var spec = new HardwareInventorySpecification(
                new HardwareInventoryFilter(q.Search, q.CategoryId, q.TypeId, q.ProductId, q.Sort));

            return await _db.HardwareInventory
                .ApplySpecification(spec)
                .ToPagedResponseAsync(new PagedQuery(q.Page, q.PageSize), ct);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (
                string? search, Guid? categoryId, Guid? typeId, Guid? productId,
                string? sort, int? page, int? pageSize,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new Query(search, categoryId, typeId, productId, sort, page ?? 1, pageSize ?? 25), ct)))
        .WithSummary("List hardware inventory (filtered, sorted, paginated)")
        .RequireAuthorization(DeviceManagementPermissions.Inventory.List);
}
