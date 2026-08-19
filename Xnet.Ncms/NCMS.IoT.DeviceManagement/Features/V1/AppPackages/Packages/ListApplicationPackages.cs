using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

public static class ListApplicationPackages
{
    public sealed record Query(string? Tag) : IRequest<List<ApplicationPackageDto>>;

    public sealed class Handler : IRequestHandler<Query, List<ApplicationPackageDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<List<ApplicationPackageDto>> Handle(Query q, CancellationToken ct)
        {
            var query = _db.ApplicationPackages.AsQueryable();
            if (!string.IsNullOrEmpty(q.Tag))
                query = query.Where(p => p.Tags.Contains(q.Tag));

            return await query
                .OrderBy(p => p.Name)
                .Select(p => new ApplicationPackageDto(
                    p.Id, p.Name, p.Tags, p.Versions.Count, p.CreatedAt))
                .ToListAsync(ct);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (string? tag, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Query(tag), ct)))
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.List)
        .WithSummary("List application packages (logical identities)");
}
