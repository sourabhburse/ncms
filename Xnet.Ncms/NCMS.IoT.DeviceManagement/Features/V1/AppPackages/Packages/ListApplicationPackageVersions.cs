using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

/// <summary>
/// Lists versions, optionally scoped to one package. <paramref name="Query.DeployableOnly"/>
/// restricts to enabled versions — the set a deployment-task or bundle-item picker may
/// select from (mirrors the "disabled versions are never offered" rule enforced again,
/// authoritatively, in CreateApplicationTask).
/// </summary>
public static class ListApplicationPackageVersions
{
    public sealed record Query(Guid? ApplicationPackageId, bool DeployableOnly) : IRequest<List<ApplicationPackageVersionListItemDto>>;

    public sealed class Handler : IRequestHandler<Query, List<ApplicationPackageVersionListItemDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<List<ApplicationPackageVersionListItemDto>> Handle(Query q, CancellationToken ct)
        {
            var spec = new ApplicationPackageVersionsSpecification(q.ApplicationPackageId, q.DeployableOnly);
            return await _db.ApplicationPackageVersions.ApplySpecification(spec).ToListAsync(ct);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/versions", async (Guid? applicationPackageId, bool deployableOnly = false, ISender sender = null!, CancellationToken ct = default) =>
            Results.Ok(await sender.Send(new Query(applicationPackageId, deployableOnly), ct)))
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.View)
        .WithSummary("List application package versions");
}
