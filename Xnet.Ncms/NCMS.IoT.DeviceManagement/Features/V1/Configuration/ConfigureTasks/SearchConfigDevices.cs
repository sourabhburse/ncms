using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.ConfigureTasks;

/// <summary>
/// Searches devices eligible for a config task — filtered by the selected product model.
/// Mirrors firmware SearchDevices.
/// </summary>
public static class SearchConfigDevices
{
    public sealed record Query(
        Guid? ProductId,
        string? ProductModel,
        string? SerialNumber,
        string? DeviceStatus,
        int Page,
        int PageSize
    ) : IRequest<ConfigDeviceSearchPagedResult>;

    public sealed class Handler : IRequestHandler<Query, ConfigDeviceSearchPagedResult>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ConfigDeviceSearchPagedResult> Handle(Query q, CancellationToken ct)
        {
            var query = _db.Devices
                .Include(d => d.HardwareInventory).ThenInclude(h => h.Product)
                .AsQueryable();

            if (q.ProductId.HasValue)
                query = query.Where(d => d.HardwareInventory.ProductId == q.ProductId.Value);
            if (!string.IsNullOrEmpty(q.ProductModel))
                query = query.Where(d => d.HardwareInventory.Product.Name.Contains(q.ProductModel));
            if (!string.IsNullOrEmpty(q.SerialNumber))
                query = query.Where(d =>
                    d.HardwareInventory.SerialNumber.Contains(q.SerialNumber) || d.Name.Contains(q.SerialNumber));

            // Online/Offline status is filtered server-side (kept out of the client).
            if (!string.IsNullOrEmpty(q.DeviceStatus))
            {
                var wantOnline = string.Equals(q.DeviceStatus, "Online", StringComparison.OrdinalIgnoreCase);
                query = query.Where(d => d.IsOnline == wantOnline);
            }

            // Server-side pagination via the shared Persistence infrastructure.
            var projected = query
                .OrderBy(d => d.HardwareInventory.SerialNumber)
                .Select(d => new ConfigDeviceSearchResult(
                    d.Id,
                    string.IsNullOrWhiteSpace(d.Name) ? d.HardwareInventory.SerialNumber : d.Name,
                    d.HardwareInventory.Product.Name,
                    d.FirmwareVersion,
                    d.IsOnline ? "Online" : "Offline"));

            var paged = await projected.ToPagedResponseAsync(new PagedQuery(q.Page, q.PageSize), ct);
            return new ConfigDeviceSearchPagedResult(paged.Items.ToList(), (int)paged.TotalCount);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/devices/search",
            async (Guid? productId, string? productModel, string? serialNumber, string? deviceStatus,
                   int page, int pageSize, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new Query(productId, productModel, serialNumber, deviceStatus, page, pageSize), ct)))
        .RequireAuthorization(DeviceManagementPermissions.ConfigTasks.Add)
        .WithSummary("Search devices eligible for a config task");
}
