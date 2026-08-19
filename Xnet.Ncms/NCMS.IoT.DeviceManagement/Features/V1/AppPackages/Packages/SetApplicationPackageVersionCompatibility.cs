using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>
/// Replaces a version's Product compatibility rows in full — presence-only, mirrors
/// FirmwareProduct exactly (no version ranges, no second compatibility layer beneath it).
/// Product.SupportsSoftwarePackages must be true for every selected product.
///
/// Editing requires the version to be disabled first — mirrors Firmware's UpdatePackage
/// ("Disable the package before editing it"). A brand-new version's initial compatibility is
/// set at creation time (<see cref="CreateApplicationPackageVersion"/>), not through this
/// endpoint, so this gate only affects changing an already-configured version later.
/// </summary>
public static class SetApplicationPackageVersionCompatibility
{
    public sealed record Command(Guid VersionId, List<Guid> ProductIds)
        : IRequest<ApplicationPackageVersionDetailDto>;

    public sealed class Handler : IRequestHandler<Command, ApplicationPackageVersionDetailDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationPackageVersionDetailDto> Handle(Command cmd, CancellationToken ct)
        {
            var version = await _db.ApplicationPackageVersions
                .Include(v => v.SupportedProducts)
                .FirstOrDefaultAsync(v => v.Id == cmd.VersionId, ct)
                ?? throw new KeyNotFoundException($"Application package version {cmd.VersionId} not found.");

            if (version.IsEnabled)
                throw new InvalidOperationException("Disable the version before editing its compatibility.");

            var productIds = cmd.ProductIds.Distinct().ToList();
            if (productIds.Count > 0)
            {
                var eligible = await _db.Products
                    .Where(p => productIds.Contains(p.Id) && p.SupportsSoftwarePackages && !p.IsDeleted)
                    .Select(p => p.Id)
                    .ToListAsync(ct);

                if (eligible.Count != productIds.Count)
                    throw new InvalidOperationException(
                        "One or more selected product models do not exist or do not support application packages.");
            }

            version.SupportedProducts.Clear();
            foreach (var productId in productIds)
                version.SupportedProducts.Add(new ApplicationPackageProductCompatibility
                {
                    ApplicationPackageVersionId = version.Id,
                    ProductId = productId
                });

            await _db.SaveChangesAsync(ct);

            var detail = await new GetApplicationPackageVersionDetail.Handler(_db)
                .Handle(new GetApplicationPackageVersionDetail.Query(version.Id), ct);
            return detail!;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/versions/{id:guid}/compatibility", async (
            Guid id, SetApplicationCompatibilityRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var result = await sender.Send(new Command(id, req.ProductIds), ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.Edit)
        .WithSummary("Replace a disabled version's Product compatibility");
}
