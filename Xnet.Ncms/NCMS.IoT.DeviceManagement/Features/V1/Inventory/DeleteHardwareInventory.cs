using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Inventory;

public static class DeleteHardwareInventory
{
    public sealed record Command(Guid Id) : IRequest<Unit>;

    public sealed class Handler : IRequestHandler<Command, Unit>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<Unit> Handle(Command cmd, CancellationToken ct)
        {
            var entry = await _db.HardwareInventory
                .FirstOrDefaultAsync(h => h.Id == cmd.Id, ct)
                ?? throw new InvalidOperationException("Hardware entry not found.");

            if (entry.IsProvisioned)
                throw new InvalidOperationException(
                    $"Cannot delete '{entry.SerialNumber}' — it has an active device. Decommission the device first.");

            _db.HardwareInventory.Remove(entry);
            await _db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
