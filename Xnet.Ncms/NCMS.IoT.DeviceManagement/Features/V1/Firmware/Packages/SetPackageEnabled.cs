using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

/// <summary>
/// Enables or disables a firmware package. Disabled packages remain visible in the
/// catalog but are excluded from the upgrade-task package selector.
/// </summary>
public static class SetPackageEnabled
{
    public sealed record Command(Guid Id, bool IsEnabled) : IRequest<FirmwareDto>;

    public sealed class Handler : IRequestHandler<Command, FirmwareDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<FirmwareDto> Handle(Command cmd, CancellationToken ct)
        {
            var pkg = await _db.Firmwares.FindAsync([cmd.Id], ct)
                ?? throw new KeyNotFoundException($"Firmware package {cmd.Id} not found.");

            pkg.IsEnabled = cmd.IsEnabled;
            await _db.SaveChangesAsync(ct);
            return ListPackages.Handler.ToDto(pkg);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/enabled", async (Guid id, bool isEnabled, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Command(id, isEnabled), ct)))
        .RequireAuthorization(DeviceManagementPermissions.Packages.EnableDisable)
        .WithSummary("Enable or disable a firmware package");
}
