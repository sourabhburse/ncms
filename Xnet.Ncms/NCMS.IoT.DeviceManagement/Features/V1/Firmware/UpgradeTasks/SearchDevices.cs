using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.UpgradeTasks;

public static class SearchDevices
{
    public sealed record Query(
        Guid? FirmwarePackageId,
        string? ProductModel,
        string? FirmwareVersion,
        string? SerialNumber,
        string? DeviceStatus,
        int Page,
        int PageSize
    ) : IRequest<FirmwareDeviceSearchPagedResult>;

    public sealed class Handler : IRequestHandler<Query, FirmwareDeviceSearchPagedResult>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<FirmwareDeviceSearchPagedResult> Handle(Query q, CancellationToken ct)
        {
            var query = _db.Devices
                .Include(d => d.HardwareInventory).ThenInclude(h => h.Product)
                .AsQueryable();

            // "Product Model" filters on the linked product catalog name.
            if (!string.IsNullOrEmpty(q.ProductModel))
                query = query.Where(d => d.HardwareInventory.Product.Name.Contains(q.ProductModel));
            if (!string.IsNullOrEmpty(q.FirmwareVersion))
                query = query.Where(d => d.FirmwareVersion == q.FirmwareVersion);
            // "Device Name or Code" matches the device name or its hardware serial number.
            if (!string.IsNullOrEmpty(q.SerialNumber))
                query = query.Where(d =>
                    d.HardwareInventory.SerialNumber.Contains(q.SerialNumber) || d.Name.Contains(q.SerialNumber));

            // Online/Offline status is filtered server-side (kept out of the client).
            if (!string.IsNullOrEmpty(q.DeviceStatus))
            {
                var wantOnline = string.Equals(q.DeviceStatus, "Online", StringComparison.OrdinalIgnoreCase);
                query = query.Where(d => d.IsOnline == wantOnline);
            }

            if (q.FirmwarePackageId.HasValue)
            {
                var supportedProductIds = await _db.FirmwareProducts
                    .Where(fp => fp.FirmwareId == q.FirmwarePackageId.Value)
                    .Select(fp => fp.ProductId)
                    .ToListAsync(ct);
                if (supportedProductIds.Count > 0)
                    query = query.Where(d => supportedProductIds.Contains(d.HardwareInventory.ProductId));
            }

            // Server-side pagination via the shared Persistence infrastructure (ordering applied
            // before the projection, as ToPagedResponseAsync expects).
            var projected = query
                .OrderBy(d => d.HardwareInventory.SerialNumber)
                .Select(d => new FirmwareDeviceSearchResult(
                    d.Id,
                    string.IsNullOrWhiteSpace(d.Name) ? d.HardwareInventory.SerialNumber : d.Name,
                    d.HardwareInventory.Product.Name,
                    d.FirmwareVersion,
                    d.IsOnline ? "Online" : "Offline"));

            var paged = await projected.ToPagedResponseAsync(new PagedQuery(q.Page, q.PageSize), ct);
            return new FirmwareDeviceSearchPagedResult(paged.Items.ToList(), (int)paged.TotalCount);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/devices/search",
            async (Guid? firmwarePackageId, string? productModel, string? firmwareVersion,
                   string? serialNumber, string? deviceStatus, int page, int pageSize, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new Query(firmwarePackageId, productModel, firmwareVersion, serialNumber, deviceStatus, page, pageSize), ct)))
        .RequireAuthorization(DeviceManagementPermissions.UpgradeTasks.Add)
        .WithSummary("Search devices eligible for a firmware upgrade task");
}
