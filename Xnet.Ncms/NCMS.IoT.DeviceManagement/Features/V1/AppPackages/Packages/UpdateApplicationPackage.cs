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

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>
/// Renames a application package and/or changes its category. ApplicationPackage carries no
/// lifecycle of its own (see its class doc) — unlike a version's compatibility, there's no
/// "must be disabled first" gate here; a rename doesn't change deployment behavior for any
/// existing version.
/// </summary>
public static class UpdateApplicationPackage
{
    public sealed record Command(Guid Id, string Name, List<string> Tags) : IRequest<ApplicationPackageDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleForEach(x => x.Tags).NotEmpty().MaximumLength(50);
        }
    }

    public sealed class Handler : IRequestHandler<Command, ApplicationPackageDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationPackageDto> Handle(Command cmd, CancellationToken ct)
        {
            var pkg = await _db.ApplicationPackages.FindAsync([cmd.Id], ct)
                ?? throw new KeyNotFoundException($"Application package {cmd.Id} not found.");

            pkg.Name = cmd.Name.Trim();
            pkg.Tags = TagNormalizer.Normalize(cmd.Tags);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
            {
                throw new InvalidOperationException($"A application package named '{cmd.Name}' already exists.");
            }

            var versionCount = await _db.ApplicationPackageVersions.CountAsync(v => v.ApplicationPackageId == pkg.Id, ct);
            return new ApplicationPackageDto(pkg.Id, pkg.Name, pkg.Tags, versionCount, pkg.CreatedAt);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{id:guid}", async (Guid id, CreateApplicationPackageRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var result = await sender.Send(new Command(id, req.Name, req.Tags), ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.Edit)
        .WithSummary("Rename a application package or change its category");
}
