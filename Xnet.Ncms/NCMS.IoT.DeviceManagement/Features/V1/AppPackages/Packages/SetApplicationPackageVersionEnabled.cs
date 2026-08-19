using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>
/// Enables or disables a application package version. Mirrors SetPackageEnabled (firmware)
/// exactly — a reversible toggle, not a one-way recall. Disabled versions remain visible in
/// the catalog but are excluded from bundle/task package selectors, and become editable
/// again (compatibility, dependencies, binary) while disabled.
/// </summary>
public static class SetApplicationPackageVersionEnabled
{
    public sealed record Command(Guid Id, bool IsEnabled) : IRequest<ApplicationPackageVersionDetailDto>;

    public sealed class Handler : IRequestHandler<Command, ApplicationPackageVersionDetailDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationPackageVersionDetailDto> Handle(Command cmd, CancellationToken ct)
        {
            var version = await _db.ApplicationPackageVersions
                .Include(v => v.SupportedProducts)
                .FirstOrDefaultAsync(v => v.Id == cmd.Id, ct)
                ?? throw new KeyNotFoundException($"Application package version {cmd.Id} not found.");

            if (cmd.IsEnabled)
            {
                if (string.IsNullOrEmpty(version.Sha256Checksum))
                    throw new InvalidOperationException("Upload the artifact file before enabling this version.");
                if (version.SupportedProducts.Count == 0)
                    throw new InvalidOperationException("Set at least one compatible product before enabling this version.");
            }

            version.IsEnabled = cmd.IsEnabled;
            await _db.SaveChangesAsync(ct);

            var detail = await new GetApplicationPackageVersionDetail.Handler(_db)
                .Handle(new GetApplicationPackageVersionDetail.Query(version.Id), ct);
            return detail!;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/versions/{id:guid}/enabled", async (Guid id, bool isEnabled, ISender sender, CancellationToken ct) =>
        {
            try { return Results.Ok(await sender.Send(new Command(id, isEnabled), ct)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.EnableDisable)
        .WithSummary("Enable or disable a application package version");
}
