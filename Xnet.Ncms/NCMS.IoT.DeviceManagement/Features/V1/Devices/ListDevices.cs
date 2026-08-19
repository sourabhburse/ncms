using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.Persistence.Pagination;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.Devices;

/// <summary>
/// Lists provisioned devices with server-side filtering (text + product hierarchy),
/// whitelisted sorting, and shared pagination (<see cref="PagedResponse{T}"/>).
/// </summary>
public static class ListDevices
{
    public sealed record Query(
        string? Search,
        Guid? CategoryId,
        Guid? TypeId,
        Guid? ProductId,
        string? Sort,
        int Page,
        int PageSize) : IRequest<PagedResponse<DeviceListItemDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<DeviceListItemDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<PagedResponse<DeviceListItemDto>> Handle(Query q, CancellationToken ct)
        {
            var spec = new DevicesSpecification(
                new DeviceFilter(q.Search, q.CategoryId, q.TypeId, q.ProductId, q.Sort));

            return await _db.Devices
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
        .WithSummary("List provisioned devices (filtered, sorted, paginated)")
        .RequireAuthorization(DeviceManagementPermissions.Devices.List);
}
