using Mediator;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Inventory;

public static class BatchDeleteHardwareInventory
{
    public sealed record Command(IReadOnlyList<Guid> Ids) : IRequest<int>;

    public sealed class Handler : IRequestHandler<Command, int>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<int> Handle(Command cmd, CancellationToken ct)
        {
            if (cmd.Ids.Count == 0) return 0;

            var entries = await _db.HardwareInventory
                .Where(h => cmd.Ids.Contains(h.Id) && !h.IsProvisioned)
                .ToListAsync(ct);

            if (entries.Count == 0) return 0;

            _db.HardwareInventory.RemoveRange(entries);
            await _db.SaveChangesAsync(ct);
            return entries.Count;
        }
    }
}
