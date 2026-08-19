using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;
using FirmwareEntity = NCMS.IoT.DeviceManagement.Entities.Firmware;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

public static class ListPackages
{
    public sealed record Query(string? DeviceTypeCode, bool OnlyEnabled = false)
        : IRequest<List<FirmwareDto>>;

    public sealed class Handler : IRequestHandler<Query, List<FirmwareDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<List<FirmwareDto>> Handle(Query q, CancellationToken ct)
        {
            var query = _db.Firmwares.AsQueryable();
            if (!string.IsNullOrEmpty(q.DeviceTypeCode))
                query = query.Where(p => p.DeviceTypeCode == q.DeviceTypeCode);
            if (q.OnlyEnabled)
                query = query.Where(p => p.IsEnabled);
            return await query.OrderByDescending(p => p.UploadedAt).Select(p => ToDto(p)).ToListAsync(ct);
        }

        internal static FirmwareDto ToDto(FirmwareEntity p) => new(
            p.Id, p.Name, p.Version, p.Description, p.ReleaseNotes, p.DeviceTypeCode,
            p.BinaryChecksum ?? string.Empty, p.StoragePath ?? string.Empty, p.Size,
            p.MinRequiredFirmwareVersion, p.CreatedBy, p.UploadedAt, p.IsEnabled);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (
            string? deviceTypeCode,
            ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(new Query(deviceTypeCode), ct)))
        .RequireAuthorization(DeviceManagementPermissions.Packages.List)
        .WithSummary("List firmware packages");
}
