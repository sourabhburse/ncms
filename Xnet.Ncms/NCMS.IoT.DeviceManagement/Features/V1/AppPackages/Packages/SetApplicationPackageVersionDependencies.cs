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
/// Replaces a version's declared dependencies. Informational only — existence of the
/// referenced ApplicationPackage is validated here; the constraint string is free text, never
/// parsed or resolved. No dependency graph, no auto-install — see the architecture decision
/// to defer automatic dependency resolution. Editable regardless of enabled state: unlike
/// compatibility, dependencies never affect deployment eligibility, so there's no risk in
/// changing them on a live version.
/// </summary>
public static class SetApplicationPackageVersionDependencies
{
    public sealed record Command(Guid VersionId, List<(Guid DependsOnApplicationPackageId, string? VersionConstraint)> Dependencies)
        : IRequest<ApplicationPackageVersionDetailDto>;

    public sealed class Handler : IRequestHandler<Command, ApplicationPackageVersionDetailDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationPackageVersionDetailDto> Handle(Command cmd, CancellationToken ct)
        {
            var version = await _db.ApplicationPackageVersions
                .Include(v => v.Dependencies)
                .FirstOrDefaultAsync(v => v.Id == cmd.VersionId, ct)
                ?? throw new KeyNotFoundException($"Application package version {cmd.VersionId} not found.");

            var dependsOnIds = cmd.Dependencies.Select(d => d.DependsOnApplicationPackageId).Distinct().ToList();
            if (dependsOnIds.Contains(version.ApplicationPackageId))
                throw new InvalidOperationException("A package cannot depend on itself.");

            if (dependsOnIds.Count > 0)
            {
                var existing = await _db.ApplicationPackages
                    .Where(p => dependsOnIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
                if (existing.Count != dependsOnIds.Count)
                    throw new InvalidOperationException("One or more dependency targets do not exist in the catalog.");
            }

            version.Dependencies.Clear();
            foreach (var (dependsOnId, constraint) in cmd.Dependencies)
                version.Dependencies.Add(new ApplicationPackageDependency
                {
                    Id = Guid.NewGuid(),
                    ApplicationPackageVersionId = version.Id,
                    DependsOnApplicationPackageId = dependsOnId,
                    VersionConstraint = constraint
                });

            await _db.SaveChangesAsync(ct);

            var detail = await new GetApplicationPackageVersionDetail.Handler(_db)
                .Handle(new GetApplicationPackageVersionDetail.Query(version.Id), ct);
            return detail!;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/versions/{id:guid}/dependencies", async (
            Guid id, SetDependenciesRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var deps = req.Dependencies.Select(d => (d.DependsOnApplicationPackageId, d.VersionConstraint)).ToList();
                var result = await sender.Send(new Command(id, deps), ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.Edit)
        .WithSummary("Replace a version's declared (informational-only) dependencies");
}
