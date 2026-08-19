using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>
/// Creates a new version under an existing ApplicationPackage, including its Product
/// compatibility — mirrors Firmware's CreatePackage taking ProductIds inline, so a version
/// is fully configured in one step and enabled (IsEnabled = true) by default. The binary is
/// still attached in a second call (<see cref="UploadApplicationPackageVersionBinary"/>), same
/// two-step Create → Upload flow as firmware, driven from one UI submit.
///
/// Compatibility is Product-only, mirroring Firmware exactly — see the domain doc comment on
/// <see cref="ApplicationPackageVersion"/> for why a firmware-version compatibility axis was
/// deliberately rejected.
/// </summary>
public static class CreateApplicationPackageVersion
{
    public sealed record Command(
        Guid ApplicationPackageId,
        string Version,
        string PackageFormat,
        string? ReleaseNotes,
        List<Guid> ProductIds
    ) : IRequest<ApplicationPackageVersionDetailDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
            RuleFor(x => x.PackageFormat).NotEmpty().MaximumLength(50);
        }
    }

    public sealed class Handler : IRequestHandler<Command, ApplicationPackageVersionDetailDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationPackageVersionDetailDto> Handle(Command cmd, CancellationToken ct)
        {
            var packageExists = await _db.ApplicationPackages.AnyAsync(p => p.Id == cmd.ApplicationPackageId, ct);
            if (!packageExists)
                throw new KeyNotFoundException($"Application package {cmd.ApplicationPackageId} not found.");

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

            var version = new ApplicationPackageVersion
            {
                Id = Guid.NewGuid(),
                ApplicationPackageId = cmd.ApplicationPackageId,
                Version = cmd.Version.Trim(),
                PackageFormat = cmd.PackageFormat.Trim(),
                ReleaseNotes = cmd.ReleaseNotes,
                UploadedAt = DateTimeOffset.UtcNow,
                UploadedBy = "system",
                // A version is never deployable at the moment it's created (no artifact
                // uploaded yet) — Enable is a deliberate later step, gated on checksum +
                // compatibility by SetApplicationPackageVersionEnabled. Must be set explicitly:
                // the entity's default (and the DB column default) is true.
                IsEnabled = false
            };

            foreach (var productId in productIds)
                version.SupportedProducts.Add(new ApplicationPackageProductCompatibility
                {
                    ApplicationPackageVersionId = version.Id,
                    ProductId = productId
                });

            _db.ApplicationPackageVersions.Add(version);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
            {
                throw new InvalidOperationException(
                    $"Version '{cmd.Version}' ({cmd.PackageFormat}) already exists for this package.");
            }

            var detail = await new GetApplicationPackageVersionDetail.Handler(_db)
                .Handle(new GetApplicationPackageVersionDetail.Query(version.Id), ct);
            return detail!;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{packageId:guid}/versions", async (
            Guid packageId, CreateApplicationPackageVersionRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var cmd = new Command(packageId, req.Version, req.PackageFormat, req.ReleaseNotes, req.ProductIds);
                var result = await sender.Send(cmd, ct);
                return Results.Created($"/api/v1/application/packages/versions/{result.Id}", result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.Add)
        .WithSummary("Create a new version under a application package (with compatibility)");
}
