using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Seeding;

public static class HardwareInventorySeeder
{
    public static async Task SeedAsync(
        DeviceManagementDbContext db,
        string serialNumber,
        Guid productId,
        CancellationToken ct = default)
    {
        var alreadyExists = await db.HardwareInventory
            .AnyAsync(h => h.SerialNumber == serialNumber, ct);

        if (alreadyExists)
            return;

        db.HardwareInventory.Add(new HardwareInventory
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            SerialNumber = serialNumber,
            IsProvisioned = false
        });

        await db.SaveChangesAsync(ct);
    }
}
