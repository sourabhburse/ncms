using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

public static class DeletePackage
{
    public sealed record Command(Guid Id) : IRequest<Unit>;

    public sealed class Handler : IRequestHandler<Command, Unit>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<Unit> Handle(Command cmd, CancellationToken ct)
        {
            var pkg = await _db.Firmwares
                .Include(f => f.SupportedProducts)
                .FirstOrDefaultAsync(f => f.Id == cmd.Id, ct)
                ?? throw new KeyNotFoundException($"Firmware package {cmd.Id} not found.");

            if (pkg.IsEnabled)
                throw new InvalidOperationException("Disable the package before deleting it.");

            var usedByTask = await _db.UpgradeTasks.AnyAsync(t => t.FirmwareId == cmd.Id, ct);
            if (usedByTask)
                throw new InvalidOperationException(
                    "This package is referenced by one or more upgrade tasks and cannot be deleted.");

            _db.Firmwares.Remove(pkg); // FirmwareProduct links cascade-delete
            await _db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            try
            {
                await sender.Send(new Command(id), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.Packages.Delete)
        .WithSummary("Delete a firmware package");
}
