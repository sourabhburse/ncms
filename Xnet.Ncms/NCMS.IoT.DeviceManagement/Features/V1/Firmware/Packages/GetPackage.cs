using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

public static class GetPackage
{
    public sealed record Query(Guid Id) : IRequest<FirmwareDto?>;

    public sealed class Handler : IRequestHandler<Query, FirmwareDto?>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<FirmwareDto?> Handle(Query q, CancellationToken ct)
        {
            var p = await _db.Firmwares.FindAsync([q.Id], ct);
            return p is null ? null : ListPackages.Handler.ToDto(p);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new Query(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization(DeviceManagementPermissions.Packages.View)
        .WithSummary("Get a firmware package by ID");
}
