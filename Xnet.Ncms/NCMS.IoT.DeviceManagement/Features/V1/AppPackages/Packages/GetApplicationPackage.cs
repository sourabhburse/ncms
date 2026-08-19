using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

public static class GetApplicationPackage
{
    public sealed record Query(Guid Id) : IRequest<ApplicationPackageDto?>;

    public sealed class Handler : IRequestHandler<Query, ApplicationPackageDto?>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationPackageDto?> Handle(Query q, CancellationToken ct) =>
            await _db.ApplicationPackages
                .Where(p => p.Id == q.Id)
                .Select(p => new ApplicationPackageDto(p.Id, p.Name, p.Tags, p.Versions.Count, p.CreatedAt))
                .FirstOrDefaultAsync(ct);
    }
}
