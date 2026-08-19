using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

public static class GetApplicationPackageVersionDetail
{
    public sealed record Query(Guid Id) : IRequest<ApplicationPackageVersionDetailDto?>;

    public sealed class Handler : IRequestHandler<Query, ApplicationPackageVersionDetailDto?>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationPackageVersionDetailDto?> Handle(Query q, CancellationToken ct)
        {
            var v = await _db.ApplicationPackageVersions
                .AsNoTracking()
                .Where(x => x.Id == q.Id)
                .Select(x => new
                {
                    x.Id,
                    x.ApplicationPackageId,
                    PackageName = x.ApplicationPackage.Name,
                    Tags = x.ApplicationPackage.Tags,
                    x.Version,
                    x.PackageFormat,
                    x.FileName,
                    x.StoragePath,
                    x.SizeBytes,
                    x.Sha256Checksum,
                    x.Md5Checksum,
                    x.Metadata,
                    x.ReleaseNotes,
                    x.IsEnabled,
                    x.UploadedAt,
                    x.UploadedBy,
                    Products = x.SupportedProducts.Select(c => new { c.ProductId, c.Product.Name }).ToList(),
                    Dependencies = x.Dependencies.Select(d => new
                    {
                        d.DependsOnApplicationPackageId,
                        DependsOnPackageName = d.DependsOnApplicationPackage.Name,
                        d.VersionConstraint
                    }).ToList()
                })
                .FirstOrDefaultAsync(ct);

            if (v is null) return null;

            return new ApplicationPackageVersionDetailDto(
                v.Id, v.ApplicationPackageId, v.PackageName, v.Tags,
                v.Version, v.PackageFormat,
                v.FileName ?? string.Empty, v.StoragePath ?? string.Empty, v.SizeBytes,
                v.Sha256Checksum ?? string.Empty, v.Md5Checksum, v.Metadata, v.ReleaseNotes,
                v.IsEnabled, v.UploadedAt, v.UploadedBy,
                v.Products.Select(p => p.ProductId).ToList(),
                v.Products.Select(p => p.Name).ToList(),
                v.Dependencies.Select(d => new ApplicationPackageDependencyDto(
                    d.DependsOnApplicationPackageId, d.DependsOnPackageName, d.VersionConstraint)).ToList());
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/versions/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new Query(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.View)
        .WithSummary("Get application package version detail (compatibility + dependencies)");
}
