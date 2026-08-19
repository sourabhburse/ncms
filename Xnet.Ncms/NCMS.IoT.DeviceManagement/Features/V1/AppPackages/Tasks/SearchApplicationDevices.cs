using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Tasks;

/// <summary>
/// Device picker for the application-task creation wizard. Filters to devices whose Product
/// has SupportsSoftwarePackages = true and — when a target version is supplied — is
/// compatible with it (mirrors CreateApplicationTask's authoritative check; this is a
/// convenience filter for the picker, not itself the source of truth). Reuses the generic
/// FirmwareDeviceSearchResult/PagedResult shape — the result is device search results, not a
/// firmware-specific concept.
/// </summary>
public static class SearchApplicationDevices
{
    public sealed record Query(
        Guid? ApplicationPackageVersionId,
        string? ProductModel,
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
                .Where(d => d.HardwareInventory.Product.SupportsSoftwarePackages)
                .AsQueryable();

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

            if (q.ApplicationPackageVersionId is { } versionId)
            {
                var eligibleProductIds = await _db.ApplicationPackageProductCompat
                    .Where(c => c.ApplicationPackageVersionId == versionId)
                    .Select(c => c.ProductId)
                    .ToListAsync(ct);

                query = query.Where(d => eligibleProductIds.Contains(d.HardwareInventory.ProductId));
            }

            // Server-side pagination via the shared Persistence infrastructure.
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
            async (Guid? applicationPackageVersionId, string? productModel,
                   string? serialNumber, string? deviceStatus, int page, int pageSize, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new Query(applicationPackageVersionId, productModel, serialNumber, deviceStatus, page, pageSize), ct)))
        .RequireAuthorization(DeviceManagementPermissions.ApplicationTasks.Add)
        .WithSummary("Search devices eligible for an application deployment task");
}
