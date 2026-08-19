using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>
/// Deletes an application package version. Mirrors Firmware's DeletePackage: must be disabled
/// first, and must never have actually been used — referenced by a deployment task, a
/// device's current inventory, or the installation history ledger. Those FKs are configured
/// Restrict (see the respective EntityTypeConfiguration), so the database would reject the
/// delete regardless; these are pre-checks for a clear error message instead of a raw
/// FK-violation exception.
///
/// When this removes a package's last version, the now-empty parent <see cref="ApplicationPackage"/>
/// is deleted too — so deleting the only version fully removes the package (like Firmware, where
/// a build and its identity are one row), and no orphaned package lingers to leak stale tags/name
/// into filters. The parent is kept only if something still legitimately references it (another
/// version's dependency declaration, or a device's application inventory).
/// </summary>
public static class DeleteApplicationPackageVersion
{
    public sealed record Command(Guid Id) : IRequest<Unit>;

    public sealed class Handler : IRequestHandler<Command, Unit>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<Unit> Handle(Command cmd, CancellationToken ct)
        {
            var version = await _db.ApplicationPackageVersions.FindAsync([cmd.Id], ct)
                ?? throw new KeyNotFoundException($"Application package version {cmd.Id} not found.");

            if (version.IsEnabled)
                throw new InvalidOperationException("Disable the version before deleting it.");

            if (await _db.ApplicationTaskDevices.AnyAsync(i => i.ApplicationPackageVersionId == cmd.Id, ct))
                throw new InvalidOperationException("This version is referenced by one or more application tasks and cannot be deleted.");

            if (await _db.DeviceApplicationInventory.AnyAsync(i => i.InstalledVersionId == cmd.Id, ct))
                throw new InvalidOperationException("This version is currently installed on one or more devices and cannot be deleted.");

            if (await _db.ApplicationInstallationHistory.AnyAsync(h => h.ApplicationPackageVersionId == cmd.Id, ct))
                throw new InvalidOperationException("This version has installation history and cannot be deleted.");

            _db.ApplicationPackageVersions.Remove(version); // compatibility/dependency rows cascade-delete

            // If this was the package's last version, remove the now-empty parent package too,
            // unless something still references it (a dependency target, or device inventory).
            var packageId = version.ApplicationPackageId;
            var hasOtherVersions = await _db.ApplicationPackageVersions
                .AnyAsync(v => v.ApplicationPackageId == packageId && v.Id != cmd.Id, ct);
            if (!hasOtherVersions)
            {
                var referenced =
                    await _db.ApplicationPackageDependencies.AnyAsync(d => d.DependsOnApplicationPackageId == packageId, ct)
                    || await _db.DeviceApplicationInventory.AnyAsync(i => i.ApplicationPackageId == packageId, ct);

                if (!referenced)
                {
                    var package = await _db.ApplicationPackages.FindAsync([packageId], ct);
                    if (package is not null)
                        _db.ApplicationPackages.Remove(package);
                }
            }

            await _db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/versions/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            try
            {
                await sender.Send(new Command(id), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.Delete)
        .WithSummary("Delete a disabled, never-used application package version");
}
