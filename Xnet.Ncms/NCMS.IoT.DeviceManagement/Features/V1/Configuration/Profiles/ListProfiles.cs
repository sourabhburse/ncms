using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;

/// <summary>Raw list of config profiles (for the task selector + API). Mirrors ListPackages.</summary>
public static class ListProfiles
{
    public sealed record Query(Guid? ProductId, bool OnlyEnabled = false)
        : IRequest<List<ConfigProfileDto>>;

    public sealed class Handler : IRequestHandler<Query, List<ConfigProfileDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<List<ConfigProfileDto>> Handle(Query q, CancellationToken ct)
        {
            var query = _db.ConfigProfiles.Include(p => p.Product).AsQueryable();
            if (q.ProductId.HasValue)
                query = query.Where(p => p.ProductId == q.ProductId.Value);
            if (q.OnlyEnabled)
                query = query.Where(p => p.Status == Contracts.Enums.ProfileStatus.Enable);

            var profiles = await query.OrderByDescending(p => p.UploadedAt).ToListAsync(ct);
            return profiles.Select(ToDto).ToList();
        }

        internal static ConfigProfileDto ToDto(ConfigProfile p) => new(
            p.Id, p.ProfileName, p.OriginalFilename ?? string.Empty, p.StoragePath ?? string.Empty,
            p.Size, p.ProductId, p.Product?.Name ?? string.Empty, p.Status, p.Remark, p.CreatedBy, p.UploadedAt);
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (Guid? productId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Query(productId), ct)))
        .RequireAuthorization(DeviceManagementPermissions.ConfigProfiles.List)
        .WithSummary("List config profiles");
}
