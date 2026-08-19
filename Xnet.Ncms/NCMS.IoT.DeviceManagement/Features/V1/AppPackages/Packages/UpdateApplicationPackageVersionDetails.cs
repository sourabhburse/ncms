using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>
/// Updates a version's Release Notes — purely informational metadata, read only for display
/// (never consulted by compatibility or dispatch logic), so unlike compatibility this is
/// intentionally editable regardless of IsEnabled. Version and PackageFormat are not part of
/// this command: they're the identity/uniqueness key and have no update path by design.
/// </summary>
public static class UpdateApplicationPackageVersionDetails
{
    public sealed record Command(Guid Id, string? ReleaseNotes)
        : IRequest<ApplicationPackageVersionDetailDto>;

    public sealed class Handler : IRequestHandler<Command, ApplicationPackageVersionDetailDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationPackageVersionDetailDto> Handle(Command cmd, CancellationToken ct)
        {
            var version = await _db.ApplicationPackageVersions.FindAsync([cmd.Id], ct)
                ?? throw new KeyNotFoundException($"Application package version {cmd.Id} not found.");

            version.ReleaseNotes = cmd.ReleaseNotes;
            await _db.SaveChangesAsync(ct);

            var detail = await new GetApplicationPackageVersionDetail.Handler(_db)
                .Handle(new GetApplicationPackageVersionDetail.Query(version.Id), ct);
            return detail!;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/versions/{id:guid}/details", async (
            Guid id, UpdateApplicationPackageVersionDetailsRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var result = await sender.Send(new Command(id, req.ReleaseNotes), ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.Edit)
        .WithSummary("Update a version's Release Notes (always editable)");
}

public sealed record UpdateApplicationPackageVersionDetailsRequest(string? ReleaseNotes);
