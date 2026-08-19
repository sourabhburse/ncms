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
/// Backs the single "Add Package" action on the merged Application Packages page — the flat,
/// Firmware-style view where every row is one version. Creates a version and, if no package
/// with the given name exists yet, its owning package too, in one step. Re-using the same
/// package name simply appends a new version to that package (the Firmware model, where adding
/// the same name again is how you publish a new build). The version is created Disabled — the
/// artifact is uploaded and the version enabled as separate steps, per the established application
/// lifecycle.
/// </summary>
public static class CreateApplicationPackageWithVersion
{
    public sealed record Command(
        string Name,
        List<string> Tags,
        string Version,
        string PackageFormat,
        string? ReleaseNotes,
        List<Guid> ProductIds
    ) : IRequest<ApplicationPackageVersionDetailDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleForEach(x => x.Tags).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
            RuleFor(x => x.PackageFormat).NotEmpty().MaximumLength(50);
            RuleFor(x => x.ProductIds).NotEmpty().WithMessage("Select a product model.");
        }
    }

    public sealed class Handler : IRequestHandler<Command, ApplicationPackageVersionDetailDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationPackageVersionDetailDto> Handle(Command cmd, CancellationToken ct)
        {
            var name = cmd.Name.Trim();

            // Find-or-create the owning package by name. An existing package is reused as-is —
            // its tags are left untouched, since re-adding a name is about publishing another
            // build, not editing the package identity (rename/retag is the Edit action).
            var package = await _db.ApplicationPackages
                .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower(), ct);

            if (package is null)
            {
                package = new ApplicationPackage
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Tags = TagNormalizer.Normalize(cmd.Tags),
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _db.ApplicationPackages.Add(package);
            }

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
                ApplicationPackageId = package.Id,
                Version = cmd.Version.Trim(),
                PackageFormat = cmd.PackageFormat.Trim(),
                ReleaseNotes = cmd.ReleaseNotes,
                UploadedAt = DateTimeOffset.UtcNow,
                UploadedBy = "system",
                // Created Disabled — no artifact yet. Enable is a deliberate later step gated on
                // checksum + compatibility. Must be set explicitly (entity/DB default is true).
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
                    $"Version '{cmd.Version}' ({cmd.PackageFormat}) already exists for package '{name}'.");
            }

            var detail = await new GetApplicationPackageVersionDetail.Handler(_db)
                .Handle(new GetApplicationPackageVersionDetail.Query(version.Id), ct);
            return detail!;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/with-version", async (
            CreateApplicationPackageWithVersionRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var cmd = new Command(req.Name, req.Tags, req.Version, req.PackageFormat,
                    req.ReleaseNotes, req.ProductIds);
                var result = await sender.Send(cmd, ct);
                return Results.Created($"/api/v1/application/packages/versions/{result.Id}", result);
            }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.Add)
        .WithSummary("Add a package (find-or-create by name) with its first/next version");
}
